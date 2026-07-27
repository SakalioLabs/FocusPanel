[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter()]
    [string]$Dotnet8Path = 'dotnet',

    [Parameter()]
    [string]$Token = $env:GITHUB_TOKEN,

    [Parameter()]
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Token)) {
    throw 'GitHub token is missing. Set GITHUB_TOKEN; this script never stores or prints it.'
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageDir = Join-Path $projectRoot 'artifacts\release\packages'
$manifest = Join-Path $projectRoot '.config\dotnet-tools.json'
if (-not (Test-Path -LiteralPath $packageDir)) {
    throw 'Release packages are missing. Run scripts\package-release.ps1 first.'
}

& $Dotnet8Path tool restore --tool-manifest $manifest
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed with exit code $LASTEXITCODE."
}

$arguments = @(
    'tool', 'run', 'vpk', '--',
    'upload', 'github',
    '--outputDir', $packageDir,
    '--channel', 'win',
    '--repoUrl', 'https://github.com/SakalioLabs/FocusPanel',
    '--token', $Token,
    '--tag', "v$Version",
    '--releaseName', "FocusPanel v$Version"
)
if ($Publish) {
    $arguments += @('--publish', 'true')
}

& $Dotnet8Path @arguments
if ($LASTEXITCODE -ne 0) {
    throw "GitHub Release upload failed with exit code $LASTEXITCODE."
}

if ($Publish) {
    Write-Host "FocusPanel v$Version has been published."
}
else {
    Write-Host "FocusPanel v$Version has been uploaded as a draft GitHub Release."
}
