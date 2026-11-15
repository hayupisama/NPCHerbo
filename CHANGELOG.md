# Journal des modifications

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/).

## [Non publié]

### Ajouté
- Documentation publique : README, guide de contribution, architecture, licence MIT, modèles d'issues, CI de compilation du générateur.

### Modifié
- Chemins de l'environnement de build configurables (`tools\config.local.ps1`, variables `NPCH_GAME_DIR` / `NPCH_MO2_DIR` / `NPCH_GAME_DATA`).
- Documents de travail déplacés dans `docs/`.

## [2.0.0] — 2026-11-15

### Ajouté
- **Savoir d'herboriste** : leçon du jour, carnet de découvertes, formation en alchimie (jusqu'au niveau 75).
- **Récolte** : alerte et détour pour les plantes rares (Nirnroot), récolte ciblée par effet, exclusion d'un ingrédient, prélèvement sur les créatures mortes, bonus et répliques nuit/météo.
- **Déblocage** : détection d'absence de progrès, petit saut de côté et recalcul d'itinéraire, abandon après 3 essais.
- **Confort** : dépôt de la besace dans un coffre de la maison, notifications de remplissage, menu de réglages intégré au dialogue, menu MCM (MCM Helper).
- Dialogues regroupés en sous-menus.

### Corrigé
- Plusieurs défauts relevés en revue de code (détection des Nirnroot, accents, réplique de nuit en double, maisons de mods refusées comme dépôt).

## [1.0.0] — 2026-11-15

### Ajouté
- Faelia, suiveuse Bosmer essentielle appelée par un pouvoir mineur, indépendante des gestionnaires de compagnons.
- Récolte automatique par tournées (package de récolte prioritaire, chaîne de plantes voisines).
- Besace limitée aux ingrédients, avec 4 paliers achetables.
- Dialogues en français, réactions vocales vanilla (Shared Info), comportement « à l'abri » en combat, téléportation.
- Plugin ESL généré en C# (Mutagen) avec FormIDs stables et migrations de réglages.
