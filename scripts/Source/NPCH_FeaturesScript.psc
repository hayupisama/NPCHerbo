Scriptname NPCH_FeaturesScript extends Quest
{NPCHerbo v2 - fonctionnalites : savoir d'herboriste (lecon du jour, carnet, formation), filtres de cueillette
 (rares, preferences, recolte ciblee), nuit/meteo, depot a la maison, paliers de remplissage.
 Attache a NPCH_MainQuest a cote de NPCH_MainQuestScript.}

NPCH_MainQuestScript Property Main Auto

; --- reglages (globales, modifiables dans le MCM ou en console)
GlobalVariable Property NPCH_DailyLesson Auto      ; 1 = lecon du jour active
GlobalVariable Property NPCH_WeatherFlavour Auto   ; 1 = repliques et bonus nuit/meteo
GlobalVariable Property NPCH_LootCorpses Auto      ; 1 = preleve les ingredients sur les creatures mortes
GlobalVariable Property NPCH_RareAlert Auto        ; 1 = alerte + detour pour les plantes rares
GlobalVariable Property NPCH_RareOnly Auto         ; 1 = ne cueille que les ingredients rares
GlobalVariable Property NPCH_TargetEffect Auto     ; 0 tout, 1 soins, 2 magie, 3 vigueur, 4 poisons
GlobalVariable Property NPCH_DepositSet Auto       ; 1 = un coffre de depot est designe
GlobalVariable Property NPCH_LastHarvestSet Auto   ; 1 = un dernier ingredient cueilli est connu (dialogue d'exclusion)
GlobalVariable Property NPCH_ExclusionCount Auto   ; nombre d'ingredients exclus (dialogue "tout cueillir")

; --- donnees vanilla
FormList Property RareIngredients Auto             ; Nirnroot, Nirnroot cramoisie, racine de jarrin
MagicEffect Property EffRestoreHealth Auto
MagicEffect Property EffRestoreHealthAll Auto
MagicEffect Property EffRestoreMagicka Auto
MagicEffect Property EffRestoreMagickaAll Auto
MagicEffect Property EffRestoreStamina Auto
MagicEffect Property EffRestoreStaminaAll Auto
Keyword Property KwActorTypeAnimal Auto
Keyword Property KwActorTypeCreature Auto
FormList Property NirnrootActivators Auto          ; TreeFloraNirnroot01 + TreeFloraNirnrootRed01 (activateurs scriptes, pas des Flora)
Activator Property NirnrootRedActivator Auto       ; TreeFloraNirnrootRed01
Keyword Property LocTypePlayerHouse Auto
Ingredient Property IngNirnroot Auto
Ingredient Property IngNirnrootRed Auto
ReferenceAlias Property DepositChestAlias Auto
Message Property MsgDiscoveries Auto               ; "Faelia a decouvert %.0f sortes d'ingredients."
Message Property MsgSettings1 Auto                 ; menu de reglages integre, page 1 (boutons)
Message Property MsgSettings2 Auto                 ; page 2

Form Property LastHarvested Auto Hidden            ; dernier ingredient cueilli (dialogue d'exclusion, bonus meteo)

String Property DiscoveredKey = "NPCH_Discovered" AutoReadOnly Hidden
String Property ExcludedKey = "NPCH_Excluded" AutoReadOnly Hidden
String Property InfoKey = "NPCH_PlantInfo" AutoReadOnly Hidden
String Property IngKey = "NPCH_PlantIng" AutoReadOnly Hidden

; bits de la fiche d'une plante (mise en cache par base, StorageUtil)
Int Property INFO_KNOWN = 1 AutoReadOnly Hidden
Int Property INFO_HEAL = 2 AutoReadOnly Hidden
Int Property INFO_MAGICKA = 4 AutoReadOnly Hidden
Int Property INFO_STAMINA = 8 AutoReadOnly Hidden
Int Property INFO_POISON = 16 AutoReadOnly Hidden
Int Property INFO_RARE = 32 AutoReadOnly Hidden

Bool discoveriesSeeded = False
Int lastLessonDay = -1
Int lastWeatherClass = -1
Int lastNightDay = -1
Int lastFillThreshold = 0

; ================================================================ texte francais
; Les sources Papyrus sont compilees sans accents : on les reconstruit a l'execution dans la page de code du jeu.
; Jetons : {eac}=e aigu {egr}=e grave {eci}=e circonflexe {agr}=a grave {ced}=c cedille {oci}=o circonflexe {ugr}=u grave {ici}=i circonflexe

String Function Fr(String s)
	s = Rep(s, "{eci}", StringUtil.AsChar(234))
	s = Rep(s, "{eac}", StringUtil.AsChar(233))
	s = Rep(s, "{egr}", StringUtil.AsChar(232))
	s = Rep(s, "{agr}", StringUtil.AsChar(224))
	s = Rep(s, "{ced}", StringUtil.AsChar(231))
	s = Rep(s, "{oci}", StringUtil.AsChar(244))
	s = Rep(s, "{ugr}", StringUtil.AsChar(249))
	s = Rep(s, "{ici}", StringUtil.AsChar(238))
	Return s
EndFunction

String Function Rep(String s, String tok, String rep)
	Int i = StringUtil.Find(s, tok)
	While i >= 0
		String head = ""
		If i > 0
			head = StringUtil.Substring(s, 0, i) ; attention : Substring(s, 0, 0) renvoie toute la chaine
		EndIf
		s = head + rep + StringUtil.Substring(s, i + StringUtil.GetLength(tok))
		i = StringUtil.Find(s, tok, i + StringUtil.GetLength(rep))
	EndWhile
	Return s
EndFunction

Function Notify(String asFrText)
	Debug.Notification(Fr(asFrText))
EndFunction

; ================================================================ fiche d'une plante (cache par base)

; Ingredient principal donne par une base (Flora, Tree, Ingredient au sol, activateur de Nirnroot)
Form Function MainIngredient(Form akBase)
	Form cached = StorageUtil.GetFormValue(akBase, IngKey)
	If cached
		Return cached
	EndIf
	Form ing = None
	If akBase as Ingredient
		ing = akBase
	ElseIf akBase as Flora
		ing = FirstIngredient((akBase as Flora).GetIngredient(), 2)
	ElseIf akBase as TreeObject
		ing = FirstIngredient((akBase as TreeObject).GetIngredient(), 2)
	ElseIf NirnrootActivators.HasForm(akBase)
		If akBase == NirnrootRedActivator
			ing = IngNirnrootRed
		Else
			ing = IngNirnroot
		EndIf
	EndIf
	If ing
		StorageUtil.SetFormValue(akBase, IngKey, ing)
	EndIf
	Return ing
EndFunction

Form Function FirstIngredient(Form akProduce, Int aiDepth)
	If !akProduce
		Return None
	ElseIf akProduce as Ingredient
		Return akProduce
	ElseIf aiDepth > 0 && akProduce as LeveledItem
		LeveledItem lvl = akProduce as LeveledItem
		Int i = 0
		Int n = lvl.GetNumForms()
		While i < n
			Form found = FirstIngredient(lvl.GetNthForm(i), aiDepth - 1)
			If found
				Return found
			EndIf
			i += 1
		EndWhile
	EndIf
	Return None
EndFunction

Int Function PlantInfo(Form akBase)
	Int info = StorageUtil.GetIntValue(akBase, InfoKey)
	If info
		Return info
	EndIf
	info = INFO_KNOWN
	Ingredient ing = MainIngredient(akBase) as Ingredient
	If ing
		Int i = ing.GetNumEffects()
		While i > 0
			i -= 1
			MagicEffect fx = ing.GetNthEffectMagicEffect(i)
			If fx == EffRestoreHealth || fx == EffRestoreHealthAll
				info = Math.LogicalOr(info, INFO_HEAL)
			ElseIf fx == EffRestoreMagicka || fx == EffRestoreMagickaAll
				info = Math.LogicalOr(info, INFO_MAGICKA)
			ElseIf fx == EffRestoreStamina || fx == EffRestoreStaminaAll
				info = Math.LogicalOr(info, INFO_STAMINA)
			EndIf
		EndWhile
		MagicEffect topFx = ing.GetNthEffectMagicEffect(ing.GetCostliestEffectIndex())
		If topFx && topFx.IsEffectFlagSet(0x00000001) ; effet principal hostile : ingredient de poison
			info = Math.LogicalOr(info, INFO_POISON)
		EndIf
		If RareIngredients.HasForm(ing)
			info = Math.LogicalOr(info, INFO_RARE)
		EndIf
	EndIf
	StorageUtil.SetIntValue(akBase, InfoKey, info)
	Return info
EndFunction

Bool Function IsRare(Form akBase)
	Return Math.LogicalAnd(PlantInfo(akBase), INFO_RARE) != 0
EndFunction

; Preferences + recolte ciblee. Les plantes rares passent toujours le filtre "cible" (on ne rate pas une Nirnroot).
Bool Function PassesFilters(Form akBase)
	Form ing = MainIngredient(akBase)
	If ing && StorageUtil.FormListHas(None, ExcludedKey, ing)
		Return False
	EndIf
	Int info = PlantInfo(akBase)
	Bool rare = Math.LogicalAnd(info, INFO_RARE) != 0
	If NPCH_RareOnly.GetValueInt() == 1 && !rare
		Return False
	EndIf
	Int target = NPCH_TargetEffect.GetValueInt()
	If target <= 0 || rare
		Return True
	ElseIf target == 1
		Return Math.LogicalAnd(info, INFO_HEAL) != 0
	ElseIf target == 2
		Return Math.LogicalAnd(info, INFO_MAGICKA) != 0
	ElseIf target == 3
		Return Math.LogicalAnd(info, INFO_STAMINA) != 0
	EndIf
	Return Math.LogicalAnd(info, INFO_POISON) != 0
EndFunction

Bool Function IsExcluded(Form akIngredient)
	Return StorageUtil.FormListHas(None, ExcludedKey, akIngredient)
EndFunction

; ================================================================ creatures mortes

Bool Function IsLootableCorpse(Actor akActor)
	If !akActor || !akActor.IsDead() || akActor == Main.PlayerRef
		Return False
	EndIf
	If !akActor.HasKeyword(KwActorTypeAnimal) && !akActor.HasKeyword(KwActorTypeCreature)
		Return False ; jamais les humanoides : seulement les creatures
	EndIf
	Return CorpseHasIngredient(akActor)
EndFunction

Bool Function CorpseHasIngredient(ObjectReference akCorpse)
	Int i = akCorpse.GetNumItems()
	While i > 0
		i -= 1
		Form item = akCorpse.GetNthForm(i)
		If item as Ingredient && !IsExcluded(item)
			Return True
		EndIf
	EndWhile
	Return False
EndFunction

; Transfere les ingredients d'une creature morte dans la besace. Renvoie le nombre preleve.
Int Function LootCorpse(ObjectReference akCorpse)
	Int total = 0
	Int i = akCorpse.GetNumItems()
	While i > 0
		i -= 1
		Form item = akCorpse.GetNthForm(i)
		If item as Ingredient && !IsExcluded(item)
			Int n = akCorpse.GetItemCount(item)
			If n > 0
				akCorpse.RemoveItem(item, n, True, Main.BagChest)
				total += n
				Main.NotifyHarvest(item, n)
			EndIf
		EndIf
	EndWhile
	Return total
EndFunction

; ================================================================ apres chaque cueillette

Function OnHarvested(Form akItem, Int aiCount)
	LastHarvested = akItem
	NPCH_LastHarvestSet.SetValueInt(1)
	CheckDiscovery(akItem)
	; CheckFillThreshold est appele par la besace apres mise a jour de son compteur
EndFunction

; Carnet de decouvertes : premiere fois qu'un ingredient arrive dans la besace
Function CheckDiscovery(Form akItem)
	SeedDiscoveries()
	If StorageUtil.FormListHas(None, DiscoveredKey, akItem)
		Return
	EndIf
	StorageUtil.FormListAdd(None, DiscoveredKey, akItem, False)
	Int n = StorageUtil.FormListCount(None, DiscoveredKey)
	Notify("Faelia : nouvelle d{eac}couverte, " + akItem.GetName() + " ! (" + n + " sortes d'ingr{eac}dients)")
	Main.DebugMsg("Decouverte : " + akItem.GetName() + " (" + n + ")")
EndFunction

; Premiere utilisation : ce qui est deja dans la besace compte comme connu, sans notification
Function SeedDiscoveries()
	If discoveriesSeeded
		Return
	EndIf
	discoveriesSeeded = True
	ObjectReference bag = Main.BagChest
	Int i = bag.GetNumItems()
	While i > 0
		i -= 1
		Form item = bag.GetNthForm(i)
		If item as Ingredient
			StorageUtil.FormListAdd(None, DiscoveredKey, item, False)
		EndIf
	EndWhile
EndFunction

Int Function DiscoveryCount()
	SeedDiscoveries()
	Return StorageUtil.FormListCount(None, DiscoveredKey)
EndFunction

Function ShowDiscoveries()
	MsgDiscoveries.Show(DiscoveryCount())
EndFunction

; Remplissage de la besace : une notification en franchissant 50 %, 75 % puis 90 %
Function CheckFillThreshold()
	Int cap = Main.GetCapacity()
	If cap <= 0
		Return
	EndIf
	Int pct = (Main.NPCH_BagCount.GetValueInt() * 100) / cap
	Int threshold = 0
	If pct >= 90
		threshold = 90
	ElseIf pct >= 75
		threshold = 75
	ElseIf pct >= 50
		threshold = 50
	EndIf
	If threshold > lastFillThreshold
		Notify("Besace de Faelia remplie {agr} " + threshold + " % (" + Main.NPCH_BagCount.GetValueInt() + " / " + cap + ")")
	EndIf
	lastFillThreshold = threshold
EndFunction

; ================================================================ lecon du jour

; Une fois par jour de jeu : Faelia revele un effet inconnu d'un ingredient de sa besace.
Function TryDailyLesson()
	If NPCH_DailyLesson.GetValueInt() != 1
		Return
	EndIf
	Int today = Utility.GetCurrentGameTime() as Int
	If today <= lastLessonDay
		Return
	EndIf
	lastLessonDay = today ; une seule tentative par jour, meme sans ingredient a enseigner

	ObjectReference bag = Main.BagChest
	Ingredient[] pool = new Ingredient[128]
	Int n = 0
	Int i = bag.GetNumItems()
	While i > 0 && n < 128
		i -= 1
		Ingredient ing = bag.GetNthForm(i) as Ingredient
		If ing && HasUnknownEffect(ing)
			pool[n] = ing
			n += 1
		EndIf
	EndWhile
	If n == 0
		Return
	EndIf
	Ingredient chosen = pool[Utility.RandomInt(0, n - 1)]
	Int idx = chosen.LearnNextEffect()
	If idx < 0
		Return
	EndIf
	MagicEffect fx = chosen.GetNthEffectMagicEffect(idx)
	Notify("Le{ced}on de Faelia : " + chosen.GetName() + " - " + fx.GetName())
	Main.DebugMsg("Lecon du jour : " + chosen.GetName() + " / " + fx.GetName())
EndFunction

Bool Function HasUnknownEffect(Ingredient akIng)
	Int i = akIng.GetNumEffects()
	While i > 0
		i -= 1
		If !akIng.GetIsNthEffectKnown(i)
			Return True
		EndIf
	EndWhile
	Return False
EndFunction

; ================================================================ nuit et meteo

; Repliques d'ambiance : changement de meteo, tombee de la nuit (en exterieur seulement)
Function TryWeatherComment()
	If NPCH_WeatherFlavour.GetValueInt() != 1 || Main.PlayerRef.IsInInterior()
		Return
	EndIf
	Weather w = Weather.GetCurrentWeather()
	If w
		Int cls = w.GetClassification()
		If cls != lastWeatherClass
			If lastWeatherClass >= 0
				If cls == 2
					Notify("Faelia : La pluie fait sortir les plus belles pousses !")
				ElseIf cls == 3
					Notify("Faelia : Brr... sous la neige, les plantes se font discr{egr}tes.")
				EndIf
			EndIf
			lastWeatherClass = cls
		EndIf
	EndIf
	If IsNight()
		Int today = (Utility.GetCurrentGameTime() + 0.2) as Int ; une meme nuit (20 h -> 5 h) = un seul "jour"
		If today != lastNightDay
			lastNightDay = today
			Notify("Faelia : La nuit tombe... c'est l'heure des champignons !")
		EndIf
	EndIf
EndFunction

Bool Function IsNight()
	Float t = Utility.GetCurrentGameTime()
	Float hour = (t - (t as Int)) * 24.0
	Return hour >= 20.0 || hour < 5.0
EndFunction

; Bonus de cueillette : +1 sous la pluie (30 %) ou la nuit (20 %)
Function TryWeatherBonus(Form akItem)
	If NPCH_WeatherFlavour.GetValueInt() != 1 || !akItem || Main.PlayerRef.IsInInterior() || Main.IsBagFull()
		Return
	EndIf
	If RareIngredients.HasForm(akItem)
		Return ; pas de duplication des ingredients rares
	EndIf
	Int chance = 0
	String reason = ""
	Weather w = Weather.GetCurrentWeather()
	If w && w.GetClassification() == 2
		chance = 30
		reason = "pluie"
	ElseIf IsNight()
		chance = 20
		reason = "nuit"
	EndIf
	If chance > 0 && Utility.RandomInt(1, 100) <= chance
		Main.BagChest.AddItem(akItem, 1, True)
		Notify("Faelia : +1 " + akItem.GetName() + " (bonus " + reason + ")")
	EndIf
EndFunction

; ================================================================ plantes rares

Function AlertRare(ObjectReference akRef)
	If NPCH_RareAlert.GetValueInt() != 1
		Return
	EndIf
	Form ing = MainIngredient(akRef.GetBaseObject())
	String rareName = "une plante rare"
	If ing
		rareName = ing.GetName()
	EndIf
	Notify("Faelia : Oh ! " + rareName + ", par ici !")
	Main.GetFaelia().Say(Main.TopicFoundPlant)
	Main.DebugMsg("Plante rare reperee : " + rareName)
EndFunction

; ================================================================ dialogues

Function ExcludeLastHarvested()
	If !LastHarvested
		Return
	EndIf
	StorageUtil.FormListAdd(None, ExcludedKey, LastHarvested, False)
	NPCH_ExclusionCount.SetValueInt(StorageUtil.FormListCount(None, ExcludedKey))
	NPCH_LastHarvestSet.SetValueInt(0) ; l'option disparait jusqu'a la prochaine cueillette
	Notify("Faelia ne cueillera plus : " + LastHarvested.GetName())
EndFunction

Function ResetExclusions()
	StorageUtil.FormListClear(None, ExcludedKey)
	NPCH_ExclusionCount.SetValueInt(0)
	Notify("Faelia cueillera de nouveau tous les ingr{eac}dients.")
EndFunction

Function SetTargetEffect(Int aiTarget)
	NPCH_TargetEffect.SetValueInt(aiTarget)
	NPCH_RareOnly.SetValueInt(0)
EndFunction

Function SetRareOnly()
	NPCH_TargetEffect.SetValueInt(0)
	NPCH_RareOnly.SetValueInt(1)
EndFunction

Function ClearTargeting()
	NPCH_TargetEffect.SetValueInt(0)
	NPCH_RareOnly.SetValueInt(0)
EndFunction

; ================================================================ menu de reglages integre (sans SkyUI)

Function OpenSettings()
	Int page = 1
	While page > 0
		If page == 1
			Int b = MsgSettings1.Show(Main.NPCH_ScanRadius.GetValue() / 70.0, Main.NPCH_BatchMaxPlants.GetValue(), \
				Main.NPCH_ReactionChance.GetValue(), Main.NPCH_ShowHarvest.GetValue(), Main.NPCH_Debug.GetValue())
			If b == 0
				Main.NPCH_ScanRadius.SetValue(ClampF(Main.NPCH_ScanRadius.GetValue() - 350.0, 700.0, 4200.0))
			ElseIf b == 1
				Main.NPCH_ScanRadius.SetValue(ClampF(Main.NPCH_ScanRadius.GetValue() + 350.0, 700.0, 4200.0))
			ElseIf b == 2
				Main.NPCH_BatchMaxPlants.SetValue(ClampF(Main.NPCH_BatchMaxPlants.GetValue() - 1.0, 1.0, 20.0))
			ElseIf b == 3
				Main.NPCH_BatchMaxPlants.SetValue(ClampF(Main.NPCH_BatchMaxPlants.GetValue() + 1.0, 1.0, 20.0))
			ElseIf b == 4
				Int r = Main.NPCH_ReactionChance.GetValueInt()
				If r <= 0
					r = 30
				ElseIf r <= 30
					r = 60
				ElseIf r <= 60
					r = 100
				Else
					r = 0
				EndIf
				Main.NPCH_ReactionChance.SetValueInt(r)
			ElseIf b == 5
				Flip(Main.NPCH_ShowHarvest)
			ElseIf b == 6
				Flip(Main.NPCH_Debug)
			ElseIf b == 7
				page = 2
			Else
				page = 0
			EndIf
		Else
			Int b2 = MsgSettings2.Show(Main.NPCH_HarvestEnabled.GetValue(), NPCH_LootCorpses.GetValue(), \
				NPCH_DailyLesson.GetValue(), NPCH_WeatherFlavour.GetValue(), NPCH_RareAlert.GetValue())
			If b2 == 0
				Main.SetHarvestEnabled(Main.NPCH_HarvestEnabled.GetValueInt() != 1)
			ElseIf b2 == 1
				Flip(NPCH_LootCorpses)
			ElseIf b2 == 2
				Flip(NPCH_DailyLesson)
			ElseIf b2 == 3
				Flip(NPCH_WeatherFlavour)
			ElseIf b2 == 4
				Flip(NPCH_RareAlert)
			ElseIf b2 == 5
				page = 1
			Else
				page = 0
			EndIf
		EndIf
	EndWhile
EndFunction

Function Flip(GlobalVariable akGlobal)
	If akGlobal.GetValueInt() == 1
		akGlobal.SetValueInt(0)
	Else
		akGlobal.SetValueInt(1)
	EndIf
EndFunction

Float Function ClampF(Float afValue, Float afMin, Float afMax)
	If afValue < afMin
		Return afMin
	ElseIf afValue > afMax
		Return afMax
	EndIf
	Return afValue
EndFunction

Function OpenTraining()
	Game.ShowTrainingMenu(Main.GetFaelia())
EndFunction

; ================================================================ depot a la maison

; Designe le coffre le plus proche du joueur, s'il est chez lui (proprietaire = joueur ou cellule du joueur)
Function SetDepositChest()
	Actor p = Main.PlayerRef
	ObjectReference[] boxes = PO3_SKSEFunctions.FindAllReferencesOfFormType(p, 28, 400.0)
	ObjectReference best = None
	Float bestDist = 1000000.0
	Int i = 0
	While i < boxes.Length
		ObjectReference c = boxes[i]
		If c && c != Main.BagChest && IsPlayerOwned(c)
			Float d = p.GetDistance(c)
			If d < bestDist
				bestDist = d
				best = c
			EndIf
		EndIf
		i += 1
	EndWhile
	If !best
		Notify("Faelia : Je ne vois pas de coffre {agr} vous ici. Approchez-vous d'un coffre chez vous.")
		Return
	EndIf
	DepositChestAlias.ForceRefTo(best)
	NPCH_DepositSet.SetValueInt(1)
	Notify("Faelia : C'est not{eac} ! Je d{eac}poserai mes trouvailles dans ce coffre.")
EndFunction

Bool Function IsPlayerOwned(ObjectReference akRef)
	Location here = Main.PlayerRef.GetCurrentLocation()
	If here && here.HasKeyword(LocTypePlayerHouse)
		Return True ; maison du joueur (vanilla, Hearthfire et la plupart des maisons de mods)
	EndIf
	ActorBase playerBase = Main.PlayerRef.GetActorBase()
	If akRef.GetActorOwner() == playerBase
		Return True
	EndIf
	Cell c = akRef.GetParentCell()
	If c && c.GetActorOwner() == playerBase
		Return True
	EndIf
	Faction playerFaction = Game.GetFormFromFile(0x00000DB1, "Skyrim.esm") as Faction
	Return akRef.GetFactionOwner() == playerFaction || (c && c.GetFactionOwner() == playerFaction)
EndFunction

Function DepositAll()
	ObjectReference chest = DepositChestAlias.GetReference()
	If !chest
		NPCH_DepositSet.SetValueInt(0)
		Return
	EndIf
	Int before = Main.NPCH_BagCount.GetValueInt()
	Main.BagChest.RemoveAllItems(chest, False, True)
	Notify("Faelia a d{eac}pos{eac} " + before + " ingr{eac}dients dans votre coffre.")
	lastFillThreshold = 0
EndFunction
