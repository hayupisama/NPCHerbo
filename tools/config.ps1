# NPCHerbo - chemins de l'environnement de build.
# Dot-source ce fichier :  . "$PSScriptRoot\config.ps1"

$ProjectRoot = Split-Path -Parent $PSScriptRoot

$GameDir     = 'C:\SteamLibrary\steamapps\common\Skyrim Special Edition'
$ModpackDir  = 'C:\Skyrim Thana Khan Modspack v8.4'
$MO2ModsDir  = Join-Path $ModpackDir 'mods'
$MO2ModName  = 'NPCHerbo'

$PapyrusCompiler = Join-Path $GameDir 'Papyrus Compiler\PapyrusCompiler.exe'
$VanillaSource   = Join-Path $GameDir 'Data\Source\Scripts'
$SkseSource      = Join-Path $GameDir 'Data\Scripts\Source'
$PapyrusFlags    = Join-Path $VanillaSource 'TESV_Papyrus_Flags.flg'
$PapyrusUtilSrc  = Join-Path $MO2ModsDir 'PapyrusUtil SE - Modders Scripting Utility Functions\Scripts\Source'
$Po3Source       = Join-Path $MO2ModsDir "powerofthree's Papyrus Extender\Source\scripts"

$ModScriptSource = Join-Path $ProjectRoot 'scripts\Source'
$BuildDir        = Join-Path $ProjectRoot 'build'
$DistDir         = Join-Path $ProjectRoot 'dist\NPCHerbo'

# Ordre des imports : le premier gagne. SKSE doit passer AVANT les sources vanilla
# (il remplace Actor.psc, ObjectReference.psc, etc. par des versions étendues).
$PapyrusImports = @($ModScriptSource, $PapyrusUtilSrc, $Po3Source, $SkseSource, $VanillaSource)
