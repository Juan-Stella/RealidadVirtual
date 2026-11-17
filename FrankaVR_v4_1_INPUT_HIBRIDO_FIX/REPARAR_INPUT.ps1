$ErrorActionPreference = 'Stop'

$packageDir = [System.IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $packageDir))
$assetsDir = Join-Path $projectRoot 'Assets'
$payloadDir = Join-Path $packageDir 'Payload'

Write-Host 'FrankaVR v4.1 - Reparacion de input hibrido' -ForegroundColor Cyan
Write-Host ('Proyecto detectado: ' + $projectRoot)
Write-Host ''

if (!(Test-Path -LiteralPath $assetsDir -PathType Container)) {
    Write-Host 'ERROR: No encontre la carpeta Assets al lado del paquete.' -ForegroundColor Red
    Write-Host ('Esperaba encontrar: ' + $assetsDir) -ForegroundColor Yellow
    exit 1
}

if (!(Test-Path -LiteralPath $payloadDir -PathType Container)) {
    Write-Host 'ERROR: Falta la carpeta Payload del reparador.' -ForegroundColor Red
    exit 1
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = Join-Path $projectRoot ("_Backup_FrankaVR_INPUT_" + $stamp)
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

function Find-ClassFiles([string]$className) {
    # IMPORTANTE: devolver los strings normalmente. No usar `return ,$matches`,
    # porque eso crea un array anidado y una unica coincidencia puede terminar
    # convertida a "System.Object[]" al pasarla a otra funcion.
    $found = New-Object System.Collections.Generic.List[string]

    Get-ChildItem -LiteralPath $assetsDir -Recurse -Filter '*.cs' -File | ForEach-Object {
        $pattern = '\bclass\s+' + [regex]::Escape($className) + '\b'
        if (Select-String -LiteralPath $_.FullName -Pattern $pattern -Quiet) {
            $found.Add([System.IO.Path]::GetFullPath($_.FullName))
        }
    }

    return $found.ToArray()
}

function Get-SafeRelativePath([string]$fullPath) {
    $targetFull = [System.IO.Path]::GetFullPath($fullPath)
    $rootFull = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\','/')
    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar

    if ($targetFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $targetFull.Substring($prefix.Length)
    }

    # Este caso no deberia ocurrir porque solo buscamos dentro de Assets,
    # pero evita que el reparador falle si alguna ruta llega de forma inesperada.
    return Join-Path '_FueraDelProyecto' ([System.IO.Path]::GetFileName($targetFull))
}

function Backup-And-Replace([string]$target, [string]$source) {
    if ([string]::IsNullOrWhiteSpace($target)) {
        throw 'La ruta target esta vacia.'
    }
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) {
        throw ('No existe el archivo destino: ' + $target)
    }
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
        throw ('No existe el archivo fuente del paquete: ' + $source)
    }

    $targetFull = [System.IO.Path]::GetFullPath($target)
    $sourceFull = [System.IO.Path]::GetFullPath($source)
    $relative = Get-SafeRelativePath $targetFull

    $backupTarget = Join-Path $backupDir $relative
    $backupParent = Split-Path -Parent $backupTarget
    if ($backupParent) {
        New-Item -ItemType Directory -Force -Path $backupParent | Out-Null
    }

    Copy-Item -LiteralPath $targetFull -Destination $backupTarget -Force
    Copy-Item -LiteralPath $sourceFull -Destination $targetFull -Force
    Write-Host ('Actualizado: ' + $relative) -ForegroundColor Green
}

function Install-Or-ReplaceClass(
    [string]$className,
    [string]$source,
    [string]$newRelativeTarget
) {
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
        throw ('Falta en Payload: ' + $source)
    }

    $matches = @(Find-ClassFiles $className)

    if ($matches.Count -gt 1) {
        Write-Host ("ERROR: hay mas de una clase $className dentro de Assets:") -ForegroundColor Red
        $matches | ForEach-Object { Write-Host ('  - ' + $_) }
        Write-Host 'No voy a elegir una automaticamente. Ejecuta primero el reparador de duplicados o elimina la copia sobrante.' -ForegroundColor Yellow
        exit 1
    }

    if ($matches.Count -eq 1) {
        Backup-And-Replace -target ([string]$matches[0]) -source $source
        return
    }

    $target = Join-Path $assetsDir $newRelativeTarget
    $targetParent = Split-Path -Parent $target
    New-Item -ItemType Directory -Force -Path $targetParent | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host ('Creado: Assets\' + $newRelativeTarget) -ForegroundColor Green
}

try {
    # 1) Input hibrido.
    Install-Or-ReplaceClass `
        -className 'HybridTeleopInput' `
        -source (Join-Path $payloadDir 'Input\HybridTeleopInput.cs') `
        -newRelativeTarget 'FrankaVR\Input\HybridTeleopInput.cs'

    # 2) Wizard del Editor con la opcion para volver a enlazar teclado + guante.
    Install-Or-ReplaceClass `
        -className 'FrankaVRSetupWizard' `
        -source (Join-Path $payloadDir 'Editor\FrankaVRSetupWizard.cs') `
        -newRelativeTarget 'FrankaVR\Editor\FrankaVRSetupWizard.cs'
}
catch {
    Write-Host ''
    Write-Host 'ERROR durante la reparacion:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ''
    Write-Host ('No borres el backup: ' + $backupDir) -ForegroundColor Yellow
    exit 1
}

Write-Host ''
Write-Host 'REPARACION DE INPUT COMPLETA.' -ForegroundColor Cyan
Write-Host ('Backup: ' + $backupDir)
Write-Host ''
Write-Host 'Ahora:'
Write-Host '  1. Abri Unity y espera que termine de compilar.'
Write-Host '  2. FrankaVR > 4 - Reparar control Teclado + Guante' -ForegroundColor Yellow
Write-Host '  3. FrankaVR > 3 - Validar escena' -ForegroundColor Yellow
Write-Host '  4. Play: sin BLE conectado debe quedar activo el teclado.' -ForegroundColor Yellow
