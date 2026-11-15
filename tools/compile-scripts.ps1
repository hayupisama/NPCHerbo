# Compile les scripts Papyrus du mod (scripts\Source\*.psc) vers build\Scripts\*.pex
param(
    [string]$Only  # optionnel : nom d'un seul script (sans extension)
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"

foreach ($p in @($PapyrusCompiler, $PapyrusFlags) + $PapyrusImports) {
    if (-not (Test-Path $p)) { throw "Chemin introuvable : $p (voir tools\config.ps1)" }
}

$out = Join-Path $BuildDir 'Scripts'
New-Item -ItemType Directory -Force $out | Out-Null
$imports = ($PapyrusImports -join ';')

if ($Only) {
    $targets = @(Join-Path $ModScriptSource "$Only.psc")
} else {
    $targets = @(Get-ChildItem $ModScriptSource -Filter *.psc | Select-Object -ExpandProperty FullName)
}
if ($targets.Count -eq 0) { Write-Host 'Aucun script a compiler.'; exit 0 }

$failed = @()
foreach ($t in $targets) {
    $name = [IO.Path]::GetFileName($t)
    $ErrorActionPreference = 'Continue'   # le compilateur ecrit ses erreurs sur stderr
    $log = & $PapyrusCompiler $t "-f=$PapyrusFlags" "-i=$imports" "-o=$out" -quiet 2>&1 | ForEach-Object { "$_" }
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) {
        $failed += $name
        Write-Host "ECHEC  $name" -ForegroundColor Red
        $log | ForEach-Object { Write-Host "   $_" }
    } else {
        Write-Host "OK     $name" -ForegroundColor Green
    }
}

if ($failed.Count -gt 0) { Write-Host "$($failed.Count) script(s) en echec." -ForegroundColor Red; exit 1 }
Write-Host "$($targets.Count) script(s) compile(s) vers $out"
