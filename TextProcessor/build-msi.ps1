# Build a versioned self-contained win-x64 MSI for TextProcessor
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
$installerOutputDir = Join-Path $installerDir "bin\x64\Release"


function ConvertTo-ProductVersion([string]$value) {
    $parsed = $null
    if (-not [version]::TryParse($value, [ref]$parsed) -or
        $parsed.Build -lt 0 -or $parsed.Major -gt 255 -or $parsed.Minor -gt 255 -or
        $parsed.Build -gt 65535 -or $parsed.Revision -gt 0) {
        throw "Use MSI version major.minor.build[.0] (major/minor <= 255, build <= 65535)."
    }
    return [version]::new($parsed.Major, $parsed.Minor, $parsed.Build, 0).ToString()
}

function Get-NextVersion([string]$current) {
    $v = [version]$current
    # Windows Installer compares only the first three fields, never Revision.
    return ConvertTo-ProductVersion ([version]::new($v.Major, $v.Minor, $v.Build + 1, 0).ToString())
}

if ($Version) {
    $productVersion = ConvertTo-ProductVersion $Version.Trim()
} elseif (Test-Path $versionFile) {
    $current = (Get-Content $versionFile -Raw).Trim()
    $productVersion = if ($NoBump) { ConvertTo-ProductVersion $current } else { Get-NextVersion $current }
} else {
    $productVersion = "1.0.1.0"
}

$msiPath = Join-Path $msiOutDir "TextProcessorSetup-$productVersion.msi"
if (Test-Path $msiPath) { throw "Installer already exists: $msiPath" }


Write-Host "=== Product version: $productVersion ===" -ForegroundColor Cyan
Write-Host "    UpgradeCode: a7b3c9d1-4e2f-4a8b-9c6d-1e2f3a4b5c6d (fixed)" -ForegroundColor DarkGray

Write-Host "=== Cleaning previous build artifacts ===" -ForegroundColor Cyan
Push-Location $appDir
try {
    dotnet clean TextProcessor.csproj -c Release
    # The last restore may have been RID-less (tests/build); do not require win-x64 assets for clean.
    if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed ($LASTEXITCODE)" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
} finally { Pop-Location }

Write-Host "=== Publishing app (self-contained win-x64) ===" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force -Path $msiOutDir | Out-Null

Push-Location $appDir
try {
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
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
} finally { Pop-Location }

Write-Host "=== Building MSI (WiX) ===" -ForegroundColor Cyan
if (Test-Path (Join-Path $installerDir "obj")) {
    Remove-Item -Recurse -Force (Join-Path $installerDir "obj")
}

Push-Location $installerDir
try {
    dotnet build TextProcessor.Installer.wixproj -c Release -p:ProductVersion=$productVersion "-p:OutputPath=$installerOutputDir"
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed ($LASTEXITCODE)" }
} finally { Pop-Location }

# Use this build's explicit output path, not the first MSI from any configuration.
$builtMsi = Join-Path $installerOutputDir "TextProcessorSetup.msi"
if (-not (Test-Path $builtMsi)) { throw "MSI was not created: $builtMsi" }

Copy-Item $builtMsi $msiPath
# Commit the version only once the installer has been successfully produced.
Set-Content -Path $versionFile -Value $productVersion -NoNewline -Encoding utf8
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
