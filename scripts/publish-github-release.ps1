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

$repositoryOwner = 'SakalioLabs'
$repositoryName = 'FocusPanel'
$repositoryUrl = "https://github.com/$repositoryOwner/$repositoryName"
$apiBaseUrl = "https://api.github.com/repos/$repositoryOwner/$repositoryName"
$releaseTag = "v$Version"
$githubHeaders = @{
    Accept = 'application/vnd.github+json'
    Authorization = "Bearer $Token"
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'FocusPanel-Release-Publisher'
}
$existingRelease = $null
if ($Publish) {
    try {
        $existingRelease = Invoke-RestMethod `
            -Method Get `
            -Uri "$apiBaseUrl/releases/tags/$releaseTag" `
            -Headers $githubHeaders
    }
    catch {
        $statusCode = if ($null -ne $_.Exception.Response) {
            [int]$_.Exception.Response.StatusCode
        }
        else {
            0
        }
        if ($statusCode -ne 404) {
            throw
        }
    }
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
    '--repoUrl', $repositoryUrl,
    '--token', $Token,
    '--tag', $releaseTag,
    '--releaseName', "FocusPanel v$Version"
)
if ($Publish) {
    $arguments += @('--publish', 'true')
}

if ($null -eq $existingRelease) {
    & $Dotnet8Path @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub Release upload failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host "Release $releaseTag already exists; repairing and verifying its assets."
}

if ($Publish) {
    $release = if ($null -ne $existingRelease) {
        $existingRelease
    }
    else {
        Invoke-RestMethod `
            -Method Get `
            -Uri "$apiBaseUrl/releases/tags/$releaseTag" `
            -Headers $githubHeaders
    }

    $installerName = 'FocusPanel-win-Setup.exe'
    $installerPath = Join-Path $packageDir $installerName
    if (-not (Test-Path -LiteralPath $installerPath)) {
        throw "Installer '$installerName' is missing from the package directory."
    }

    $existingInstaller = $release.assets |
        Where-Object { $_.name -eq $installerName } |
        Select-Object -First 1
    $installerLength = (Get-Item -LiteralPath $installerPath).Length
    if ($null -ne $existingInstaller -and
        $existingInstaller.size -ne $installerLength) {
        Invoke-RestMethod `
            -Method Delete `
            -Uri "$apiBaseUrl/releases/assets/$($existingInstaller.id)" `
            -Headers $githubHeaders | Out-Null
        $existingInstaller = $null
    }

    if ($null -eq $existingInstaller) {
        Write-Host "Uploading $installerName..."
        $escapedAssetName = [Uri]::EscapeDataString($installerName)
        Invoke-RestMethod `
            -Method Post `
            -Uri "https://uploads.github.com/repos/$repositoryOwner/$repositoryName/releases/$($release.id)/assets?name=$escapedAssetName" `
            -Headers $githubHeaders `
            -ContentType 'application/octet-stream' `
            -InFile $installerPath | Out-Null
    }

    Write-Host "Marking $releaseTag as the latest GitHub Release..."
    $latestBody = @{
        draft = $false
        prerelease = $false
        make_latest = 'true'
    } | ConvertTo-Json
    Invoke-RestMethod `
        -Method Patch `
        -Uri "$apiBaseUrl/releases/$($release.id)" `
        -Headers $githubHeaders `
        -ContentType 'application/json' `
        -Body $latestBody | Out-Null

    $latest = Invoke-RestMethod `
        -Method Get `
        -Uri "$apiBaseUrl/releases/latest" `
        -Headers $githubHeaders
    if ($latest.tag_name -ne $releaseTag) {
        throw "GitHub latest release is '$($latest.tag_name)', expected '$releaseTag'."
    }

    $assetNames = @($latest.assets | ForEach-Object { $_.name })
    $requiredAssets = @(
        'releases.win.json',
        'RELEASES'
    )
    foreach ($requiredAsset in $requiredAssets) {
        if ($assetNames -notcontains $requiredAsset) {
            throw "Latest release '$releaseTag' is missing required update asset '$requiredAsset'."
        }
    }

    $hasFullPackage = $assetNames |
        Where-Object { $_ -match "^FocusPanel-$([regex]::Escape($Version))-full\.nupkg$" } |
        Select-Object -First 1
    if (-not $hasFullPackage) {
        throw "Latest release '$releaseTag' is missing the full Velopack package."
    }
    if ($assetNames -notcontains $installerName) {
        throw "Latest release '$releaseTag' is missing the install-location picker."
    }
    if ($assetNames -notcontains 'FocusPanel-win.msi') {
        throw "Latest release '$releaseTag' is missing the MSI installer."
    }

    Write-Host "FocusPanel $releaseTag is published, marked latest, and contains a valid update feed."
}
else {
    Write-Host "FocusPanel $releaseTag has been uploaded as a draft GitHub Release."
}
