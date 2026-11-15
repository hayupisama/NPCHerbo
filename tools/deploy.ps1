# Copie dist\NPCHerbo vers le dossier mods de MO2 (remplace la version precedente du mod).
# Il faut ensuite cocher le mod "NPCHerbo" (panneau gauche) et le plugin NPCHerbo.esp (panneau droit) dans MO2.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"

if (-not (Test-Path "$DistDir\NPCHerbo.esp")) { throw "Rien a deployer : lancer d'abord tools\build.ps1" }
$target = Join-Path $MO2ModsDir $MO2ModName

if (Get-Process ModOrganizer -ErrorAction SilentlyContinue) {
    Write-Warning 'MO2 est ouvert : si le jeu tourne, fermez-le avant de deployer. Pensez a rafraichir MO2 (F5) apres.'
}

if (Test-Path $target) {
    # on ne supprime que ce que le build produit, pour ne pas perdre un meta.ini ou des fichiers ajoutes par MO2
    Remove-Item "$target\NPCHerbo.esp", "$target\SEQ\NPCHerbo.seq", "$target\Scripts\NPCH_*.pex", "$target\Scripts\TIF_NPCH_*.pex", "$target\Source\Scripts\NPCH_*.psc", "$target\Source\Scripts\TIF_NPCH_*.psc" -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item "$DistDir\*" $target -Recurse -Force
Write-Host "Deploye dans $target" -ForegroundColor Green
