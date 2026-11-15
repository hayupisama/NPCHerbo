# Contribuer à NPCHerbo

Merci de votre intérêt ! Rapports de bugs, idées et pull requests sont les bienvenus.

## Signaler un bug

Ouvrez une [issue « Bug »](../../issues/new?template=bug_report.md) en indiquant :

- votre version de Skyrim (le mod cible **1.5.97**) et de SKSE ;
- ce que fait Faelia, et ce que vous attendiez ;
- les lignes `[NPCHerbo]` de `Documents\My Games\Skyrim Special Edition\Logs\Script\Papyrus.0.log`
  (activez les logs Papyrus : `bEnableLogging=1`, `bEnableTrace=1` dans la section `[Papyrus]` de `Skyrim.ini`).

## Proposer une idée

Ouvrez une [issue « Idée »](../../issues/new?template=feature_request.md). Décrivez le besoin côté joueur avant la solution technique.

## Préparer l'environnement

Voir la section *Construire depuis les sources* du [README](README.md) : .NET SDK 8, Creation Kit (compilateur Papyrus et sources vanilla), sources Papyrus de SKSE, PapyrusUtil et po3 Papyrus Extender. Vos chemins vont dans `tools\config.local.ps1` (jamais commité).

```powershell
.\tools\build.ps1           # compile les scripts + génère le plugin + assemble dist\NPCHerbo
.\tools\build.ps1 -Deploy   # idem, puis copie dans votre instance Mod Organizer 2
```

## Règles du projet

Lisez [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) avant de modifier le code. Points essentiels :

1. **Le plugin est généré, jamais édité dans le Creation Kit.** Toute modification d'enregistrement passe par `src/NPCHerbo.Generator`.
2. **FormIDs stables** : chaque enregistrement a un FormID fixe dans `formids.json` (attribué automatiquement à la première génération). Ne réutilisez jamais un EditorID supprimé pour autre chose et ne renumérotez pas : cela casserait les sauvegardes des joueurs.
3. **Valeurs par défaut des globales** : elles sont conservées dans les sauvegardes. Pour changer un réglage existant, ajoutez une étape dans `MigrateSettings()` (`NPCH_MainQuestScript.psc`) et incrémentez `SETTINGS_VERSION`.
4. **Plugin ESL** : n'ajoutez pas de nouvelle cellule (CELL) ; placez les références dans la surcharge de cellule existante.
5. **Papyrus** :
   - les sources sont compilées sans accents : utilisez des enregistrements `Message` (générés en C#) ou la fonction `Fr()` de `NPCH_FeaturesScript` (jetons `{eac}`, `{egr}`…) pour le texte affiché ;
   - toute boucle longue doit vérifier la garde de génération (`IsCurrent(myGen)`) après chaque `Wait` ;
   - pas d'affectation `None` à un tableau ; pas de références conservées dans des variables de script au-delà d'une tournée.
6. Textes de jeu en **français** ; commentaires de code en français.

## Pull requests

- Une PR = un sujet ; décrivez le comportement en jeu attendu et **comment vous l'avez testé** (en jeu de préférence, sur une sauvegarde de test).
- Le build (`tools\build.ps1`) doit passer sans erreur Papyrus ni C#.
- Mettez à jour le [CHANGELOG](CHANGELOG.md) (section *Non publié*) et, si besoin, le README.

## Code de conduite

Restez courtois et bienveillants. Les remarques portent sur le code, jamais sur les personnes.
