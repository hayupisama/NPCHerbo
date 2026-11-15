# NPCHerbo — Faelia, l'herboriste qui te suit

Mod pour **Skyrim SE 1.5.97** (modpack Thana Khan v8.4, MO2, FR).
Faelia est une Bosmer rapide et vive qui te suit partout. Elle récolte les ingrédients d'alchimie autour de toi et les range dans sa besace. Tu peux agrandir la besace en la payant. En combat, elle se met à l'abri.

---

## 1. Cahier des charges (décisions validées)

| Sujet | Décision |
|---|---|
| Jeu | Skyrim SE **1.5.97**, SKSE 1.5.97, modpack géré par MO2 |
| Fabrication de l'ESP | **Générée par code** (C# + Mutagen, .NET 8). Le CK sert uniquement à compiler les scripts et à générer le visage. |
| Dépendances | SKSE, **PapyrusUtil**, **powerofthree's Papyrus Extender** (déjà dans le modpack) ; Fuz Ro D-oh (déjà présent) pour les sous-titres sans voix |
| PNJ | **Faelia**, Bosmer femme, voix vanilla `FemaleYoungEager`, essentielle |
| Obtention | **Pouvoir mineur « Appel de Faelia »** reçu automatiquement. Il l'appelle ou la renvoie. |
| Suivi | Système **indépendant de NFF** : package de suivi maison, pas de place de compagnon occupée |
| Vitesse | +50 % de vitesse de déplacement (capacité permanente), Discrétion élevée |
| Récolte | **Vraie récolte** : plantes et champignons (Flora), arbres et plantes (Tree), ruches, nids, palourdes et nirnroot, ingrédients posés au sol. **Jamais d'objet possédé** (pas de vol). |
| Mode récolte | Activable ou désactivable par dialogue |
| Stockage | Compté en **nombre d'objets**, 300 au départ |
| Améliorations | 300 → **600** (1 000 po) → **1 000** (3 000 po) → **1 500** (7 500 po) → **2 500** (15 000 po) |
| Échange | Par dialogue « Montre-moi ta besace », qui ouvre un menu conteneur. Tout ce qui n'est pas un ingrédient est rendu aussitôt. |
| Déplacements | Téléportation auto : voyage rapide, changement de zone (portes), distance > ~80 m. À cheval, elle court. |
| Combat | Elle reste en retrait, accroupie (furtive) à 15-25 m. Si elle est attaquée, elle fuit (comportement « couarde »). Elle est essentielle : genou à terre, puis elle se relève. |
| Dialogues | Texte français, sans fichier audio (Fuz Ro D-oh affiche le sous-titre). Les répliques génériques utilisent la voix vanilla. |

---

## 2. Environnement constaté et prérequis

**Déjà en place :**
- `C:\SteamLibrary\steamapps\common\Skyrim Special Edition` : SkyrimSE.exe 1.5.97, SKSE 1.5.97, `Data\Scripts\Source` (incomplet : 63 fichiers)
- `C:\Skyrim Thana Khan Modspack v8.4\mods\` :
  - PapyrusUtil SE, avec ses `.psc`
  - po3 Papyrus Extender, avec ses `.psc`. On utilisera surtout `FindAllReferencesOfFormType` et `IsQuestItem`.
  - SkyUI, NFF, CACO, Fuz Ro D-oh, Base Object Swapper / Seasons, plusieurs mods de flore (Mari's flora, Freak's Floral…)
- .NET SDK 8.0.426

**À installer (phase 0, guidée) :**
1. ⚠️ **Sauvegarde préalable** de `SkyrimSE.exe` et de `Data\*.esm`, `*.bsa`, `*.esl` (fichiers vanilla et CC). En effet, installer le Creation Kit via Steam peut **mettre à jour le jeu en 1.6** et casser le modpack. On vérifie que la mise à jour auto Steam reste bloquée (le `appmanifest_489830.acf` est en lecture seule, ce qui est la méthode habituelle des modpacks downgradés). Après l'installation, on revérifie que l'exe est toujours en 1.5.97.
2. **Creation Kit** (Steam, gratuit) et **Creation Kit Platform Extended** (Nexus), pour la stabilité du CK.
3. Extraire `Data\Scripts.zip` (fourni avec le CK) vers `Data\Source\Scripts`, ce qui donne les sources vanilla complètes.
4. Récupérer les **sources Papyrus de SKSE 1.5.97** (archive `skse64_2_00_20` sur skse.silverlock.org, dossier `Data\Scripts\Source`). Le mod `SKSE_Scripts` du modpack ne contient pas de `.psc`.
5. (Recommandé) **SSEEdit / xEdit**, pour inspecter l'ESP généré.
6. Dans MO2 : ajouter `CreationKit.exe` comme exécutable, afin que le CK voie les mods du modpack (indispensable pour le visage).
7. Dans MO2 : **dupliquer le profil** `01 - Écran standard` en `NPCHerbo Test`, avec des sauvegardes propres au profil, pour ne jamais abîmer tes vraies parties.

---

## 3. Architecture du mod

### 3.1 Enregistrements de l'ESP (`NPCHerbo.esp`, flaggé ESL)

L'en-tête est forcé en **1.70** (compatible 1.5.97). Les FormIDs vont de 0x800 à 0xFFF, ce qui ne consomme aucun slot de plugin plein.

| EditorID | Type | Rôle |
|---|---|---|
| `NPCH_Faelia` | NPC_ | Bosmer F, essentielle, unique, voix FemaleYoungEager, confiance « Couarde », agressivité « Non agressive », Discrétion 100, tenue vanilla discrète |
| `NPCH_FaeliaRef` | ACHR | Référence persistante, placée dans la cellule de garde |
| `NPCH_HoldingCell` | CELL (intérieur) | « Repaire de Faelia » : cellule technique invisible où elle attend quand elle est renvoyée |
| `NPCH_HoldingMarker` | REFR XMarker | Point de retour |
| `NPCH_BagChest` | CONT + REFR | **La besace** : coffre persistant dans la cellule de garde, nommé « Besace de Faelia » |
| `NPCH_Faction` | FACT | Faction de Faelia, alliée à `PlayerFaction` |
| `NPCH_PlayerRelationship` | RELA | Relation « Allié » Faelia ↔ joueur |
| `NPCH_AbAgility` / `NPCH_AbAgilityEffect` | SPEL + MGEF | Capacité permanente : vitesse de déplacement +50 % |
| `NPCH_PowerSummon` / `NPCH_PowerSummonEffect` | SPEL (pouvoir mineur) + MGEF (Script) | « Appel de Faelia » |
| `NPCH_MainQuest` | QUST | Quête démarrée au lancement, invisible. Alias `Player`, `Faelia` (forcé), `BagChest`. Porte les packages et les dialogues. |
| `NPCH_PkgFollow` | PACK | Suivre le joueur (distance ~3-6 m). Condition : `NPCH_Following == 1` |
| `NPCH_PkgHide` | PACK | Suivre le joueur de loin (15-25 m), **toujours accroupie**. Condition : joueur en combat |
| `NPCH_PkgWait` | PACK | Rester sur place (cellule de garde) |
| Globales `NPCH_Following`, `NPCH_HarvestEnabled`, `NPCH_BagTier`, `NPCH_BagCapacity`, `NPCH_BagCount`, `NPCH_HarvestRadius`, `NPCH_LeashDistance` | GLOB | État et réglages (modifiables à la console) |
| `NPCH_MsgBagStatus`, `NPCH_MsgBagFull`, `NPCH_MsgRefused`, `NPCH_MsgInstalled`… | MESG | Messages FR |
| Branche + topics de dialogue | DLBR / DIAL / INFO | Voir 4.6 |

Les packages sont **dérivés des templates vanilla** (Follow, Travel), que le générateur retrouve par EditorID dans `Skyrim.esm`.

### 3.2 Scripts Papyrus

| Script | Attaché à | Rôle |
|---|---|---|
| `NPCH_MainQuestScript` | Quête | État global, installation (don du pouvoir), appel/renvoi, prix et paliers, recomptage au chargement |
| `NPCH_PlayerAliasScript` | Alias Player | `OnPlayerLoadGame` (relance les boucles et recompte), `OnPlayerFastTravelEnd`, `OnLocationChange` → téléportation |
| `NPCH_FaeliaAliasScript` | Alias Faelia | **Boucle principale** (toutes les ~2 s) : téléportation, état combat, une étape de récolte |
| `NPCH_HarvestLib` | (bibliothèque globale) | Scan, filtres, choix de la cible, liste des plantes déjà visitées |
| `NPCH_BagScript` | Alias BagChest | `OnItemAdded`/`OnItemRemoved` : filtre « ingrédients uniquement », plafond, compteur |
| `NPCH_SummonEffectScript` | MGEF du pouvoir | Appelle `MainQuest.ToggleFaelia()` |
| `TIF_NPCH_*` | Infos de dialogue | Fragments : ouvrir la besace, acheter un sac, activer/couper la récolte, état du sac |

### 3.3 Machine à états de Faelia

```
          pouvoir                          pouvoir
RENVOYÉE ─────────► SUIT LE JOUEUR ◄──────────────── (retour au repaire)
 (repaire)            │      ▲
                      │      │ cueillette finie / cible invalide
     plante proche ───┘      │
     & récolte ON     ▼      │
                    RÉCOLTE ─┘
                      │
     joueur en combat │ (depuis SUIT ou RÉCOLTE)
                      ▼
                  À L'ABRI (suit de loin, accroupie ; fuit si attaquée)
                      │ fin du combat
                      ▼
                  SUIT LE JOUEUR
```

---

## 4. Détail des mécaniques

### 4.1 Appel / renvoi (pouvoir)
- À l'installation (`OnInit` de la quête), le joueur reçoit le pouvoir. Un message s'affiche : « Nouveau pouvoir : Appel de Faelia ».
- **Appel** : `MoveTo` derrière le joueur, hors de son champ de vision si possible. `NPCH_Following = 1`, `EvaluatePackage`.
- **Renvoi** : elle retourne au `NPCH_HoldingMarker`, et `NPCH_Following = 0`. La besace reste intacte, mais elle n'est accessible qu'en sa présence.

### 4.2 Suivi et téléportation
- Package `NPCH_PkgFollow` (vitesse : course quand elle est loin).
- Elle est téléportée derrière le joueur dans ces cas :
  - `OnPlayerFastTravelEnd` (voyage rapide) ;
  - `OnLocationChange` du joueur, quand Faelia n'est pas dans la même cellule ou n'est pas chargée (portes, donjons) ;
  - dans la boucle de 2 s : distance > `NPCH_LeashDistance` (5600 unités ≈ 80 m) ou 3D non chargée.
- À cheval : la capacité +50 % lui permet de suivre au trot. Au galop, la téléportation prend le relais.

### 4.3 Récolte : algorithme (une étape par tick de la boucle)
1. **Préconditions** :
   - Faelia suit le joueur ;
   - `NPCH_HarvestEnabled == 1` ;
   - la besace n'est pas pleine ;
   - ni le joueur ni elle ne sont en combat ;
   - le joueur n'est pas dans un menu ni en dialogue ;
   - la cellule n'est pas possédée (villes, maisons).
2. **Scan** avec `PO3_SKSEFunctions.FindAllReferencesOfFormType(Faelia, type, NPCH_HarvestRadius)` pour les types Flora (39), Tree (38) et Ingredient (30). Le rayon par défaut est de **1500 unités ≈ 21 m**.
3. **Filtres** :
   - la référence est activée et chargée, et ce n'est pas un objet de quête (`IsQuestItem`) ;
   - **aucun propriétaire** : ni la référence (`GetActorOwner` / `GetFactionOwner`), ni sa cellule. Cela exclut les fermes, les maisons et ta propre maison ;
   - Flora / Tree : `GetIngredient()` (SKSE) doit renvoyer un **Ingrédient** ou une liste nivelée. Les ruches, nids, palourdes et nirnroot sont des Flora dans le jeu de base, donc ils sont couverts ;
   - Ingredient au sol : la base est un `Ingredient` ;
   - la cible est à moins de `NPCH_LeashDistance` du joueur (elle ne s'éloigne pas de toi), avec un écart de hauteur raisonnable (pas de falaise) ;
   - la cible n'est pas dans la **liste des plantes déjà visitées** (voir 5).
4. **Choix** de la cible la plus proche de Faelia.
5. **Déplacement** : `Faelia.PathToReference(cible, 1.0)` (course). En cas d'échec de chemin, la cible part dans la liste des visitées.
6. **Cueillette** :
   - une animation vanilla de ramassage ;
   - puis `cible.Activate(Faelia)`. L'ingrédient arrive dans l'inventaire de Faelia, puis il est transféré dans la besace ;
   - pour un ingrédient au sol : `Besace.AddItem(cible)`.
7. **Plafond** : si la cueillette dépasse la capacité, l'excédent est posé à ses pieds. Elle annonce « Ma besace est pleine ! » une seule fois et cesse de récolter.
8. La cible est ajoutée à la liste des visitées, avec la date en jours de jeu.

**Liste des plantes visitées** : elle est stockée via PapyrusUtil `StorageUtil`, sous forme de deux listes parallèles (référence + `GameDaysPassed`). Elle est purgée des entrées de plus de 10 jours (délai de repousse) et plafonnée à environ 2000 entrées. Cela évite qu'elle revienne sans cesse vers une plante déjà cueillie, y compris celles que **tu** as cueillies : elle y passe une fois, ne récupère rien et la mémorise.

### 4.4 La besace (`NPCH_BagChest`)
- Ouverte par le dialogue « Montre-moi ta besace » (`BagChest.Activate(Player)` en fin de dialogue).
- `OnItemAdded` :
  - si l'objet vient du joueur et **n'est pas un `Ingredient`**, il est rendu aussitôt, avec le message « Je ne garde que les ingrédients d'alchimie ! » ;
  - si c'est un ingrédient mais que le plafond est dépassé, l'excédent est rendu au joueur avec le message « Plus de place ».
- `NPCH_BagCount` est mis à jour à chaque ajout et à chaque retrait. Il est **recompté entièrement** au chargement d'une partie (parcours SKSE `GetNumItems` / `GetNthForm` / `GetItemCount`) pour corriger toute dérive.

### 4.5 Améliorations de sac

| Palier | Capacité | Prix pour y accéder |
|---|---|---|
| 0 | 300 | — |
| 1 | 600 | 1 000 po |
| 2 | 1 000 | 3 000 po |
| 3 | 1 500 | 7 500 po |
| 4 | 2 500 | 15 000 po |

- Une seule ligne de dialogue est visible à la fois : celle du palier suivant. Elle a deux conditions : `NPCH_BagTier == n`, et l'or du joueur supérieur ou égal au prix.
- Si tu n'as pas assez d'or, la ligne reste visible mais Faelia répond « Il me faudrait X pièces d'or… », sans transaction.
- Le fragment retire l'or, incrémente le palier et met à jour la capacité.

### 4.6 Dialogues (texte FR, sans voix)
La branche principale, avec la condition `GetIsID NPCH_Faelia`, propose :
- « Montre-moi ta besace. »
- « Où en est ta besace ? », qui affiche le message « Besace : 247 / 600 ingrédients ».
- « Tu pourrais t'acheter un plus grand sac. (X po) » (selon le palier, voir 4.5)
- « Arrête de cueillir pour l'instant. » / « Tu peux recommencer à cueillir. » (une seule des deux est visible selon l'état)
- Au revoir.

Quelques répliques d'ambiance, sur un ton enjoué de Bosmer, s'affichent aléatoirement à la cueillette (notifications discrètes, désactivables).

### 4.7 Combat
- Quand le joueur entre en combat, le package `NPCH_PkgHide` passe en tête de pile : elle reste loin (15-25 m), accroupie, et elle a Discrétion 100. La récolte est suspendue.
- Si elle est attaquée, sa confiance « Couarde » la fait **fuir**. Elle est essentielle : elle tombe à genoux, puis se relève.
- `IgnoreFriendlyHits` est activé, et elle appartient à une faction alliée au joueur : un coup ou un sort de zone accidentel ne la rend pas hostile.
- À la fin du combat, elle reprend le suivi normal puis la récolte.

---

## 5. Structure du projet et chaîne de build

```
NPCHerbo/
├─ PLAN.md
├─ src/NPCHerbo.Generator/      # Console C# (Mutagen.Bethesda.Skyrim) → génère NPCHerbo.esp
│   ├─ Program.cs
│   ├─ Records/*.cs             # PNJ, quête, packages, dialogues, sorts, cellule…
│   └─ Texts.fr.cs              # Tous les textes FR centralisés
├─ scripts/Source/*.psc         # Sources Papyrus du mod
├─ tools/
│   ├─ config.ps1               # Chemins (jeu, MO2, CK, sources SKSE/PU/po3)
│   ├─ build.ps1                # Build complet : compile → génère → assemble
│   ├─ compile-scripts.ps1      # PapyrusCompiler.exe + imports + TESV_Papyrus_Flags.flg
│   └─ deploy.ps1               # Copie dist/ vers MO2\mods\NPCHerbo
├─ assets/facegen/              # Visage exporté depuis le CK (nif + dds)
└─ dist/NPCHerbo/               # Mod prêt : NPCHerbo.esp, Scripts/*.pex, Source/, meshes/, textures/
```

Commande unique : `tools\build.ps1 -Deploy`. Ensuite, tu actives « NPCHerbo » dans MO2.

---

## 6. Phases de réalisation

Chaque phase se termine par un **test en jeu** sur le profil de test, avec une checklist de validation.

### Phase 0 : Environnement (avec toi)
- Sauvegarde du jeu, installation du CK et de CKPE, extraction des sources, sources SKSE, profil MO2 de test.
- Mise en place de `tools/config.ps1` et `compile-scripts.ps1`. Je crée aussi le dépôt git du projet.
- ✅ Critères : un script de test compile en `.pex`, `SkyrimSE.exe` est toujours en 1.5.97, et le modpack se lance normalement.

### Phase 1 : Squelette et prototype technique (point le plus risqué)
- Le générateur crée le PNJ, la cellule de garde, la besace, la quête, le pouvoir et le package de suivi.
- **Prototype de la cueillette par PNJ** : on vérifie que `Flora.Activate(Faelia)` lui donne bien l'ingrédient et marque la plante comme cueillie.
  - Plan B si ce n'est pas le cas : activer en tant que joueur, puis transférer silencieusement vers la besace. L'inconvénient est la notification « X ajouté ».
- On vérifie aussi que les plantes remplacées par **Seasons of Skyrim / Base Object Swapper** et les plantes de CACO se comportent normalement.
- ✅ Critères : le pouvoir l'appelle et la renvoie, elle te suit, et elle cueille une plante désignée.

### Phase 2 : Récolte complète
- Scan, filtres de propriété, liste des visitées, choix de la cible, animation, besace pleine, activation/désactivation.
- Réglage du rayon et de la fréquence, et mesure de la charge Papyrus dans les zones très fleuries (Mari's flora, Freak's Floral).
- ✅ Critères : elle récolte seule en forêt, ne touche à rien en ville ni chez toi, ne boucle pas sur une plante, et le jeu reste fluide.

### Phase 3 : Besace et capacité
- Dialogue « Montre-moi ta besace », filtre des non-ingrédients, plafond, compteur et recomptage au chargement, message d'état.
- ✅ Critères : tu prends et déposes des ingrédients, une épée est refusée et rendue, et le compteur reste juste après sauvegarde et chargement.

### Phase 4 : Améliorations de sac
- 4 paliers, retrait de l'or, cas d'or insuffisant.
- ✅ Critères : la progression 300 → 2500 fonctionne, l'or est bien débité, et le palier est conservé après chargement.

### Phase 5 : Déplacements
- Voyage rapide, portes et donjons, distance, cheval, chargement de partie avec Faelia loin du joueur.
- ✅ Critères : elle n'est jamais perdue, et elle n'apparaît jamais sous tes yeux quand c'est évitable.

### Phase 6 : Combat
- Package d'abri, fuite, essentielle, coups amis.
- ✅ Critères : contre des bandits et contre un dragon, elle reste à l'écart, survit et reprend la récolte ensuite.

### Phase 7 : Apparence et finitions
- Visage de base copié d'une Bosmer vanilla, puis **export facegen dans le CK lancé via MO2** (sélectionner le PNJ, Ctrl+F4). Cela évite le bug du « visage noir ».
- Tenue, nom affiché, relecture des textes FR, messages, fichier README pour l'installation.
- ✅ Critères : visage correct en jeu avec les mods de race du modpack, et aucun texte manquant.

### Phase 8 : Recette finale
- Checklist complète (voir 7), longue session de jeu, contrôle du `Papyrus.0.log`.
- Version 1.0 dans MO2.

---

## 7. Plan de test en jeu (profil `NPCHerbo Test`)

- [ ] Nouvelle partie ou sauvegarde de test : le message d'installation s'affiche et le pouvoir est présent.
- [ ] Le pouvoir l'appelle, puis la renvoie, puis l'appelle de nouveau.
- [ ] En forêt (`coc RiverwoodExterior`, puis vers la rivière) : elle récolte seule. La besace se remplit et le message d'état est juste.
- [ ] À Rivebois (ferme, maisons) : elle ne prend rien de possédé.
- [ ] Ruche, nid, palourde, nirnroot, ingrédient posé au sol dans un donjon : tous sont récoltés.
- [ ] Récolte désactivée : elle se contente de suivre.
- [ ] Déposer une arme dans la besace : elle est rendue avec un message. Déposer des ingrédients au-delà du plafond : l'excédent est rendu.
- [ ] Besace pleine : message unique et arrêt de la récolte.
- [ ] Achat de chaque palier, avec puis sans assez d'or.
- [ ] Voyage rapide, entrée dans un donjon, retour dehors, galop à cheval.
- [ ] Combat contre des bandits (`player.placeatme 0001E7D6 3` ou équivalent) : elle se met à l'abri, fuit si elle est attaquée et ne meurt pas.
- [ ] Sauvegarde puis chargement à chaque étape : état, palier et compteur sont conservés.
- [ ] Aucune erreur NPCH dans `Documents\My Games\Skyrim Special Edition\Logs\Script\Papyrus.0.log` (logs activés dans le profil de test).
- [ ] NFF : tes compagnons NFF fonctionnent toujours, et Faelia n'apparaît pas dans NFF.

---

## 8. Risques et incertitudes techniques

| Risque | Impact | Parade |
|---|---|---|
| Installer le CK via Steam met le jeu à jour en 1.6 | Modpack cassé | Sauvegarde préalable, manifest en lecture seule, vérification de la version après installation |
| `Activate(Faelia)` ne donne pas l'ingrédient à un PNJ | Récolte à revoir | Prototype en phase 1, plan B (activation en tant que joueur puis transfert) |
| Pas de fonction Papyrus fiable pour savoir si une plante est déjà cueillie | Allers-retours inutiles | Liste des plantes visitées (4.3), purge à 10 jours |
| Packages générés par code mal formés | IA inerte | Dérivation de templates vanilla, inspection xEdit, test isolé en phase 1 |
| En-tête 1.71 ou FormIDs < 0x800 refusés par 1.5.97 | Plugin qui ne charge pas | En-tête 1.70 forcé, plage ESL 0x800-0xFFF |
| Facegen incompatible avec les mods de race du modpack | Visage noir ou décalé | Export facegen depuis le CK lancé **via MO2**, avec le plugin modifiant la race chargé |
| Zones très denses en flore | Charge Papyrus | Rayon raisonnable, une seule cible par tick, pas de scan en ville ni en combat |
| Désinstallation en cours de partie | Scripts orphelins | Mod scripté, donc pas de désinstallation sur une partie en cours (indiqué dans le README) |

---

## 9. Compatibilité avec le modpack

- **NFF** : aucune interaction. Faelia n'est pas dans `CurrentFollowerFaction` ni dans `PotentialFollowerFaction`.
- **CACO, Mari's flora, Freak's Floral, Seasons / BOS** : ces mods utilisent des Flora standard, elles sont donc récoltables. Les plantes saisonnières remplacées seront vérifiées en phase 1.
- **QuickLoot IE** : il peut s'afficher sur la besace si on la regarde, mais on ne la regarde jamais puisqu'elle est dans la cellule de garde. Le menu complet s'ouvre via le dialogue.
- **Fuz Ro D-oh** : déjà présent. Les sous-titres restent affichés le temps de la lecture.
- **Disable Follower Collision** : il ne s'appliquera pas à Faelia. Si elle bloque un passage, la téléportation par distance ou le pouvoir permettent de s'en sortir.

---

## 10 bis. Features v2 validées par l'utilisateur (2026-11-15)

Ordre proposé (lots) :
1. **Savoir** : leçon du jour (`Ingredient.LearnNextEffect` sur un ingrédient de la besace, 1×/jour), carnet de découvertes (1re cueillette d'un ingrédient → réplique/notification, liste StorageUtil), formatrice d'alchimie (classe + faction de formateur vanilla).
2. **Récolte** : alerte plante rare (liste de bases rares + détour prioritaire), préférences de cueillette (FormList d'exclusion gérée par dialogue, mode « rares seulement »), commande ciblée (« trouve-moi de quoi soigner » : filtre par effet via `GetNthEffectMagicEffect`), nuit/météo (répliques + bonus d'ambiance), cadavres de créatures (scan des acteurs morts, ingrédients de leur inventaire).
3. **Confort** : menu MCM SkyUI (tous les réglages console), widget « X / capacité », dépôt à la maison (coffre désigné une fois, transfert par dialogue).

## 10. Hors périmètre (évolutions possibles plus tard)

- Menu MCM (SkyUI) pour régler le rayon, les prix et les notifications.
- Récolte sur les cadavres de créatures.
- Voix générée (xVASynth).
- Filtre « ne récolte que les ingrédients que je n'ai pas encore » ou liste noire d'ingrédients.
- Widget « X / capacité » à l'écran.
