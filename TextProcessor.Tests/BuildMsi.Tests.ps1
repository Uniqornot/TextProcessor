$ErrorActionPreference='Stop'
$path=Join-Path (Split-Path $PSScriptRoot -Parent) 'TextProcessor\build-msi.ps1'
$tokens=$null; $errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$functions=$ast.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true)
foreach ($f in $functions) { Invoke-Expression $f.Extent.Text }
$checks=0
function Check($actual,$expected) {
    if ($actual -ne $expected) { throw "Expected $expected, got $actual" }
    $script:checks++
}
Check (Get-NextVersion '1.0.1.0') '1.0.2.0'
Check (Get-NextVersion '1.0.0.6') '1.0.1.0'
Check (ConvertTo-ProductVersion '1.2.3') '1.2.3.0'
Check (ConvertTo-ProductVersion '255.255.65535.0') '255.255.65535.0'
foreach ($v in @('1.0','1.0.1.9','256.1.1.0','1.256.1.0','1.1.65536.0','../bad','foo')) {
    $failed=$false
    try { $null=ConvertTo-ProductVersion $v } catch { $failed=$true }
    Check $failed $true
}
$failed=$false
try { $null=Get-NextVersion '1.1.65535.0' } catch { $failed=$true }
Check $failed $true


# Exercise the actual orchestration with an isolated fake dotnet, never a real build.
$root=Join-Path ([IO.Path]::GetTempPath()) ('TextProcessor-build-test-' + [guid]::NewGuid().ToString('N'))
$startLocation=(Get-Location).Path
try {
    New-Item -ItemType Directory -Path "$root\TextProcessor","$root\TextProcessor.Installer","$root\dist" | Out-Null
    Copy-Item $path "$root\TextProcessor\build-msi.ps1"
    $testScript="$root\TextProcessor\build-msi.ps1"
    $versionFile="$root\TextProcessor.Installer\VERSION"
    $global:TextProcessorTestFailStage=''
    function dotnet {
        $stage=$args[0]
        $global:LASTEXITCODE=0
        if ($stage -eq $global:TextProcessorTestFailStage) { $global:LASTEXITCODE=1; return }
        if ($stage -eq 'build') {
            $outputArg=$args | Where-Object { $_ -like '-p:OutputPath=*' }
            $dest=$outputArg.Substring('-p:OutputPath='.Length)
            New-Item -ItemType Directory -Force -Path $dest | Out-Null
            Set-Content (Join-Path $dest 'TextProcessorSetup.msi') 'new-test-package'
        }
    }
    Set-Content $versionFile '1.0.1.0'
    Set-Content "$root\dist\TextProcessorSetup.msi" 'old-test-package'
    # A stale file from another configuration must never be selected.
    New-Item -ItemType Directory -Path "$root\TextProcessor.Installer\bin\Debug" | Out-Null
    Set-Content "$root\TextProcessor.Installer\bin\Debug\TextProcessorSetup.msi" 'stale-test-package'
    & $testScript | Out-Null
    Check ((Get-Content $versionFile -Raw).Trim()) '1.0.2.0'
    Check ((Get-Content "$root\dist\TextProcessorSetup-1.0.2.0.msi" -Raw).Trim()) 'new-test-package'
    Check ((Get-Content "$root\dist\TextProcessorSetup.msi" -Raw).Trim()) 'old-test-package'
    foreach ($stage in @('clean','publish','build')) {
        $global:TextProcessorTestFailStage=$stage
        $failed=$false
        try { & $testScript | Out-Null } catch { $failed=$true }
        Check $failed $true
        Check ((Get-Content $versionFile -Raw).Trim()) '1.0.2.0'
        Check (Test-Path "$root\dist\TextProcessorSetup-1.0.3.0.msi") $false
        Check (Get-Location).Path $startLocation
    }
    $failed=$false
    try { & $testScript -NoBump | Out-Null } catch { $failed=$true }
    Check $failed $true
    Check ((Get-Content "$root\dist\TextProcessorSetup-1.0.2.0.msi" -Raw).Trim()) 'new-test-package'
} finally {
    Set-Location $startLocation
    Remove-Item function:dotnet -ErrorAction SilentlyContinue
    Remove-Variable TextProcessorTestFailStage -Scope Global -ErrorAction SilentlyContinue
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Output "Build script checks: $checks passed"
