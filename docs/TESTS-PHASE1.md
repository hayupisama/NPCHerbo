> Document historique : checklist utilisée pour tester le prototype (phase 1). Certains raccourcis de test qu'il mentionne ont été retirés depuis.

# Test en jeu — Phase 1 (prototype)

Objectif : vérifier que Faelia **apparaît, suit, récolte** et surtout **quelle méthode de cueillette marche** (point le plus risqué du projet).

> Le mode debug est actif : Faelia affiche ses actions en notifications `[Faelia] …` en haut à gauche.
> **Attendu à ce stade** : son visage peut être noir ou gris. C'est normal, il sera généré en phase 7.

## 0. Préparation (MO2)

1. Dans MO2, sélectionne le profil **`NPCHerbo Test`**, puis appuie sur **F5** (rafraîchir).
2. Panneau gauche : coche le mod **NPCHerbo**. Il apparaît tout en bas de la liste.
3. Panneau droit (Plugins) : vérifie que **NPCHerbo.esp** est coché, idéalement en fin d'ordre de chargement.
4. Lance le jeu via **SKSE**, puis charge une sauvegarde **en extérieur** (forêt, bord de route, pas en ville).

## 1. Installation

- [ ] Environ 3 s après le chargement, les messages « **NPCHerbo : nouveau pouvoir - Appel de Faelia** » et « **[Faelia] Plantes recoltables connues : X flores, Y plantes/arbres** » s'affichent. → **Note X et Y.**
- [ ] Le pouvoir **Appel de Faelia** est présent dans Magie → Pouvoirs.

## 2. Appel / suivi

- [ ] Équipe le pouvoir et lance-le (touche Z) : « Faelia vous rejoint. » s'affiche, et elle apparaît derrière toi.
- [ ] Marche, puis cours : elle suit. Elle est rapide (+50 % de vitesse).
- [ ] Sa tenue est celle d'un chasseur, et son nom affiché est « Faelia ».

## 3. Cueillette (le test clé)

Approche-toi de plantes : fleurs, champignons, buissons de baies. Rayon d'environ 20 m.

- [ ] Elle annonce « Cible : *nom de la plante* », court vers la plante et se penche.
- [ ] **Méthode 0 (par défaut)** : le message « **+1 *ingrédient*** » s'affiche-t-il ? La plante change-t-elle d'aspect (cueillie) ?
  - Si tu vois « **Rien obtenu de … (methode Faelia)** » : la méthode 0 ne marche pas. Passe au test suivant.
- [ ] **Méthode 1** : ouvre la console (touche ²), tape `set NPCH_HarvestMethod to 1` et valide, puis referme la console. Elle doit maintenant cueillir « à ton nom ». Les notifications vanilla « X ajouté » sont normales avec cette méthode, mais l'ingrédient doit finir dans **sa** besace et pas dans ton inventaire.
- [ ] **Ingrédient posé au sol** (si tu en croises un, par exemple dans un donjon) : il est ramassé (« +1 … (ramasse) »).
- [ ] **Ville / ferme** (Rivebois, une ferme) : elle **ne cueille rien** dans les champs ni chez les gens.

### 3 bis. Version 2 : récolte prioritaire + réactions vocales

- [ ] Dès qu'elle repère une plante (dans un rayon d'environ 28 m), elle **part en courant** vers elle, sans attendre que tu t'approches, puis enchaîne sur la suivante.
- [ ] Le délai entre « Cible : … » et « +N … » ne dépasse plus quelques secondes. Une plante injoignable est abandonnée au bout de 20 s maximum.
- [ ] Elle peut s'éloigner de toi (jusqu'à environ 50 m) pour récolter. Si elle va trop loin, elle se téléporte près de toi.
- [ ] **Réaction vocale** (voix FR vanilla) quand elle repère une plante, environ 6 fois sur 10 et au plus une fois toutes les 15 s : « Regardez ça ! », « Oh, regardez toutes ces couleurs ! », « Oh, ça a l'air charmant. », « Oh là ! ».
- [ ] Parfois, après une cueillette : « Parfois ça paye de regarder dans le moindre recoin. »
- Réglage : `set NPCH_ReactionChance to 100` (elle parle à chaque plante, pratique pour tester) ou `to 0` (muette).

## 4. Besace (raccourci temporaire de test)

- [ ] **Accroupis-toi puis lance le pouvoir** : le message « Besace de Faelia : X / 300 ingredients » s'affiche, puis la besace s'ouvre, avec les ingrédients récoltés dedans.
  (Ce raccourci sera remplacé par le dialogue en phase 3.)
- [ ] Prends quelques ingrédients : le compteur baisse (vérifie au prochain ouverture).
- [ ] **Dépose une arme ou de l'or** dans la besace : l'objet t'est rendu aussitôt, avec le message « Je ne garde que les ingredients d'alchimie ! ».
- [ ] Dépose des ingrédients : ils sont acceptés.
- [ ] **Test du plafond** : en console, tape `set NPCH_BagCapacity to 5`. Dépose ou fais récolter plus de 5 ingrédients : l'excédent t'est rendu (« Plus de place »), elle annonce « Ma besace est pleine ! » une seule fois, puis arrête de cueillir.
  Remets ensuite la capacité normale avec `set NPCH_BagCapacity to 300`. Elle est aussi recalculée à chaque chargement de partie.

## 5. Déplacements

- [ ] Entre dans un bâtiment ou une grotte : elle te rejoint à l'intérieur.
- [ ] Voyage rapide : elle est près de toi à l'arrivée.
- [ ] Monte à cheval et galope : elle suit ou se téléporte.

## 6. Renvoi et sauvegarde

- [ ] Lance le pouvoir **debout** : « Faelia retourne a son repaire. » s'affiche et elle disparaît.
- [ ] Relance-le : elle revient.
- [ ] **Sauvegarde, quitte, recharge** : elle est toujours là et récolte toujours.

## 7. Ce que je dois récupérer après le test

Pas besoin de copier quoi que ce soit : je lis directement le journal
`Documents\My Games\Skyrim Special Edition\Logs\Script\Papyrus.0.log` (toutes les lignes `[NPCHerbo]` et les erreurs).
Dis-moi simplement :
1. les nombres X / Y du point 1 ;
2. si la **méthode 0** marche (oui / non / partiellement) ;
3. tout comportement bizarre : elle reste plantée, tourne en rond, cueille dans les fermes, ralentissements…

## Commandes console utiles

| Commande | Effet |
|---|---|
| `set NPCH_HarvestMethod to 0` / `1` | méthode de cueillette (Faelia / joueur) |
| `set NPCH_HarvestEnabled to 0` / `1` | couper / reprendre la récolte |
| `set NPCH_HarvestRadius to 2500` | rayon de recherche (2000 par défaut ≈ 28 m) |
| `set NPCH_ReactionChance to 100` | % de chance de réaction vocale (60 par défaut, 0 = muette) |
| `set NPCH_Debug to 0` | masquer les notifications de debug |
| `set NPCH_BagCapacity to 5` | plafond temporaire pour tester la besace pleine |
