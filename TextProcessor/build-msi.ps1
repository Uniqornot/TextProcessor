# Build MSI installer for TextProcessor (with version bump + upgrade support)
# Requires: .NET 8 SDK, WiX (dotnet tool install --global wix)
# Run from TextProcessor folder: .\build-msi.ps1
#
# Optional: .\build-msi.ps1 -Version 1.2.0.0
#           .\build-msi.ps1 -NoBump   (rebuild same version, dev only)

param(
    [string]$Version = "",
    [switch]$NoBump
)

$ErrorActionPreference = "Stop"
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $appDir
$installerDir = Join-Path $repoRoot "TextProcessor.Installer"
$versionFile = Join-Path $installerDir "VERSION"
$publishDir = Join-Path $installerDir "publish"
$msiOutDir = Join-Path $repoRoot "dist"
$msiPath = Join-Path $msiOutDir "TextProcessorSetup.msi"

function Get-NextVersion([string]$current) {
    $v = [version]$current
    return [version]::new($v.Major, $v.Minor, $v.Build, $v.Revision + 1).ToString()
}

if ($Version) {
    $productVersion = $Version.Trim()
} elseif (Test-Path $versionFile) {
    $current = (Get-Content $versionFile -Raw).Trim()
    $productVersion = if ($NoBump) { $current } else { Get-NextVersion $current }
} else {
    $productVersion = "1.0.0.1"
}

Set-Content -Path $versionFile -Value $productVersion -NoNewline -Encoding utf8

Write-Host "=== Product version: $productVersion ===" -ForegroundColor Cyan
Write-Host "    UpgradeCode: a7b3c9d1-4e2f-4a8b-9c6d-1e2f3a4b5c6d (fixed)" -ForegroundColor DarkGray

Write-Host "=== Cleaning previous build artifacts ===" -ForegroundColor Cyan
Push-Location $appDir
dotnet clean TextProcessor.csproj -c Release -r win-x64 | Out-Null
if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
Pop-Location

Write-Host "=== Publishing app (self-contained win-x64) ===" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force -Path $msiOutDir | Out-Null

Push-Location $appDir
dotnet publish TextProcessor.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:WindowsPackageType=None `
    -p:Version=$productVersion `
    -p:AssemblyVersion=$productVersion `
    -p:FileVersion=$productVersion `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

Write-Host "=== Building MSI (WiX) ===" -ForegroundColor Cyan
if (Test-Path (Join-Path $installerDir "obj")) {
    Remove-Item -Recurse -Force (Join-Path $installerDir "obj")
}

Push-Location $installerDir
dotnet build TextProcessor.Installer.wixproj -c Release -p:ProductVersion=$productVersion
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

$builtMsi = Get-ChildItem -Path (Join-Path $installerDir "bin") -Filter "TextProcessorSetup.msi" -Recurse | Select-Object -First 1
if (-not $builtMsi) {
    Write-Host "ERROR: MSI was not created." -ForegroundColor Red
    exit 1
}

Copy-Item -Force $builtMsi.FullName $msiPath
$sizeMb = [math]::Round((Get-Item $msiPath).Length / 1MB, 1)

Write-Host ""
Write-Host "MSI ready v$productVersion ($sizeMb MB):" -ForegroundColor Green
Write-Host "  $msiPath"
Write-Host ""
Write-Host "Install / upgrade:" -ForegroundColor Cyan
Write-Host "  msiexec /i `"$msiPath`""
Write-Host ""
Write-Host "If duplicate installs remain from old builds, uninstall them once in"
Write-Host "Settings -> Apps, then install this MSI — future builds will upgrade in place."
