Scriptname NPCH_MainQuestScript extends Quest
{NPCHerbo - etat global de Faelia : installation, appel/renvoi, teleportation, journal de debug.}

Actor Property PlayerRef Auto
ReferenceAlias Property FaeliaAlias Auto
ReferenceAlias Property PlayerAlias Auto
ReferenceAlias Property HarvestTargetAlias Auto  ; plante visee : cible du package de recolte
GlobalVariable Property NPCH_Harvesting Auto     ; 1 = package de recolte actif
GlobalVariable Property NPCH_ReactionChance Auto ; % de chance qu'elle commente une plante trouvee (0 = muette)
Topic Property TopicFoundPlant Auto              ; "Regardez ca !", "Oh, regardez toutes ces couleurs !"...
Topic Property TopicHarvestDone Auto             ; "Parfois ca paye de regarder dans le moindre recoin."
ObjectReference Property BagChest Auto
ObjectReference Property HoldingMarker Auto
Spell Property PowerSummon Auto
Idle Property IdleHarvest Auto
FormList Property HarvestableList Auto  ; rempli au chargement : toutes les Flora/Tree qui donnent un ingredient
Message Property MsgBagStatus Auto      ; "Besace de Faelia : %.0f / %.0f ingredients"
Message Property MsgInstalled Auto
Message Property MsgSummon Auto
Message Property MsgDismiss Auto
Message Property MsgBagFull Auto
Message Property MsgRefused Auto
Message Property MsgNoRoom Auto
Message Property MsgUpgraded Auto       ; "Faelia a un nouveau sac : %.0f places."
Float Property SpawnDistance = 300.0 Auto  ; apparition derriere le joueur (hors de son champ de vision)
MiscObject Property Gold001 Auto
NPCH_FeaturesScript Property Features Auto  ; fonctionnalites v2 (meme quete)

GlobalVariable Property NPCH_Following Auto
GlobalVariable Property NPCH_HarvestEnabled Auto
GlobalVariable Property NPCH_ScanRadius Auto        ; rayon du scan, centre sur le joueur
GlobalVariable Property NPCH_BatchStartDist Auto    ; 1re plante d'une tournee : a moins de X du joueur (15 m)
GlobalVariable Property NPCH_BatchLinkDist Auto     ; distance max entre deux plantes d'une meme tournee
GlobalVariable Property NPCH_BatchMaxPlants Auto    ; plantes max par tournee
GlobalVariable Property NPCH_BatchMaxTime Auto      ; duree max d'une tournee (secondes)
GlobalVariable Property NPCH_LeashDistance Auto
GlobalVariable Property NPCH_HarvestMethod Auto  ; 0 = Faelia active la plante, 1 = activation par le joueur puis transfert
GlobalVariable Property NPCH_Debug Auto
GlobalVariable Property NPCH_ShowHarvest Auto  ; 1 = notification a chaque cueillette ("Faelia : +3 Lys des cimes")
GlobalVariable Property NPCH_BagTier Auto      ; palier du sac : 0..4
GlobalVariable Property NPCH_BagCapacity Auto  ; capacite en nombre d'ingredients
GlobalVariable Property NPCH_BagCount Auto     ; nombre d'ingredients dans la besace

Bool Property FullWarned = False Auto Hidden

; Les valeurs des globales sont conservees dans la sauvegarde : un nouveau reglage par defaut
; doit etre applique par migration. Incrementer SETTINGS_VERSION et ajouter un palier dans MigrateSettings().
Int Property SETTINGS_VERSION = 3 AutoReadOnly Hidden
Int settingsVersion = 0

; Capture des objets recus pendant une cueillette : 0 = aucune, 1 = inventaire de Faelia, 2 = inventaire du joueur
Int Property CaptureMode = 0 Auto Hidden
Int Property HarvestGained = 0 Auto Hidden

; ---------------------------------------------------------------- installation

Event OnInit()
	RegisterForSingleUpdate(3.0)
EndEvent

Event OnUpdate()
	If !PlayerRef.HasSpell(PowerSummon)
		PlayerRef.AddSpell(PowerSummon, False)
		MsgInstalled.Show()
	EndIf
	settingsVersion = SETTINGS_VERSION ; installation neuve : les valeurs de l'ESP sont deja les bonnes
	RecountBag()
	BuildHarvestableList()
EndEvent

Function MigrateSettings()
	If settingsVersion < 1
		NPCH_ScanRadius.SetValue(1950.0) ; +30 % (etait 1500)
	EndIf
	If settingsVersion < 2
		NPCH_Debug.SetValueInt(0) ; fin des tests : plus de notifications de debug (le journal Papyrus reste complet)
	EndIf
	If settingsVersion < 3
		NPCH_Debug.SetValueInt(1) ; demande utilisateur : logs a l'ecran actifs par defaut (desactivables dans les reglages)
	EndIf
	If settingsVersion != SETTINGS_VERSION
		DebugMsg("Reglages migres : v" + settingsVersion + " -> v" + SETTINGS_VERSION)
		settingsVersion = SETTINGS_VERSION
	EndIf
EndFunction

Function OnGameLoaded()
	DebugMsg("Chargement de partie, suivi=" + NPCH_Following.GetValueInt())
	MigrateSettings()
	RecountBag()
	CaptureMode = 0
	If IsFollowing()
		; d'abord changer de generation (une tournee reprise depuis la sauvegarde s'arretera d'elle-meme),
		; puis remettre a zero le package de recolte qu'elle aurait pu laisser actif
		(FaeliaAlias as NPCH_FaeliaAliasScript).StartLoop()
		GetFaelia().IgnoreFriendlyHits(True)
	EndIf
	EndHarvestTravel()
	; la liste est reconstruite a chaque chargement : elle suit les ajouts/retraits de mods de flore
	BuildHarvestableList()
EndFunction

; ---------------------------------------------------------------- besace

Int Function CapacityForTier(Int aiTier)
	If aiTier <= 0
		Return 300
	ElseIf aiTier == 1
		Return 600
	ElseIf aiTier == 2
		Return 1000
	ElseIf aiTier == 3
		Return 1500
	EndIf
	Return 2500
EndFunction

Int Function GetCapacity()
	Return NPCH_BagCapacity.GetValueInt()
EndFunction

Bool Function IsBagFull()
	Return NPCH_BagCount.GetValueInt() >= GetCapacity()
EndFunction

; recomptage complet (au chargement) : corrige toute derive du compteur
Function RecountBag()
	NPCH_BagCapacity.SetValueInt(CapacityForTier(NPCH_BagTier.GetValueInt()))
	Int total = 0
	Int i = BagChest.GetNumItems()
	While i > 0
		i -= 1
		Form item = BagChest.GetNthForm(i)
		If item as Ingredient
			total += BagChest.GetItemCount(item)
		EndIf
	EndWhile
	NPCH_BagCount.SetValueInt(total)
	DebugMsg("Besace : " + total + " / " + GetCapacity())
EndFunction

Function ShowBagStatus()
	MsgBagStatus.Show(NPCH_BagCount.GetValueInt(), GetCapacity())
EndFunction

Function OpenBag()
	ShowBagStatus()
	BagChest.Activate(PlayerRef)
EndFunction

; Prix pour atteindre un palier. DOIT rester synchronise avec BagTiers dans le generateur (Dialogues.cs).
Int Function PriceForTier(Int aiTier)
	If aiTier == 1
		Return 1000
	ElseIf aiTier == 2
		Return 3000
	ElseIf aiTier == 3
		Return 7500
	EndIf
	Return 15000
EndFunction

Function UpgradeBag()
	Int tier = NPCH_BagTier.GetValueInt()
	If tier >= 4
		Return
	ElseIf tier < 0
		tier = 0 ; reglage console absurde
	EndIf
	Int price = PriceForTier(tier + 1)
	If PlayerRef.GetItemCount(Gold001) < price
		Return ; garde-fou : la condition du dialogue l'a normalement deja verifie
	EndIf
	PlayerRef.RemoveItem(Gold001, price)
	NPCH_BagTier.SetValueInt(tier + 1)
	NPCH_BagCapacity.SetValueInt(CapacityForTier(tier + 1))
	FullWarned = False
	DebugMsg("Sac ameliore : palier " + (tier + 1) + ", " + GetCapacity() + " places (-" + price + " po)")
	MsgUpgraded.Show(GetCapacity())
EndFunction

Function SetHarvestEnabled(Bool abEnabled)
	If abEnabled
		NPCH_HarvestEnabled.SetValueInt(1)
	Else
		NPCH_HarvestEnabled.SetValueInt(0)
		EndHarvestTravel() ; si elle etait en route vers une plante, elle revient tout de suite
	EndIf
EndFunction

; ---------------------------------------------------------------- catalogue des plantes recoltables

Int lastFloraFormCount = -1
Int lastTreeFormCount = -1

; Reconstruit le catalogue seulement si le nombre de Flora/Tree du jeu a change (mods ajoutes/retires) :
; la reconstruction coute des milliers d'appels natifs, inutile a chaque chargement.
Function BuildHarvestableList()
	Form[] floras = PO3_SKSEFunctions.GetAllForms(39)
	Form[] trees = PO3_SKSEFunctions.GetAllForms(38)
	If floras.Length == lastFloraFormCount && trees.Length == lastTreeFormCount && HarvestableList.GetSize() > 0
		DebugMsg("Catalogue inchange : " + HarvestableList.GetSize() + " plantes recoltables")
		Return
	EndIf
	HarvestableList.Revert()
	Int nFlora = AddHarvestable(floras, True)
	Int nTree = AddHarvestable(trees, False)
	lastFloraFormCount = floras.Length
	lastTreeFormCount = trees.Length
	DebugMsg("Plantes recoltables connues : " + nFlora + " flores, " + nTree + " plantes/arbres")
EndFunction

; Une liste nivelee n'est acceptee que si elle peut donner un ingredient (exclut bourses d'or, nourriture...)
Bool Function LeveledHasIngredient(LeveledItem akList, Int aiDepth)
	Int i = akList.GetNumForms()
	While i > 0
		i -= 1
		Form entry = akList.GetNthForm(i)
		If entry as Ingredient
			Return True
		ElseIf aiDepth > 0 && entry as LeveledItem
			If LeveledHasIngredient(entry as LeveledItem, aiDepth - 1)
				Return True
			EndIf
		EndIf
	EndWhile
	Return False
EndFunction

Int Function AddHarvestable(Form[] akForms, Bool abFlora)
	Int added = 0
	Int i = 0
	While i < akForms.Length
		Form produceForm = None
		If abFlora
			produceForm = (akForms[i] as Flora).GetIngredient()
		Else
			produceForm = (akForms[i] as TreeObject).GetIngredient()
		EndIf
		Bool ok = produceForm as Ingredient
		If !ok && produceForm as LeveledItem
			ok = LeveledHasIngredient(produceForm as LeveledItem, 2)
		EndIf
		If ok
			HarvestableList.AddForm(akForms[i])
			added += 1
		EndIf
		i += 1
	EndWhile
	Return added
EndFunction

; ---------------------------------------------------------------- appel / renvoi

Actor Function GetFaelia()
	Return FaeliaAlias.GetActorRef()
EndFunction

Bool Function IsFollowing()
	Return NPCH_Following.GetValueInt() == 1
EndFunction

Function ToggleFaelia()
	If IsFollowing()
		Dismiss()
	Else
		Summon()
	EndIf
EndFunction

Function Summon()
	Actor f = GetFaelia()
	EndHarvestTravel() ; aucune cible perimee d'une ancienne tournee
	NPCH_Following.SetValueInt(1)
	TeleportToPlayer(f)
	f.Enable()
	f.IgnoreFriendlyHits(True) ; un coup ou un sort de zone accidentel ne la rend pas hostile
	; le bonus de vitesse (SpeedMult) n'est applique par le moteur qu'au prochain changement de poids porte
	f.ModActorValue("CarryWeight", 0.1)
	f.ModActorValue("CarryWeight", -0.1)
	f.EvaluatePackage()
	(FaeliaAlias as NPCH_FaeliaAliasScript).StartLoop()
	MsgSummon.Show()
EndFunction

Function Dismiss()
	Actor f = GetFaelia()
	NPCH_Following.SetValueInt(0)
	(FaeliaAlias as NPCH_FaeliaAliasScript).StopLoop()
	EndHarvestTravel()
	f.MoveTo(HoldingMarker)
	f.Disable()
	MsgDismiss.Show()
EndFunction

Function TeleportToPlayer(Actor f)
	If PlayerRef.IsInInterior()
		f.MoveTo(PlayerRef) ; en interieur, 300 unites derriere peuvent tomber dans un mur
		Return
	EndIf
	Float a = PlayerRef.GetAngleZ()
	; derriere le joueur, hors de son champ de vision
	f.MoveTo(PlayerRef, -SpawnDistance * Math.Sin(a), -SpawnDistance * Math.Cos(a), 0.0, True)
EndFunction

; Voyage rapide : elle arrive avec le joueur, sans attendre le prochain tick de la boucle
Function OnPlayerFastTravelEnd()
	If IsFollowing()
		DebugMsg("Voyage rapide : Faelia rejoint le joueur")
		(FaeliaAlias as NPCH_FaeliaAliasScript).StartLoop() ; nouvelle generation : la tournee d'avant le voyage s'arrete
		EndHarvestTravel()
		TeleportToPlayer(GetFaelia())
		GetFaelia().EvaluatePackage()
	EndIf
EndFunction

; ---------------------------------------------------------------- recolte : package de deplacement

Function BeginHarvestTravel(ObjectReference akTarget)
	HarvestTargetAlias.ForceRefTo(akTarget)
	NPCH_Harvesting.SetValueInt(1)
	GetFaelia().EvaluatePackage()
EndFunction

Function EndHarvestTravel()
	NPCH_Harvesting.SetValueInt(0)
	HarvestTargetAlias.Clear()
	GetFaelia().EvaluatePackage()
EndFunction

; ---------------------------------------------------------------- recolte (compteur partage)

Function BeginCapture(Int aiMode)
	HarvestGained = 0
	CaptureMode = aiMode
EndFunction

Int Function EndCapture()
	CaptureMode = 0
	Return HarvestGained
EndFunction

Function OnCaptured(ObjectReference akFrom, Form akItem, Int aiCount)
	akFrom.RemoveItem(akItem, aiCount, True, BagChest)
	HarvestGained += aiCount
	NotifyHarvest(akItem, aiCount)
EndFunction

; Toujours ecrit dans le journal ; affiche a l'ecran sauf si NPCH_ShowHarvest = 0 (independant du mode debug)
Function NotifyHarvest(Form akItem, Int aiCount)
	Debug.Trace("[NPCHerbo] +" + aiCount + " " + akItem.GetName())
	If NPCH_ShowHarvest.GetValueInt() == 1
		Debug.Notification("Faelia : +" + aiCount + " " + akItem.GetName())
	EndIf
	Features.OnHarvested(akItem, aiCount) ; carnet de decouvertes, paliers de remplissage, dernier ingredient
EndFunction

; ---------------------------------------------------------------- debug

Function DebugMsg(String asMsg)
	Debug.Trace("[NPCHerbo] " + asMsg)
	If NPCH_Debug.GetValueInt() == 1
		Debug.Notification("[Faelia] " + asMsg)
	EndIf
EndFunction
