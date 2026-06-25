# Скрипт создания сертификата для локальной подписи MSIX-пакета
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pfxPath = Join-Path $projectDir "TextProcessor_Dev.pfx"

if (Test-Path $pfxPath) {
    Write-Host "Сертификат уже существует: $pfxPath"
    exit 0
}

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=TextProcessor Dev" `
    -KeyUsage DigitalSignature `
    -FriendlyName "TextProcessor Dev Certificate" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pwd = ConvertTo-SecureString -String "dev" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pwd | Out-Null

Write-Host "Сертификат создан: $pfxPath"
Write-Host "Отпечаток: $($cert.Thumbprint)"
Write-Host ""
Write-Host "Для установки MSIX локально доверьте сертификату:"
Write-Host "  certutil -addstore TrustedPeople `"$pfxPath`""
