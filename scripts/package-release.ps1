[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.10.62',

    [Parameter()]
    [string]$Dotnet8Path,

    [Parameter()]
    [string]$PublishDotnetPath = 'dotnet',

    [Parameter()]
    [string]$SignParams,

    [Parameter()]
    [switch]$CleanPackages,

    [Parameter()]
    [switch]$ReplaceCurrentVersion
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishDir = [IO.Path]::GetFullPath(
    (Join-Path $projectRoot "artifacts\release\publish\win-x64\$Version"))
$packageDir = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\release\packages'))
$manifest = Join-Path $projectRoot '.config\dotnet-tools.json'
$releaseNotesSource = Join-Path $projectRoot 'packaging\release-notes.md'
$releaseNotes = Join-Path $projectRoot 'artifacts\release\release-notes-unicode.md'
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

function Remove-GeneratedFile([string]$Path) {
    Assert-WorkspacePath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
}

if ($CleanPackages -and $ReplaceCurrentVersion) {
    throw 'CleanPackages and ReplaceCurrentVersion cannot be used together.'
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
elseif ($ReplaceCurrentVersion) {
    $replaceTargets = @(
        "FocusPanel-$Version-full.nupkg",
        "FocusPanel-$Version-delta.nupkg",
        'FocusPanel-win-Setup.exe',
        'FocusPanel-win.msi',
        'FocusPanel-win-Portable.zip',
        'assets.win.json',
        'RELEASES',
        'releases.win.json'
    )
    foreach ($target in $replaceTargets) {
        Remove-GeneratedFile (
            Join-Path $packageDir $target)
    }
}

# 0.10.27 folds the location picker into the primary Setup.exe.
# Remove the legacy unversioned launcher so it cannot leak into a
# later GitHub Release from a reused package directory.
Remove-GeneratedFile (
    Join-Path $packageDir 'FocusPanel-win-CustomSetup.exe')

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$unicodeWithBom = New-Object System.Text.UnicodeEncoding($false, $true)
$releaseNotesText = [IO.File]::ReadAllText(
    $releaseNotesSource,
    $utf8NoBom)
$expectedReleaseHeading = "# FocusPanel $Version"
$actualReleaseHeading = (
    $releaseNotesText -split "`r?`n",
    2
)[0].Trim()
if ($actualReleaseHeading -cne $expectedReleaseHeading) {
    throw "Release notes heading is '$actualReleaseHeading', expected '$expectedReleaseHeading'."
}
[IO.File]::WriteAllText(
    $releaseNotes,
    $releaseNotesText,
    $unicodeWithBom)

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
    '--shortcuts', 'Desktop,StartMenuRoot',
    '--msi', 'true',
    '--instLocation', 'Either'
)
if (-not [string]::IsNullOrWhiteSpace($SignParams)) {
    $vpkArguments += @('--signParams', $SignParams)
}

& $Dotnet8Path @vpkArguments
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$releaseManifest = Join-Path $packageDir 'releases.win.json'
if (-not (Test-Path -LiteralPath $releaseManifest)) {
    throw 'Packaging completed without producing releases.win.json.'
}

$manifestData = Get-Content -LiteralPath $releaseManifest -Raw -Encoding UTF8 |
    ConvertFrom-Json
$currentFullAsset = $manifestData.Assets |
    Where-Object { $_.Version -eq $Version -and $_.Type -eq 'Full' } |
    Select-Object -First 1
if ($null -eq $currentFullAsset) {
    throw "Release manifest does not contain the full package for $Version."
}

$normalizeNewlines = {
    param([string]$Text)
    return ($Text -replace "`r`n?", "`n").TrimEnd()
}
$expectedNotes = & $normalizeNewlines $releaseNotesText
$manifestNotes = & $normalizeNewlines $currentFullAsset.NotesMarkdown
if ($manifestNotes -cne $expectedNotes) {
    throw 'Release notes changed while packaging. Refusing to publish a manifest with corrupted text.'
}

$setupPath = Join-Path $packageDir 'FocusPanel-win-Setup.exe'
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw 'Packaging completed without producing Setup.exe.'
}
$setup = Get-Item -LiteralPath $setupPath
$msi = Get-ChildItem -LiteralPath $packageDir -Filter '*.msi' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $msi) {
    throw 'Packaging completed without producing the MSI installer.'
}

$customInstallerSource = Join-Path $projectRoot 'packaging\CustomInstallerLauncher.cs'
$installerLocationPolicySource = Join-Path $projectRoot 'Services\InstallerLocationPolicy.cs'
$nativeSetup = Join-Path $packageDir 'FocusPanel-win-NativeSetup.exe'
$customInstaller = Join-Path $packageDir 'FocusPanel-win-Setup.exe'
$frameworkCsc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $frameworkCsc)) {
    $frameworkCsc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $frameworkCsc)) {
    throw 'The Windows .NET Framework compiler required for the install-location picker was not found.'
}

Write-Host 'Creating the custom install-location launcher...'
Move-Item -LiteralPath $setup.FullName -Destination $nativeSetup -Force
& $frameworkCsc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    "/resource:$($msi.FullName),FocusPanelMsi" `
    "/out:$customInstaller" `
    $customInstallerSource `
    $installerLocationPolicySource
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $customInstaller)) {
    throw "Custom installer compilation failed with exit code $LASTEXITCODE."
}

$setupProbe = Start-Process `
    -FilePath $customInstaller `
    -ArgumentList '--verify-install-location-picker' `
    -Wait `
    -PassThru
if ($setupProbe.ExitCode -ne 42) {
    throw "Setup.exe install-location probe returned $($setupProbe.ExitCode), expected 42."
}
Remove-GeneratedFile $nativeSetup

Write-Host ''
Write-Host "Installer with install-location picker created: $customInstaller"
Write-Host "MSI installer created: $($msi.FullName)"
Get-ChildItem -LiteralPath $packageDir |
    Sort-Object Name |
    Select-Object Name, Length, LastWriteTime
