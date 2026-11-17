$ErrorActionPreference = 'Stop'

$packageDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $packageDir
$assetsDir = Join-Path $projectRoot 'Assets'
$payloadDir = Join-Path $packageDir 'Payload'

if (!(Test-Path $assetsDir)) {
    Write-Host 'ERROR: No encontre la carpeta Assets al lado del paquete.' -ForegroundColor Red
    exit 1
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = Join-Path $projectRoot ("_Backup_FrankaVR_INPUT_" + $stamp)
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

function Find-ClassFile([string]$className) {
    $matches = @()
    Get-ChildItem -Path $assetsDir -Recurse -Filter *.cs -File | ForEach-Object {
        $hit = Select-String -Path $_.FullName -Pattern ("\bclass\s+" + [regex]::Escape($className) + "\b") -Quiet
        if ($hit) { $matches += $_.FullName }
    }
    return ,$matches
}

function Backup-And-Replace([string]$target, [string]$source) {
    $relative = $target.Substring($projectRoot.Length).TrimStart('\\','/')
    $backupTarget = Join-Path $backupDir $relative
    $backupParent = Split-Path -Parent $backupTarget
    New-Item -ItemType Directory -Force -Path $backupParent | Out-Null
    Copy-Item -LiteralPath $target -Destination $backupTarget -Force
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host ("Actualizado: " + $relative) -ForegroundColor Green
}

# 1) HybridTeleopInput: clase nueva. Si ya existe una copia, actualizarla; si no, crearla.
$hybridSource = Join-Path $payloadDir 'Input\HybridTeleopInput.cs'
$hybridMatches = @(Find-ClassFile 'HybridTeleopInput')
if ($hybridMatches.Count -gt 1) {
    Write-Host 'ERROR: hay mas de una clase HybridTeleopInput dentro de Assets:' -ForegroundColor Red
    $hybridMatches | ForEach-Object { Write-Host $_ }
    Write-Host 'No se modifico nada de esa clase. Elimina duplicados y vuelve a ejecutar.' -ForegroundColor Yellow
    exit 1
}
elseif ($hybridMatches.Count -eq 1) {
    Backup-And-Replace $hybridMatches[0] $hybridSource
}
else {
    $targetDir = Join-Path $assetsDir 'FrankaVR\Input'
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $target = Join-Path $targetDir 'HybridTeleopInput.cs'
    Copy-Item -LiteralPath $hybridSource -Destination $target -Force
    Write-Host 'Creado: Assets\FrankaVR\Input\HybridTeleopInput.cs' -ForegroundColor Green
}

# 2) Setup wizard: reemplazar la unica copia preservando su .meta. Si no existe, crear en Editor.
$wizardSource = Join-Path $payloadDir 'Editor\FrankaVRSetupWizard.cs'
$wizardMatches = @(Find-ClassFile 'FrankaVRSetupWizard')
if ($wizardMatches.Count -gt 1) {
    Write-Host 'ERROR: hay mas de una clase FrankaVRSetupWizard dentro de Assets:' -ForegroundColor Red
    $wizardMatches | ForEach-Object { Write-Host $_ }
    exit 1
}
elseif ($wizardMatches.Count -eq 1) {
    Backup-And-Replace $wizardMatches[0] $wizardSource
}
else {
    $targetDir = Join-Path $assetsDir 'FrankaVR\Editor'
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $target = Join-Path $targetDir 'FrankaVRSetupWizard.cs'
    Copy-Item -LiteralPath $wizardSource -Destination $target -Force
    Write-Host 'Creado: Assets\FrankaVR\Editor\FrankaVRSetupWizard.cs' -ForegroundColor Green
}

Write-Host ''
Write-Host 'REPARACION DE INPUT COMPLETA.' -ForegroundColor Cyan
Write-Host ('Backup: ' + $backupDir)
Write-Host 'Ahora abre Unity y ejecuta:'
Write-Host '  FrankaVR > 4 - Reparar control Teclado + Guante' -ForegroundColor Yellow
Write-Host 'Luego:'
Write-Host '  FrankaVR > 3 - Validar escena' -ForegroundColor Yellow
