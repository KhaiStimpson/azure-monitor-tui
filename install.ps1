<#
.SYNOPSIS
    Installs az-monitor from the latest GitHub release.

.DESCRIPTION
    Downloads the latest az-monitor release for Windows, extracts it to
    %LOCALAPPDATA%\az-monitor, adds the bin directory to the user PATH,
    and creates a starter config at ~/.config/az-monitor/config.json
    if one does not already exist.

.PARAMETER Version
    Specific version tag to install (e.g. "v1.0.0"). Defaults to latest.

.EXAMPLE
    irm https://raw.githubusercontent.com/KhaiStimpson/azure-monitor-tui/master/install.ps1 | iex
#>
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repo = "KhaiStimpson/azure-monitor-tui"
$assetPattern = "az-monitor-win-x64.zip"
$installDir = Join-Path $env:LOCALAPPDATA "az-monitor"
$binDir = Join-Path $installDir "bin"
$configDir = Join-Path $HOME ".config" "az-monitor"
$configFile = Join-Path $configDir "config.json"

# ── Resolve version ──────────────────────────────────────────────────────────

if ($Version) {
    $tag = $Version
} else {
    Write-Host "Fetching latest release..."
    $release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
    $tag = $release.tag_name
}

Write-Host "Installing az-monitor $tag"

# ── Download ─────────────────────────────────────────────────────────────────

$downloadUrl = "https://github.com/$repo/releases/download/$tag/$assetPattern"
$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) "az-monitor-$tag.zip"

Write-Host "Downloading $downloadUrl ..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing

# ── Extract ──────────────────────────────────────────────────────────────────

if (Test-Path $binDir) {
    Write-Host "Removing previous installation..."
    Remove-Item -Recurse -Force $binDir
}

New-Item -ItemType Directory -Force -Path $binDir | Out-Null

Write-Host "Extracting to $binDir ..."
Expand-Archive -Path $tempZip -DestinationPath $binDir -Force
Remove-Item $tempZip

# ── Update PATH ──────────────────────────────────────────────────────────────

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")

if ($userPath -notlike "*$binDir*") {
    Write-Host "Adding $binDir to user PATH..."
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$binDir", "User")
    $env:Path = "$env:Path;$binDir"
    Write-Host "  PATH updated. You may need to restart your terminal."
} else {
    Write-Host "  $binDir is already in PATH."
}

# ── Starter config ───────────────────────────────────────────────────────────

if (-not (Test-Path $configFile)) {
    Write-Host "Creating starter config at $configFile ..."
    New-Item -ItemType Directory -Force -Path $configDir | Out-Null

    $starterConfig = @'
{
  "AzureStorage": {
    "ConnectionString": ""
  },
  "Monitor": {
    "PollIntervalSeconds": 10,
    "MaxDataPoints": 200,
    "ShowDebugErrors": false
  }
}
'@
    Set-Content -Path $configFile -Value $starterConfig -Encoding UTF8
    Write-Host "  Edit $configFile to set your Azure connection string."
} else {
    Write-Host "  Config already exists at $configFile"
}

# ── Done ─────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "az-monitor $tag installed successfully!"
Write-Host "Run 'az-monitor' to start the TUI."
