param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName = "device-detection-dotnet-examples",
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "msbuild"
)

if ($BuildMethod -eq "dotnet"){
    Write-Output "$Configuration"
    $Projects = @( 
    ".\Examples\ExampleBase\FiftyOne.DeviceDetection.Examples.csproj",
    ".\Examples\Legacy Web\Cloud-UACH\Cloud - Client-Hints.csproj",
    ".\Examples\Legacy Web\Cloud-UACH-manual\Cloud - Client-Hints Not Integrated.csproj",
    ".\Examples\Legacy Web\UACH\Client-Hints.csproj",
    ".\Examples\Legacy Web\UACH-manual\Client-Hints Not Integrated.csproj",
    ".\Examples\Cloud\Configurator-Console\Configurator-Console.csproj",
    ".\Examples\Cloud\GetAllProperties-Console\GetAllProperties.csproj",
    ".\Examples\Cloud\GettingStarted-Console\GettingStarted-Console.csproj",
    ".\Examples\Cloud\GettingStarted-Web\GettingStarted-Web.csproj",
    ".\Examples\Cloud\Metadata-Console\Metadata-Console.csproj",
    ".\Examples\Cloud\NativeModel-Console\NativeModelLookup-Console.csproj",
    ".\Examples\Cloud\TAC-Console\TacLookup-Console.csproj",
    ".\Examples\OnPremise\GettingStarted-Console\GettingStarted-Console.csproj",
    ".\Examples\OnPremise\GettingStarted-Web\GettingStarted-Web.csproj",
    ".\Examples\OnPremise\MatchMetrics-Console\MatchMetrics-Console.csproj",
    ".\Examples\OnPremise\Metadata-Console\Metadata-Console.csproj",
    ".\Examples\OnPremise\OfflineProcessing-Console\OfflineProcessing-Console.csproj",
    ".\Examples\OnPremise\Performance-Console\Performance-Console.csproj",
    ".\Examples\OnPremise\UpdateDataFile-Console\UpdateDataFile-Console.csproj",
    ".\Tests\FiftyOne.DeviceDetection.Example.Tests.Web\FiftyOne.DeviceDetection.Example.Tests.Web.csproj"
    )

    foreach($Project in $Projects){
        ./dotnet/build-project-core.ps1 -RepoName $RepoName -ProjectDir $Project -Name $Name -Configuration $Configuration -Arch $Arch
    }


}
else{

    ./dotnet/build-project-framework.ps1 -RepoName $RepoName -ProjectDir $ProjectDir -Name $Name -Configuration $Configuration -Arch $Arch
}

exit $LASTEXITCODE
