# Build complet de NPCHerbo :
#   1. compile les scripts Papyrus      -> build\Scripts\*.pex
#   2. genere le plugin (Mutagen)       -> build\NPCHerbo.esp
#   3. assemble le mod                  -> dist\NPCHerbo\
#   4. (option -Deploy) copie vers MO2  -> <modpack>\mods\NPCHerbo
param([switch]$Deploy)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"

Write-Host '== 1/3 Scripts Papyrus' -ForegroundColor Cyan
& "$PSScriptRoot\compile-scripts.ps1"
if ($LASTEXITCODE -ne 0) { throw 'Compilation Papyrus en echec.' }

Write-Host '== 2/3 Plugin' -ForegroundColor Cyan
$esp = Join-Path $BuildDir 'NPCHerbo.esp'
$gen = Join-Path $ProjectRoot 'src\NPCHerbo.Generator'
$ErrorActionPreference = 'Continue'
dotnet run --project $gen -v q -- build $esp 2>&1 | ForEach-Object { "$_" }
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) { throw 'Generation du plugin en echec.' }

Write-Host '== 3/3 Assemblage' -ForegroundColor Cyan
if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
New-Item -ItemType Directory -Force "$DistDir\Scripts", "$DistDir\Source\Scripts" | Out-Null
Copy-Item $esp $DistDir
Copy-Item "$BuildDir\SEQ" "$DistDir\SEQ" -Recurse
Copy-Item "$BuildDir\Scripts\NPCH_*.pex", "$BuildDir\Scripts\TIF_NPCH_*.pex" "$DistDir\Scripts"
Copy-Item "$ModScriptSource\NPCH_*.psc", "$ModScriptSource\TIF_NPCH_*.psc" "$DistDir\Source\Scripts"
$facegen = Join-Path $ProjectRoot 'assets\facegen'
if (Test-Path $facegen) {
    Copy-Item "$facegen\*" $DistDir -Recurse -Force   # meshes\... et textures\... du visage de Faelia
} else {
    # Apparence actuelle validee par l'utilisateur sans facegen. En cas de visage gris/sombre un jour : tools\import-facegen.ps1
    Write-Host 'Visage : non inclus (apparence calculee par le jeu, validee).' -ForegroundColor DarkGray
}
$n = (Get-ChildItem $DistDir -Recurse -File).Count
Write-Host "Mod assemble : $DistDir ($n fichiers)"

if ($Deploy) {
    & "$PSScriptRoot\deploy.ps1"
}
