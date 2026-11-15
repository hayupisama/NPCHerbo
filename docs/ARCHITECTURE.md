# Architecture de NPCHerbo

Vue d'ensemble technique pour les contributeurs.

## Arborescence

```text
NPCHerbo/
├─ src/NPCHerbo.Generator/     Générateur du plugin (C#, .NET 8, Mutagen 0.53.1)
│   ├─ Program.cs              Points d'entrée : build, dump, inspect, dialogues, outils d'exploration de Skyrim.esm
│   ├─ PluginBuilder.cs        Cœur : PNJ, quête, alias, packages, cellule, pouvoir, besace, globales
│   ├─ Dialogues.cs            Dialogues v1 (besace, état, achat de sac)
│   ├─ Features.cs             Fonctionnalités v2 : menus de dialogue en arbre, réglages, MCM (config.json + traductions)
│   ├─ FormIdRegistry.cs       FormIDs stables par EditorID (formids.json)
│   ├─ Vanilla.cs              FormKeys vanilla utilisés (vérifiés par les outils d'exploration)
│   └─ Explore.cs              Diagnostic : dump réflexif, recherche de répliques doublées, packages Travel
├─ scripts/Source/             Scripts Papyrus
│   ├─ NPCH_MainQuestScript    État global, installation, migrations, appel/renvoi, téléportation, besace, catalogue
│   ├─ NPCH_FeaturesScript     Fonctionnalités v2 (savoir, filtres, météo, rares, dépôt, menu de réglages, texte FR)
│   ├─ NPCH_FaeliaAliasScript  Boucle de Faelia : téléportation, scan, planification et exécution des tournées
│   ├─ NPCH_BagScript          Besace : filtre ingrédients, plafond, compteur, paliers de remplissage
│   ├─ NPCH_PlayerAliasScript  Chargement de partie, voyage rapide, capture (méthode joueur)
│   ├─ NPCH_SummonEffectScript Pouvoir « Appel de Faelia »
│   └─ TIF_NPCH_Dialogue       Fragments de dialogue (une fonction par action)
├─ tools/                      Build PowerShell (config, compilation Papyrus, assemblage, déploiement MO2, facegen)
├─ formids.json                Registre des FormIDs (versionné, ne jamais renuméroter)
└─ docs/                       Conception d'origine, tests, architecture
```

## Chaîne de build

`tools/build.ps1` :

1. **Papyrus** : `PapyrusCompiler.exe` sur `scripts/Source/*.psc`. Ordre des imports : sources du mod → PapyrusUtil → po3 → SKSE → vanilla (les versions SKSE de `Actor.psc`, `ObjectReference.psc`… doivent primer sur les vanilla).
2. **Plugin** : `dotnet run -- build build/NPCHerbo.esp` lit `Skyrim.esm` (dossier donné par `NPCH_GAME_DATA`), génère le plugin, le fichier `SEQ` et le menu MCM.
3. **Assemblage** dans `dist/NPCHerbo/`, puis copie vers Mod Organizer 2 avec `-Deploy`.

## Plugin généré

- **ESL**, en-tête **1.70** (compatible 1.5.97), FormIDs dans la plage `0x800–0xFFF`, seul master : `Skyrim.esm`.
- **FormIDs stables** via `formids.json` : un enregistrement garde son FormID d'une version à l'autre, sinon les sauvegardes existantes se cassent. Les EditorID abandonnés restent réservés.
- **Pas de nouvelle CELL** (bug de persistance des ESL) : Faelia, sa besace et le point de retour sont placés dans une surcharge de la cellule de développement vanilla `AAADeleteWhenDoneTestJeremy`, inaccessible en jeu.
- **Copies d'enregistrements vanilla** nettoyées : PNJ dérivé d'un preset Bosmer, packages dérivés de packages vanilla (suivi, Travel vers alias), en retirant leurs scripts hérités.
- **SEQ** généré pour les quêtes « Start Game Enabled » qui ont des dialogues.

## Quête principale `NPCH_MainQuest`

Alias : `Player`, `Faelia` (forcé, désactivé hors appel), `BagChest`, `HarvestTarget` (cible courante du package de récolte), `DepositChest`.

Pile de packages de Faelia (la première dont les conditions passent gagne) :

| Package | Condition | Rôle |
|---|---|---|
| `NPCH_PkgHide` | joueur en combat | suit de loin (15-25 m), accroupie |
| `NPCH_PkgHarvest` | `NPCH_Harvesting == 1` | Travel vers l'alias `HarvestTarget` (course) |
| `NPCH_PkgFollow` | — | suit le joueur |

## Boucle de récolte (`NPCH_FaeliaAliasScript`)

```text
OnUpdate (toutes les 2 s, 4 s après une tournée)
 ├─ CheckTeleport        trop loin / autre cellule / autre espace → téléportation
 ├─ RunBatch
 │   ├─ ScanCandidates   UN scan centré sur le joueur, 4 passes :
 │   │                   plantes du catalogue, ingrédients au sol, Nirnroot, créatures mortes
 │   │                   filtres du moins cher au plus cher (visitées, propriétaire, distance,
 │   │                   dénivelé, préférences / récolte ciblée)
 │   ├─ BuildChain       chaîne du plus proche voisin, sans appel au moteur ;
 │   │                   une plante rare ouvre la tournée (détour)
 │   └─ HarvestOne ×N    le package reste actif, seule la cible change (ForceRefTo)
 │        ├─ attente d'arrivée avec détection de blocage (1,5 s sans progrès)
 │        │   → petit saut de côté + recalcul d'itinéraire, abandon après 3 essais
 │        └─ cueillette selon le type : Flora/Tree (Activate par Faelia), ingrédient au sol,
 │            Nirnroot (activateur scripté), créature morte (transfert d'inventaire)
 └─ météo, leçon du jour
```

- **Itinéraires** : calculés par le moteur (navmesh) quand le package Travel reçoit sa cible ; le script ne fait que détecter l'absence de progrès.
- **Garde de réentrance** : `loopGen` change à chaque `StartLoop`/`StopLoop` ; une tournée d'une génération périmée (pile reprise après un chargement) s'arrête d'elle-même. `runningGen` empêche deux tournées simultanées sans pouvoir rester bloqué.
- **Plantes visitées** : deux listes StorageUtil parallèles (référence, date), purge à 10 jours, plafond de 2 000.
- **Catalogue** : toutes les Flora/Tree qui donnent un ingrédient (listes nivelées vérifiées), reconstruit seulement si le nombre de formes du jeu change.

## Besace (`NPCH_BagScript`)

Coffre persistant hors du monde. `OnItemAdded` : refuse les non-ingrédients (rendus à l'expéditeur), plafonne (excédent rendu au joueur s'il déposait à la main, sinon recréé aux pieds de Faelia), tient `NPCH_BagCount`. Recompte complet à chaque chargement.

## Réglages et sauvegardes

- Tous les réglages sont des **globales** (`NPCH_*`), modifiables en console, dans le menu de réglages intégré (boîtes de dialogue `NPCH_MsgSettings1/2`) ou dans le MCM.
- Les valeurs des globales sont **conservées dans la sauvegarde** : changer une valeur par défaut exige une migration (`MigrateSettings`, `SETTINGS_VERSION`).

## Dialogues

- Branches de premier niveau (catégorie Player, TopLevel) ; sous-menus = topics de la même branche reliés par `LinkTo`.
- Fragments : script unique `TIF_NPCH_Dialogue`, une fonction par action (`Fragment_*`), appelée en fin de réplique.
- **Réactions vocales** : topics sans branche joués par `Say()`, dont les répliques pointent (*Shared Info*, `ResponseData`) vers des répliques vanilla doublées avec la voix `FemaleYoungEager`.

## Texte français

Les sources Papyrus sont compilées sans accents. Le texte affiché vient soit d'enregistrements `Message` (générés en C#, encodés en cp1252), soit de `Fr()` qui reconstruit les accents à l'exécution (`StringUtil.AsChar`) à partir de jetons (`{eac}` = é, `{egr}` = è, `{agr}` = à…).

## Outils de diagnostic

```powershell
cd src\NPCHerbo.Generator
dotnet run -- dump ..\..\build\NPCHerbo.esp              # liste des enregistrements
dotnet run -- inspect ..\..\build\NPCHerbo.esp NPCH_Faelia
dotnet run -- dialogues ..\..\build\NPCHerbo.esp         # branches, topics, conditions, fragments, liens
dotnet run -- find Nirn AlchRestore                      # recherche dans Skyrim.esm (noms FR)
```
