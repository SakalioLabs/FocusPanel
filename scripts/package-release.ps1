[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.9.41',

    [Parameter()]
    [string]$Dotnet8Path,

    [Parameter()]
    [string]$PublishDotnetPath = 'dotnet',

    [Parameter()]
    [string]$SignParams,

    [Parameter()]
    [switch]$CleanPackages
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishDir = [IO.Path]::GetFullPath(
    (Join-Path $projectRoot "artifacts\release\publish\win-x64\$Version"))
$packageDir = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\release\packages'))
$manifest = Join-Path $projectRoot '.config\dotnet-tools.json'
$releaseNotes = Join-Path $projectRoot 'packaging\release-notes.md'
$projectFile = Join-Path $projectRoot 'FocusPanel.csproj'
$numericVersion = $Version.Split('-')[0]

function Assert-WorkspacePath([string]$Path) {
    $rootPrefix = $projectRoot.TrimEnd('\') + '\'
    if (-not $Path.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the workspace: $Path"
    }
}

function Reset-Directory([string]$Path) {
    Assert-WorkspacePath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

if ([string]::IsNullOrWhiteSpace($Dotnet8Path)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $Dotnet8Path = $dotnetCommand.Source
}

$sdkVersion = (& $Dotnet8Path --version).Trim()
if ([version]($sdkVersion.Split('-')[0]) -lt [version]'8.0.0') {
    throw 'Velopack 1.2.0 requires the .NET 8 SDK. FocusPanel itself still targets .NET 7. Pass a .NET 8 executable via -Dotnet8Path.'
}

Reset-Directory $publishDir
if ($CleanPackages) {
    Reset-Directory $packageDir
}
elseif (-not (Test-Path -LiteralPath $packageDir)) {
    New-Item -ItemType Directory -Path $packageDir | Out-Null
}

Write-Host "Publishing FocusPanel $Version (win-x64, self-contained)..."
& $PublishDotnetPath publish $projectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir `
    -p:Version=$Version `
    -p:AssemblyVersion="$numericVersion.0" `
    -p:FileVersion="$numericVersion.0" `
    -p:InformationalVersion=$Version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host 'Restoring the Velopack packaging tool...'
& $Dotnet8Path tool restore --tool-manifest $manifest
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed with exit code $LASTEXITCODE."
}

Write-Host 'Creating installer, full package, and delta package...'
$vpkArguments = @(
    'tool', 'run', 'vpk', '--',
    'pack',
    '--packId', 'FocusPanel',
    '--packVersion', $Version,
    '--packDir', $publishDir,
    '--mainExe', 'FocusPanel.exe',
    '--packTitle', 'FocusPanel',
    '--packAuthors', 'SakalioLabs',
    '--runtime', 'win-x64',
    '--channel', 'win',
    '--outputDir', $packageDir,
    '--releaseNotes', $releaseNotes,
    '--shortcuts', 'Desktop,StartMenuRoot'
)
if (-not [string]::IsNullOrWhiteSpace($SignParams)) {
    $vpkArguments += @('--signParams', $SignParams)
}

& $Dotnet8Path @vpkArguments
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$setup = Get-ChildItem -LiteralPath $packageDir -Filter '*Setup.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $setup) {
    throw 'Packaging completed without producing Setup.exe.'
}

Write-Host ''
Write-Host "Installer created: $($setup.FullName)"
Get-ChildItem -LiteralPath $packageDir |
    Sort-Object Name |
    Select-Object Name, Length, LastWriteTime
