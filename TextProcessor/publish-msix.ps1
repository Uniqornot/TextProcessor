# Сборка и публикация MSIX-пакета «Обработчик текста»
#
# Требования (один раз):
#   1. .NET 8 SDK          — winget install Microsoft.DotNet.SDK.8
#   2. Windows 10/11 SDK   — winget install Microsoft.WindowsSDK.10.0.22621
#   3. Сертификат подписи  — .\create-dev-cert.ps1
#   4. Доверие сертификату — certutil -user -addstore TrustedPeople .\TextProcessor_Dev.pfx
#
# Публикация:
#   .\publish-msix.ps1

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

function Find-MakeAppx {
    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) { return $null }
    $latest = Get-ChildItem $kitsRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $latest) { return $null }
    $x64 = Join-Path $latest.FullName "x64\makeappx.exe"
    if (Test-Path $x64) { return $x64 }
    return $null
}

$pfx = Join-Path $projectDir "TextProcessor_Dev.pfx"
if (-not (Test-Path $pfx)) {
    Write-Host "Сертификат не найден. Сначала выполните:" -ForegroundColor Yellow
    Write-Host "  .\create-dev-cert.ps1"
    exit 1
}

$makeAppx = Find-MakeAppx
if (-not $makeAppx) {
    Write-Host "MakeAppx.exe не найден. Установите Windows SDK:" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.WindowsSDK.10.0.22621"
    exit 1
}

Write-Host "MakeAppx: $makeAppx" -ForegroundColor Cyan

dotnet publish -c Release `
    -p:PublishProfile=win-msix `
    -p:RuntimeIdentifier=win-x64 `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateKeyFile=TextProcessor_Dev.pfx `
    -p:PackageCertificatePassword=dev

$msix = Get-ChildItem -Path "bin\Release" -Filter "*.msix" -Recurse | Select-Object -First 1
if ($msix) {
    Write-Host ""
    Write-Host "MSIX готов:" -ForegroundColor Green
    Write-Host "  $($msix.FullName)"
    Write-Host ""
    Write-Host "Установка:"
    Write-Host "  Add-AppxPackage -Path `"$($msix.FullName)`""
} else {
    Write-Host "Файл .msix не найден. Убедитесь, что установлен Windows SDK и повторите publish." -ForegroundColor Red
    exit 1
}
