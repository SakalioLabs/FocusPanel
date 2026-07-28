[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [Parameter()]
    [string]$Source,

    [Parameter()]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if ([string]::IsNullOrWhiteSpace($Source)) {
    $Source = Join-Path $projectRoot 'artifacts\release\packages'
}
$Source = [IO.Path]::GetFullPath($Source)

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'FocusPanel.csproj')
    $Version = [string]$project.Project.PropertyGroup.Version
}

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Update package directory does not exist: $Source"
}

$fullPackage = Join-Path $Source "FocusPanel-$Version-full.nupkg"
$releaseManifest = Join-Path $Source 'releases.win.json'
if (-not (Test-Path -LiteralPath $fullPackage -PathType Leaf)) {
    throw "Full update package is missing: $fullPackage"
}
if (-not (Test-Path -LiteralPath $releaseManifest -PathType Leaf)) {
    throw "Velopack update manifest is missing: $releaseManifest"
}

if (-not (Test-Path -LiteralPath $Destination -PathType Container)) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
}
$Destination = (Resolve-Path -LiteralPath $Destination).Path

# Copy packages before manifests so clients cannot discover the new version
# until every file referenced by the manifest is already available.
$payloadNames = @(
    "FocusPanel-$Version-full.nupkg",
    "FocusPanel-$Version-delta.nupkg",
    'FocusPanel-win-Setup.exe',
    'FocusPanel-win-Portable.zip',
    'assets.win.json'
)
$manifestNames = @('RELEASES', 'releases.win.json')
$copied = [Collections.Generic.List[string]]::new()

foreach ($name in $payloadNames) {
    $sourcePath = Join-Path $Source $name
    if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $Destination $name) -Force
        $copied.Add($name)
    }
}

foreach ($name in $manifestNames) {
    $sourcePath = Join-Path $Source $name
    if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $Destination $name) -Force
        $copied.Add($name)
    }
}

Write-Host "FocusPanel $Version was published to: $Destination"
Write-Host "Copied $($copied.Count) files: $($copied -join ', ')"
Write-Host 'On each client, select the LAN update source and enter this shared folder or its HTTP URL.'
