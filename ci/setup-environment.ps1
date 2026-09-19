param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [hashtable]$Keys
)
$RepoPath = [IO.Path]::Combine($pwd, $RepoName)

if (!$Configuration.Contains("Core")) {

    # Setup the MSBuild environment if it is required.
    ./environments/setup-msbuild.ps1
    ./environments/setup-vstest.ps1
}

if ($IsLinux) {
    # Shared, because the same call in five repositories carried the same
    # fault: these packages do not exist on arm64, so asking for them on
    # an ARM runner fails the step. See common-ci environments/README.md.
    ./environments/setup-multilib.ps1

}

dotnet dev-certs https

$env:_51DEGREES_DD_PATH = [IO.Path]::Combine($RepoPath, "device-detection-data", "TAC-HashV41.hash")
$env:_51DEGREES_RESOURCE_KEY = $Keys.TestResourceKey
$env:SUPER_RESOURCE_KEY = $Keys.TestResourceKey
$env:RESOURCE_KEY_CLOUD_V5_BESPOKE = $Keys.TestResourceKey
$env:DEVICEDETECTIONLICENSEKEY_DOTNET = $Keys.DeviceDetection
$env:ACCEPTCH_BROWSER_KEY = $Keys.AcceptCHBrowserKey
$env:ACCEPTCH_HARDWARE_KEY = $Keys.AcceptCHHardwareKey
$env:ACCEPTCH_PLATFORM_KEY = $Keys.AcceptCHPlatformKey
$env:ACCEPTCH_NONE_KEY = $Keys.AcceptCHNoneKey

$env:SE_SKIP_DRIVER_IN_PATH = "true"
