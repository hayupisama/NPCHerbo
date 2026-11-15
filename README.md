# NPCHerbo — Faelia, l'herboriste qui vous suit

[![Build](https://github.com/hayupisama/NPCHerbo/actions/workflows/build.yml/badge.svg)](https://github.com/hayupisama/NPCHerbo/actions/workflows/build.yml)
![Skyrim SE 1.5.97](https://img.shields.io/badge/Skyrim%20SE-1.5.97-6b4f2a)
![Licence MIT](https://img.shields.io/badge/licence-MIT-green)
![Codé par Claude Code](https://img.shields.io/badge/cod%C3%A9%20par-Claude%20Code-d97757)

Mod pour **The Elder Scrolls V: Skyrim Special Edition (1.5.97)** : **Faelia**, une Bosmer vive et rapide, vous suit partout et **récolte pour vous les ingrédients d'alchimie** (plantes, champignons, Nirnroot, ingrédients au sol, créatures mortes). Elle range tout dans sa besace, que vous pouvez agrandir, et elle se met à l'abri pendant les combats.

> 🤖 **Ce mod a été entièrement conçu et codé par [Claude Code](https://claude.com/claude-code)** (l'agent de programmation d'Anthropic) : scripts Papyrus, générateur de plugin en C#, outils de build et documentation. Le propriétaire du dépôt a défini les besoins, fait les choix de design et mené tous les tests en jeu.

*English summary below.*

---

## ✨ Fonctionnalités

**Récolte**
- **Tournées de récolte** : elle repère les plantes autour de vous, enchaîne les plantes voisines sans revenir vers vous entre deux, puis vous rejoint.
- Récolte **réelle** : la plante est cueillie dans le monde, comme si vous l'aviez fait. **Jamais de vol** : elle ignore tout ce qui appartient à quelqu'un.
- **Plantes rares** (Nirnroot, Nirnroot cramoisie…) : elle les signale et fait un détour pour les cueillir.
- **Récolte ciblée** : « trouve-moi de quoi soigner / restaurer la magie / la vigueur / faire des poisons / seulement les rares ».
- **Préférences** : « ne cueille plus ce que tu viens de ramasser ».
- **Créatures mortes** : elle prélève leurs ingrédients (jamais sur les humanoïdes).
- **Déblocage** : bloquée par un obstacle, elle fait un petit saut de côté et recalcule son chemin ; elle abandonne la plante après 3 essais.

**Besace**
- N'accepte **que les ingrédients** (tout autre objet vous est rendu).
- **300 places**, agrandissables contre de l'or : 600 → 1 000 → 1 500 → 2 500.
- Alertes de remplissage à 50, 75 et 90 % ; **dépôt** de toute la besace dans un coffre de votre maison.

**Savoir d'herboriste**
- **Leçon du jour** : chaque jour, elle vous apprend un effet inconnu d'un ingrédient de sa besace.
- **Carnet de découvertes** : elle note chaque nouvelle sorte d'ingrédient trouvée.
- **Formatrice d'alchimie** jusqu'au niveau 75.

**Compagnonne**
- Appelée ou renvoyée par un pouvoir mineur, **indépendante des gestionnaires de compagnons** (n'occupe pas de place de suiveur, compatible NFF).
- Rapide (+50 % de vitesse), se téléporte si elle vous perd (voyage rapide, portes, distance).
- **En combat**, elle reste à l'écart, accroupie et discrète ; essentielle, elle ne meurt jamais.
- Réactions **vocales vanilla** (« Regardez ça ! »…) en réutilisant de vraies répliques doublées, et répliques d'ambiance nuit/météo.

## 📦 Prérequis

| Requis | Rôle |
|---|---|
| Skyrim Special Edition **1.5.97** | version ciblée (plugin ESL, en-tête 1.70) |
| [SKSE64](https://skse.silverlock.org/) pour 1.5.97 | fonctions de script étendues |
| [PapyrusUtil SE](https://www.nexusmods.com/skyrimspecialedition/mods/13048) | stockage persistant (plantes visitées, carnet…) |
| [powerofthree's Papyrus Extender](https://www.nexusmods.com/skyrimspecialedition/mods/22854) | recherche des plantes autour du joueur |
| [Fuz Ro D-oh](https://www.nexusmods.com/skyrimspecialedition/mods/15109) | affichage des dialogues sans fichier audio |

Facultatif : [MCM Helper](https://www.nexusmods.com/skyrimspecialedition/mods/53000) + SkyUI pour un menu MCM (les mêmes réglages existent aussi directement dans le dialogue de Faelia).

## 🚀 Installation

Le mod se construit à partir des sources (voir [Construire depuis les sources](#-construire-depuis-les-sources)). Le résultat (`dist/NPCHerbo/`) s'installe comme n'importe quel mod avec Mod Organizer 2 ou Vortex : `NPCHerbo.esp` (flaggé ESL, n'occupe pas de place dans l'ordre de chargement), les scripts `.pex`, le fichier `SEQ` et le menu MCM.

Mod scripté : ne le désinstallez pas en cours de partie.

## 🎮 Utilisation

1. Au chargement, vous recevez le pouvoir **« Appel de Faelia »** (Magie → Pouvoirs). Lancez-le pour l'appeler, relancez-le pour la renvoyer.
2. Elle vous suit et récolte toute seule. Chaque cueillette s'affiche en notification.
3. **Parlez-lui** :
   - *Montre-moi ta besace* / *Où en est ta besace ?*
   - *J'aimerais t'offrir un plus grand sac* (contre de l'or)
   - *Tu pourrais m'apprendre l'alchimie ?*
   - *Parlons de ta cueillette…* → arrêter/reprendre, ingrédients précis, exclusions, **réglages**
   - *À propos de notre maison…* → désigner un coffre de dépôt, y ranger la besace

### Réglages

Dans *Parlons de ta cueillette… → Changeons tes réglages* (ou dans le MCM s'il s'affiche) : rayon de recherche, taille des tournées, réactions vocales, notifications, logs à l'écran, créatures mortes, leçon du jour, nuit/météo, alerte rares.

Tous les réglages sont aussi des globales modifiables en console, par exemple :

```text
set NPCH_ScanRadius to 2500        ; rayon de recherche autour du joueur (70 unités = 1 m)
set NPCH_BatchMaxPlants to 10      ; plantes par tournée
set NPCH_ReactionChance to 0       ; Faelia muette
set NPCH_Debug to 0                ; masquer les logs à l'écran
```

## ⚠️ Limites connues

- **Menu MCM** : SkyUI est limité à 128 menus ; dans un très gros modpack, le menu peut ne pas apparaître. Les réglages restent accessibles par le dialogue.
- **Pas de widget à l'écran** : remplacé par des notifications de remplissage.
- **Visage** : aucun fichier facegen n'est fourni ; l'apparence est calculée par le jeu. En cas de visage gris, régénérez-le dans le Creation Kit (`tools/import-facegen.ps1`).
- Plantes hors navmesh (sur un rocher…) : elle peut ne pas les atteindre et les abandonne après 3 essais.

## 🛠️ Construire depuis les sources

Le plugin n'est **pas édité à la main dans le Creation Kit** : il est **généré par du code C#** avec [Mutagen](https://github.com/Mutagen-Modding/Mutagen), ce qui le rend reproductible et versionnable.

**Outils** : Windows, [.NET SDK 8](https://dotnet.microsoft.com/download), le Creation Kit de Skyrim SE (pour `PapyrusCompiler.exe` et les sources vanilla extraites de `Scripts.zip`), les sources Papyrus de SKSE, PapyrusUtil et po3 Papyrus Extender.

```powershell
# 1. Indiquer vos chemins (fichier ignoré par git)
Copy-Item tools\config.local.example.ps1 tools\config.local.ps1
notepad tools\config.local.ps1

# 2. Compiler les scripts, générer le plugin, assembler le mod (et le copier dans MO2 avec -Deploy)
.\tools\build.ps1 -Deploy
```

Détails techniques : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Document de conception d'origine : [docs/CONCEPTION.md](docs/CONCEPTION.md).

## 🤝 Contribuer

Les contributions sont les bienvenues : voir [CONTRIBUTING.md](CONTRIBUTING.md). Historique des versions : [CHANGELOG.md](CHANGELOG.md).

## 🙏 Crédits

- **Code, conception technique et documentation** : [Claude Code](https://claude.com/claude-code) (Anthropic).
- **Idée, design et tests en jeu** : [hayupisama](https://github.com/hayupisama).
- Bibliothèques et mods utilisés : Mutagen, SKSE64, PapyrusUtil, powerofthree's Papyrus Extender, Fuz Ro D-oh, MCM Helper, SkyUI.
- The Elder Scrolls V: Skyrim est une marque de Bethesda Softworks / ZeniMax Media. Projet de fan non officiel, sans lien avec Bethesda.

## 📄 Licence

[MIT](LICENSE).

---

## English summary

**NPCHerbo** adds **Faelia**, a quick Bosmer follower for **Skyrim SE 1.5.97** who **gathers alchemy ingredients for you**: real harvesting of nearby plants in "rounds", rare plant alerts (Nirnroot), targeted gathering by effect, creature corpses, an ingredient-only satchel upgradable with gold (300 → 2,500), daily alchemy lessons, a discovery log, alchemy training, home chest deposit, a hide-in-combat behaviour and vanilla voice reactions. She is independent from follower frameworks (NFF compatible).

Requires SKSE, PapyrusUtil, powerofthree's Papyrus Extender and Fuz Ro D-oh. The ESL plugin is **generated from C# code with Mutagen**; build with `tools\build.ps1` (see above). In-game text is in French.

> 🤖 **Entirely designed and coded by [Claude Code](https://claude.com/claude-code)** (Anthropic's coding agent); the repository owner defined the requirements, made the design calls and did all in-game testing.
