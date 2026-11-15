# Recupere le visage de Faelia exporte par le Creation Kit (Ctrl+F4) dans le dossier overwrite de MO2
# et le range dans assets\facegen (versionne, inclus par build.ps1 dans le mod).
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"

$overwrite = Join-Path $ModpackDir 'overwrite'
$assets = Join-Path $ProjectRoot 'assets\facegen'
$parts = @(
    'meshes\actors\character\FaceGenData\FaceGeom\NPCHerbo.esp',
    'textures\actors\character\FaceGenData\FaceTint\NPCHerbo.esp'
)

$found = 0
foreach ($rel in $parts) {
    $src = Join-Path $overwrite $rel
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $assets $rel
    New-Item -ItemType Directory -Force $dst | Out-Null
    foreach ($f in Get-ChildItem $src -File) {
        Move-Item $f.FullName (Join-Path $dst $f.Name) -Force
        Write-Host "importe : $rel\$($f.Name)" -ForegroundColor Green
        $found++
    }
}
if ($found -eq 0) {
    Write-Warning "Aucun fichier de visage trouve dans $overwrite. Dans le CK : selectionner NPCH_Faelia puis Ctrl+F4."
    exit 1
}
Write-Host "$found fichier(s) importe(s). Lancer ensuite : tools\build.ps1 -Deploy"
