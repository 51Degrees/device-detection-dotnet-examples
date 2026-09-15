<#
.SYNOPSIS
    Runs the unit tests and fails the build if any test class could not be
    discovered.

.DESCRIPTION
    MSTest drops a whole test class when one of its fixture methods has a
    signature the adapter cannot use, for example a [ClassInitialize] method
    that is not static. It writes a line to the console and carries on, so
    the exit code stays zero and the run still reports "Passed!". Nine
    Selenium classes in this repository were dropped that way and the suite
    stayed green throughout, which is the worst thing a test suite can do
    because it reports safety it is not delivering.

    Two things here stop that happening again.

    First, the assembly filter includes the Selenium web test assemblies.
    The old filter matched only 'Tests.Cloud' and 'Tests.OnPremise', so
    'Tests.Web.Cloud', 'Tests.Web.Cloud.ClientOnly' and
    'Tests.Web.OnPremise' never ran in CI at all, and no fix to those
    classes could have shown up here.

    Second, the console output of the whole run is captured and searched for
    the adapter's discovery messages, and any hit fails the step. That check
    does not depend on the test code, so it still works for an assembly
    carrying no guard of its own.

    There is also an in assembly guard, Tests/Shared/TestDiscoveryGuard.cs,
    linked into every test project, which checks the fixture signatures by
    reflection. Belt and braces, because the guard runs in Visual Studio and
    on a developer's machine as well as in CI.
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet"
)

# Every assembly holding tests. 'Tests.Web' on its own is the shared base
# library and holds no tests, so it is deliberately not matched.
$AssemblyFilter =
    ".*Tests\.(Cloud|OnPremise|Web\.Cloud|Web\.OnPremise).*\.dll$"

# Lines the MSTest adapter writes when it cannot discover a class. Any of
# them means tests exist that did not run.
$DiscoveryFailurePatterns = @(
    "failed to discover tests in class",
    "has wrong signature",
    "An exception occurred while test discoverer",
    "Exception occurred while enumerating"
)

$LogDirectory = [IO.Path]::Combine($pwd, $RepoName, "test-results")
if ((Test-Path $LogDirectory) -eq $false) {
    New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
}
$LogPath = [IO.Path]::Combine($LogDirectory, "unit-test-console-$Name.log")

# The runner is allowed to fail without hiding the discovery check, so its
# error is held and rethrown after the log has been searched.
$TestError = $null
$TestExitCode = 0
try {
    # *>&1 merges the warning and error streams into the output so that
    # everything the adapter printed reaches the log as well as the console.
    ./dotnet/run-unit-tests.ps1 `
        -RepoName $RepoName `
        -ProjectDir $ProjectDir `
        -Name $Name `
        -Configuration $Configuration `
        -Arch $Arch `
        -BuildMethod $BuildMethod `
        -Filter $AssemblyFilter *>&1 |
        Tee-Object -FilePath $LogPath
    $TestExitCode = $LASTEXITCODE
}
catch {
    $TestError = $_
}

if ($null -eq $TestExitCode) {
    $TestExitCode = 0
}

$DiscoveryFailures = @()
if (Test-Path $LogPath) {
    $DiscoveryFailures = @(
        Select-String `
            -Path $LogPath `
            -Pattern $DiscoveryFailurePatterns `
            -SimpleMatch)
}

if ($DiscoveryFailures.Count -gt 0) {
    Write-Output ""
    Write-Output ("=" * 78)
    Write-Output "TEST DISCOVERY FAILED"
    Write-Output ""
    Write-Output (
        "The test adapter could not discover one or more classes. Every " +
        "test in those classes was skipped, and without this check the " +
        "run would have reported success. Correct the signatures, then " +
        "run again.")
    Write-Output ""
    foreach ($failure in $DiscoveryFailures) {
        Write-Output "  $($failure.Line.Trim())"
    }
    Write-Output ("=" * 78)
    Write-Output ""
    Write-Error (
        "$($DiscoveryFailures.Count) test discovery error(s) were " +
        "reported. See the lines above and $LogPath.")
    exit 1
}

# A skipped test is not a passing test. The console logger prints the word
# "Skipped" and nothing else, so the reason, which is the only part anybody
# can act on, is invisible in a build log. The reasons are read back out of
# the trx files and printed. The build is not failed on a skip, because a
# browser that is genuinely absent on a runner is a fair reason to skip, but
# nobody can now say they were not told.
$ResultsRoot = [IO.Path]::Combine($pwd, $RepoName, "test-results")
if (Test-Path $ResultsRoot) {
    $Skipped = [System.Collections.Generic.List[string]]::new()
    foreach ($trx in Get-ChildItem -Path $ResultsRoot -Recurse -Filter *.trx) {
        try {
            [xml]$doc = Get-Content -Raw -Path $trx.FullName
        }
        catch {
            Write-Output (
                "Could not read '$($trx.Name)': " +
                $_.Exception.Message)
            continue
        }
        foreach ($result in $doc.TestRun.Results.UnitTestResult) {
            if ($result.outcome -ne "NotExecuted") { continue }
            $reason = $result.Output.ErrorInfo.Message
            if ([string]::IsNullOrWhiteSpace($reason)) {
                $reason = "no reason was recorded"
            }
            $Skipped.Add("$($result.testName): $($reason.Trim())")
        }
    }

    if ($Skipped.Count -gt 0) {
        Write-Output ""
        Write-Output ("-" * 78)
        Write-Output "$($Skipped.Count) TEST(S) WERE SKIPPED AND DID NOT RUN"
        Write-Output ""
        foreach ($entry in ($Skipped | Sort-Object -Unique)) {
            Write-Output "  $entry"
        }
        Write-Output ("-" * 78)
        Write-Output ""
    }
}

if ($null -ne $TestError) {
    throw $TestError
}

exit $TestExitCode