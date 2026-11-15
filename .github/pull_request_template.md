## Changement
<!-- Ce que fait cette PR, côté joueur et côté code. -->

## Comment c'est testé
<!-- En jeu (sauvegarde de test), journal Papyrus, outils dump/dialogues du générateur... -->

## Vérifications
- [ ] `tools\build.ps1` passe sans erreur Papyrus ni C#
- [ ] Aucun FormID existant renuméroté (`formids.json` ne fait que s'allonger)
- [ ] Nouvelle valeur par défaut d'une globale existante → migration ajoutée (`MigrateSettings`)
- [ ] CHANGELOG mis à jour (section *Non publié*)
