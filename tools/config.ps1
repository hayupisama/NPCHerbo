# NPCHerbo - chemins de l'environnement de build.
# Dot-source ce fichier :  . "$PSScriptRoot\config.ps1"
#
# Les valeurs par defaut ci-dessous sont generiques. Pour votre installation, creez tools\config.local.ps1
# (ignore par git) a partir de tools\config.local.example.ps1, ou definissez les variables d'environnement
# NPCH_GAME_DIR / NPCH_MO2_DIR.

$ProjectRoot = Split-Path -Parent $PSScriptRoot

# --- valeurs par defaut
$GameDir = if ($env:NPCH_GAME_DIR) { $env:NPCH_GAME_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition' }
$ModpackDir = if ($env:NPCH_MO2_DIR) { $env:NPCH_MO2_DIR } else { '' }   # instance Mod Organizer 2 (dossier contenant mods\)
$MO2ModName = 'NPCHerbo'

# --- surcharge locale (non versionnee)
$localConfig = Join-Path $PSScriptRoot 'config.local.ps1'
if (Test-Path $localConfig) { . $localConfig }

# --- chemins derives (surchargeables dans config.local.ps1)
if (-not $MO2ModsDir)      { $MO2ModsDir = if ($ModpackDir) { Join-Path $ModpackDir 'mods' } else { '' } }
if (-not $PapyrusCompiler) { $PapyrusCompiler = Join-Path $GameDir 'Papyrus Compiler\PapyrusCompiler.exe' }
if (-not $VanillaSource)   { $VanillaSource = Join-Path $GameDir 'Data\Source\Scripts' }
if (-not $SkseSource)      { $SkseSource = Join-Path $GameDir 'Data\Scripts\Source' }
if (-not $PapyrusFlags)    { $PapyrusFlags = Join-Path $VanillaSource 'TESV_Papyrus_Flags.flg' }
if (-not $PapyrusUtilSrc)  { $PapyrusUtilSrc = Join-Path $MO2ModsDir 'PapyrusUtil SE - Modders Scripting Utility Functions\Scripts\Source' }
if (-not $Po3Source)       { $Po3Source = Join-Path $MO2ModsDir "powerofthree's Papyrus Extender\Source\scripts" }

$ModScriptSource = Join-Path $ProjectRoot 'scripts\Source'
$BuildDir        = Join-Path $ProjectRoot 'build'
$DistDir         = Join-Path $ProjectRoot 'dist\NPCHerbo'

# Ordre des imports : le premier gagne. SKSE doit passer AVANT les sources vanilla
# (il remplace Actor.psc, ObjectReference.psc, etc. par des versions etendues).
$PapyrusImports = @($ModScriptSource, $PapyrusUtilSrc, $Po3Source, $SkseSource, $VanillaSource)
