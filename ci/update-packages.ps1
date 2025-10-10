
param(
    [string]$ProjectDir = ".",
    [string]$Name,
    [Parameter(Mandatory=$true)]
    [string]$RepoName
)

./dotnet/run-update-dependencies.ps1 `
    -IncludePrerelease `
    -RepoName $RepoName `
    -ProjectDir $ProjectDir `
    -Name $Name

exit $LASTEXITCODE
