Scriptname NPCH_FaeliaAliasScript extends ReferenceAlias
{NPCHerbo - boucle principale de Faelia : teleportation + recolte par tournees.
 Une tournee = un seul scan centre sur le joueur, une chaine de plantes voisines (plus proche voisin),
 recoltees d'affilee sans repasser par le suivi, puis un seul retour vers le joueur.}

; ---------------------------------------------------------------- reglages fins
; (les reglages principaux sont des globales modifiables en console : NPCH_ScanRadius, NPCH_BatchStartDist,
;  NPCH_BatchLinkDist, NPCH_BatchMaxPlants, NPCH_BatchMaxTime)
Float Property TickInterval = 2.0 Auto          ; frequence de scan quand il n'y a rien a recolter
Float Property PostBatchPause = 4.0 Auto        ; pause apres une tournee (elle revient vers le joueur)
Float Property ReachDistance = 200.0 Auto       ; distance a laquelle elle peut cueillir
Float Property TimeoutBase = 3.0 Auto           ; abandon d'une cible : base + distance x TimeoutPerUnit
Float Property TimeoutPerUnit = 0.004 Auto      ; soit 1 s par 250 unites
Float Property TimeoutMax = 15.0 Auto
Float Property StuckSeconds = 1.5 Auto          ; sans se rapprocher de la plante pendant X s = bloquee
Float Property StuckProgress = 30.0 Auto        ; rapprochement minimal (unites) pour ne pas etre consideree bloquee
Int Property MaxUnstickAttempts = 3 Auto        ; tentatives de deblocage avant d'abandonner la plante
Float Property UnstickStep = 80.0 Auto          ; amplitude du petit saut de cote
Float Property PlayerExclusion = 150.0 Auto     ; ignore les plantes collees au joueur (il est peut-etre en train de les cueillir)
Float Property PlayerLeashRatio = 0.65 Auto     ; tournee interrompue si elle est a plus de 65 % de la distance de teleportation
Float Property MaxHeightDiff = 400.0 Auto       ; evite les plantes en haut des falaises
Float Property PickWait = 0.6 Auto              ; animation avant la cueillette
Float Property CaptureWait = 0.8 Auto           ; delai de reception des ingredients
Float Property ReactionCooldown = 15.0 Auto     ; secondes minimum entre deux reactions vocales
Float Property RevisitDays = 10.0 Auto          ; delai avant de retenter une plante deja visitee
Int Property MaxVisited = 2000 Auto

String Property VisitedKey = "NPCH_Visited" Auto Hidden
String Property VisitedTimeKey = "NPCH_VisitedTime" Auto Hidden

; Codes de retour de HarvestOne
Int Property RESULT_INTERRUPTED = -1 AutoReadOnly Hidden
Int Property RESULT_UNREACHABLE = -2 AutoReadOnly Hidden

; Garde de reentrance : chaque StartLoop/StopLoop change de generation. Une boucle d'une generation
; perimee (ex. pile reprise apres un chargement de partie) s'arrete d'elle-meme a son prochain controle.
Int loopGen = 0
Int runningGen = -1          ; generation de la tournee en cours (-1 = aucune). Un verrou d'une generation perimee
                             ; (pile jetee au chargement) est ignore : il ne peut pas bloquer la boucle pour toujours.
Float lastReactionTime = 0.0
Float lastPurgeTime = 0.0    ; temps de jeu (jours) de la derniere purge des plantes visitees

NPCH_MainQuestScript Function Main()
	Return GetOwningQuest() as NPCH_MainQuestScript
EndFunction

NPCH_FeaturesScript Function Feat()
	Return GetOwningQuest() as NPCH_FeaturesScript
EndFunction

; ---------------------------------------------------------------- boucle

Function StartLoop()
	loopGen += 1
	PurgeVisited()
	RegisterForSingleUpdate(1.0)
EndFunction

Function StopLoop()
	loopGen += 1
	UnregisterForUpdate()
EndFunction

Bool Function IsCurrent(Int aiGen)
	Return aiGen == loopGen
EndFunction

Event OnUpdate()
	If runningGen == loopGen
		RegisterForSingleUpdate(1.0) ; une tournee de cette generation est encore en cours : on repasse plus tard
		Return
	EndIf
	Int myGen = loopGen
	NPCH_MainQuestScript q = Main()
	If !q.IsFollowing()
		Return
	EndIf
	runningGen = myGen
	Actor f = GetActorRef()
	Actor p = q.PlayerRef

	CheckTeleport(q, f, p)
	If Utility.GetCurrentGameTime() - lastPurgeTime > 1.0
		PurgeVisited() ; une fois par jour de jeu, meme en longue session
	EndIf

	Int handled = -1 ; -1 = pas de tournee
	If q.NPCH_HarvestEnabled.GetValueInt() == 1 && !p.IsInCombat() && !f.IsInCombat() && f.Is3DLoaded()
		handled = RunBatch(q, f, p, myGen)
	EndIf
	If handled < 0 && IsCurrent(myGen) && q.NPCH_Harvesting.GetValueInt() == 1
		q.EndHarvestTravel() ; filet de securite : jamais de package de recolte actif hors tournee
	EndIf
	If IsCurrent(myGen) && !p.IsInCombat()
		NPCH_FeaturesScript ft = Feat()
		ft.TryWeatherComment()
		ft.TryDailyLesson()
	EndIf
	If runningGen == myGen
		runningGen = -1
	EndIf

	If IsCurrent(myGen) && q.IsFollowing()
		If handled >= 0
			RegisterForSingleUpdate(PostBatchPause)
		Else
			RegisterForSingleUpdate(TickInterval)
		EndIf
	EndIf
EndEvent

; ---------------------------------------------------------------- teleportation

Function CheckTeleport(NPCH_MainQuestScript q, Actor f, Actor p)
	Bool otherSpace = f.GetWorldSpace() != p.GetWorldSpace()
	Bool otherInterior = (p.IsInInterior() || f.IsInInterior()) && f.GetParentCell() != p.GetParentCell()
	Bool tooFar = f.GetDistance(p) > q.NPCH_LeashDistance.GetValue()
	If otherSpace || otherInterior || tooFar || !f.Is3DLoaded()
		q.DebugMsg("Teleportation vers le joueur")
		q.TeleportToPlayer(f)
		f.EvaluatePackage()
	EndIf
EndFunction

; ---------------------------------------------------------------- tournee

; Renvoie le nombre de plantes traitees, ou -1 si aucune tournee n'a ete lancee.
Int Function RunBatch(NPCH_MainQuestScript q, Actor f, Actor p, Int myGen)
	Cell pc = p.GetParentCell()
	If pc && (pc.GetActorOwner() || pc.GetFactionOwner())
		Return -1 ; lieu prive (maison, ferme...) : on ne touche a rien
	EndIf
	If q.IsBagFull()
		If !q.FullWarned
			q.FullWarned = True
			q.MsgBagFull.Show()
		EndIf
		Return -1
	EndIf
	q.FullWarned = False

	; 1. un seul scan, centre sur le joueur
	ObjectReference[] cand = new ObjectReference[128]
	Float[] cx = new Float[128]
	Float[] cy = new Float[128]
	Float[] cz = new Float[128]
	Bool[] rare = new Bool[128]
	Int n = ScanCandidates(q, p, cand, cx, cy, cz, rare)
	If n == 0
		Return -1
	EndIf

	; 2. chaine de plantes voisines (calcul pur, sans appel au moteur). Une plante rare, si presente, ouvre la tournee.
	Int[] order = new Int[128]
	Int k = BuildChain(q, f, p, n, cx, cy, cz, rare, order)
	If k == 0
		Return -1
	EndIf
	q.DebugMsg("Tournee : " + k + " cible(s) (" + n + " candidates)")
	If rare[order[0]]
		Feat().AlertRare(cand[order[0]]) ; detour annonce : "Oh ! Nirnroot, par ici !"
	Else
		React(f, q.TopicFoundPlant, q.NPCH_ReactionChance.GetValueInt())
	EndIf

	; 3. execution : on garde le package de recolte actif et on change seulement de cible
	Float startTime = Utility.GetCurrentRealTime()
	Float maxTime = ClampPositive(q.NPCH_BatchMaxTime.GetValue(), 30.0)
	Int handled = 0
	Int gainedTotal = 0
	String stopReason = "fin de liste"
	Int i = 0
	While i < k
		ObjectReference tgt = cand[order[i]]
		If !BatchMayContinue(q, f, p, myGen)
			stopReason = "interrompue"
			i = k
		ElseIf Utility.GetCurrentRealTime() - startTime > maxTime
			stopReason = "temps ecoule"
			i = k
		Else
			If !tgt.IsDisabled() && tgt.Is3DLoaded()
				Int res = HarvestOne(q, f, p, tgt, myGen)
				If res == RESULT_INTERRUPTED
					stopReason = "interrompue"
					i = k
				Else
					handled += 1
					If res > 0
						gainedTotal += res
					EndIf
				EndIf
			EndIf
			i += 1
		EndIf
	EndWhile

	If IsCurrent(myGen)
		q.EndHarvestTravel() ; un seul retour au suivi, en fin de tournee
	EndIf
	q.DebugMsg("Tournee terminee (" + stopReason + ") : " + handled + "/" + k + " plantes, +" + gainedTotal + " ingredients")
	If gainedTotal > 0
		React(f, q.TopicHarvestDone, q.NPCH_ReactionChance.GetValueInt() / 2)
	EndIf
	Return handled
EndFunction

Bool Function BatchMayContinue(NPCH_MainQuestScript q, Actor f, Actor p, Int myGen)
	If !IsCurrent(myGen) || !q.IsFollowing() || q.NPCH_HarvestEnabled.GetValueInt() != 1
		Return False
	EndIf
	If p.IsInCombat() || f.IsInCombat() || q.IsBagFull()
		Return False
	EndIf
	Return f.GetDistance(p) <= q.NPCH_LeashDistance.GetValue() * PlayerLeashRatio
EndFunction

; Deplacement + cueillette d'une plante. Renvoie le nombre d'ingredients obtenus (>= 0),
; RESULT_UNREACHABLE (cible marquee visitee) ou RESULT_INTERRUPTED (rien n'est marque).
Int Function HarvestOne(NPCH_MainQuestScript q, Actor f, Actor p, ObjectReference tgt, Int myGen)
	Form base = tgt.GetBaseObject()
	Bool isLoose = (base as Ingredient) != None   ; ingredient pose au sol
	Bool isCorpse = (tgt as Actor) != None        ; creature morte
	Bool isNirn = Feat().NirnrootActivators.HasForm(base) ; activateur scripte de Nirnroot (attention : Flora herite d'Activator)
	If !IsCurrent(myGen)
		Return RESULT_INTERRUPTED
	EndIf
	q.BeginHarvestTravel(tgt) ; ForceRefTo + package actif : pas de passage par le suivi entre deux plantes

	Float timeout = TimeoutBase + f.GetDistance(tgt) * TimeoutPerUnit
	If timeout > TimeoutMax
		timeout = TimeoutMax
	EndIf
	Float waited = 0.0
	; Detection de blocage : si elle ne se rapproche pas de la plante pendant StuckSeconds, petit saut de cote puis
	; recalcul de l'itineraire. Apres MaxUnstickAttempts tentatives, la plante est abandonnee.
	Int attempts = 0
	Float bestDist = f.GetDistance(tgt)
	Float stuckTime = 0.0
	While f.GetDistance(tgt) > ReachDistance && waited < timeout
		If !BatchMayContinue(q, f, p, myGen)
			Return RESULT_INTERRUPTED
		EndIf
		Utility.Wait(0.25)
		If !f.IsInDialogueWithPlayer()
			waited += 0.25 ; le temps passe a lui parler ne compte pas
			Float d = f.GetDistance(tgt)
			If d < bestDist - StuckProgress
				bestDist = d
				stuckTime = 0.0
			Else
				stuckTime += 0.25
			EndIf
			If stuckTime >= StuckSeconds && d > ReachDistance
				attempts += 1
				If attempts > MaxUnstickAttempts
					waited = timeout ; abandon : traite comme injoignable juste apres
				Else
					q.DebugMsg("Bloquee (essai " + attempts + "/" + MaxUnstickAttempts + ") : " + base.GetName())
					Unstick(f, tgt, attempts)
					If !IsCurrent(myGen)
						Return RESULT_INTERRUPTED
					EndIf
					q.BeginHarvestTravel(tgt) ; reaffecte la cible : le moteur recalcule l'itineraire depuis la nouvelle position
					bestDist = f.GetDistance(tgt)
					stuckTime = 0.0
					waited = 0.0 ; chaque tentative a droit a son propre delai
				EndIf
			EndIf
		EndIf
	EndWhile
	If !IsCurrent(myGen)
		Return RESULT_INTERRUPTED
	EndIf
	If f.GetDistance(tgt) > ReachDistance
		If !isLoose
			MarkVisited(tgt) ; vrai timeout : on ne la retentera pas avant RevisitDays
		EndIf
		q.DebugMsg("Injoignable : " + base.GetName() + " (" + waited + " s)")
		Return RESULT_UNREACHABLE
	EndIf

	If q.IdleHarvest
		f.PlayIdle(q.IdleHarvest)
	EndIf
	Utility.Wait(PickWait)
	If !IsCurrent(myGen)
		Return RESULT_INTERRUPTED
	EndIf

	Int gained = 0
	If isLoose
		; ingredient pose au sol : il part directement dans la besace
		q.BagChest.AddItem(tgt, 1, True)
		gained = 1
		q.NotifyHarvest(base, 1)
	ElseIf isCorpse
		gained = Feat().LootCorpse(tgt)
	ElseIf isNirn
		; le script vanilla de la Nirnroot donne l'ingredient a l'acteur qui l'active (TriggerRef.AddItem) :
		; Faelia l'active, l'ingredient arrive dans son inventaire et son alias le transfere dans la besace
		q.BeginCapture(1)
		tgt.Activate(f)
		Utility.Wait(CaptureWait)
		gained = q.EndCapture()
	ElseIf q.NPCH_HarvestMethod.GetValueInt() == 0
		q.BeginCapture(1)
		tgt.Activate(f)
		Utility.Wait(CaptureWait)
		gained = q.EndCapture()
	Else
		q.BeginCapture(2)
		tgt.Activate(p)
		Utility.Wait(CaptureWait)
		gained = q.EndCapture()
	EndIf
	If gained == 0
		q.DebugMsg("Rien obtenu de " + base.GetName())
	ElseIf !isLoose && !isCorpse
		Feat().TryWeatherBonus(Feat().LastHarvested) ; +1 sous la pluie ou la nuit (selon les chances)
	EndIf

	If !isLoose
		MarkVisited(tgt) ; marquee seulement une fois traitee (les ingredients ramasses disparaissent d'eux-memes)
	EndIf
	Debug.SendAnimationEvent(f, "IdleForceDefaultState")
	Return gained
EndFunction

; Petit saut pour changer de point de depart : essai 1 a gauche, essai 2 a droite, essai 3 en arriere
; (perpendiculaire / oppose a la direction de la plante). Le moteur recalcule ensuite le chemin.
Function Unstick(Actor f, ObjectReference tgt, Int aiAttempt)
	Float dx = tgt.GetPositionX() - f.GetPositionX()
	Float dy = tgt.GetPositionY() - f.GetPositionY()
	Float len = Math.Sqrt(dx * dx + dy * dy)
	If len < 1.0
		len = 1.0
	EndIf
	dx = dx / len
	dy = dy / len
	Float ox = 0.0
	Float oy = 0.0
	If aiAttempt == 1
		ox = -dy * UnstickStep
		oy = dx * UnstickStep
	ElseIf aiAttempt == 2
		ox = dy * UnstickStep
		oy = -dx * UnstickStep
	Else
		ox = -dx * UnstickStep * 1.5
		oy = -dy * UnstickStep * 1.5
	EndIf
	f.MoveTo(f, ox, oy, 20.0, False)
	Utility.Wait(0.3)
EndFunction

Event OnItemAdded(Form akBaseItem, Int aiItemCount, ObjectReference akItemReference, ObjectReference akSourceContainer)
	; Tout ingredient recu par Faelia part dans la besace, meme si l'evenement arrive apres la fenetre de capture
	; (file Papyrus chargee sur un gros modpack) : elle n'a aucun autre usage des ingredients.
	If akBaseItem as Ingredient
		Main().OnCaptured(GetActorRef(), akBaseItem, aiItemCount)
	EndIf
EndEvent

; ---------------------------------------------------------------- scan et planification

; Remplit les tableaux avec les cibles recoltables autour du joueur. Filtres du moins cher au plus cher.
; Passes : 0 plantes du catalogue, 1 ingredients au sol, 2 Nirnroot (activateurs), 3 creatures mortes.
Int Function ScanCandidates(NPCH_MainQuestScript q, Actor p, ObjectReference[] cand, Float[] cx, Float[] cy, Float[] cz, Bool[] rare)
	NPCH_FeaturesScript ft = Feat()
	Float radius = q.NPCH_ScanRadius.GetValue()
	If radius <= 0.0
		radius = 1950.0
	EndIf
	; au-dela de cette distance du joueur, une cible ordinaire ne peut pas faire partie d'une tournee : on ne la garde pas
	; (evite de remplir les 128 places avec des plantes lointaines). Les plantes rares sont gardees jusqu'au rayon de scan.
	Float maxUseful = ClampPositive(q.NPCH_BatchStartDist.GetValue(), 1050.0) + ClampPositive(q.NPCH_BatchLinkDist.GetValue(), 500.0) * q.NPCH_BatchMaxPlants.GetValueInt()
	Bool rareDetour = ft.NPCH_RareAlert.GetValueInt() == 1
	Float pz = p.GetPositionZ()
	Cell lastCell = None
	Bool lastCellPrivate = False
	Int n = 0
	Int pass = 0
	; creatures mortes : ignorees si un filtre de recolte (cible / rares seulement) est actif
	Bool corpsesOn = ft.NPCH_LootCorpses.GetValueInt() == 1 && ft.NPCH_TargetEffect.GetValueInt() == 0 && ft.NPCH_RareOnly.GetValueInt() == 0
	While pass < 4 && n < 128
		; Papyrus refuse d'affecter None a un tableau : on suit le nombre de resultats de CETTE passe a part
		ObjectReference[] refs
		Int refCount = 0
		If pass == 0
			refs = PO3_SKSEFunctions.FindAllReferencesOfType(p, q.HarvestableList, radius)
			refCount = refs.Length
		ElseIf pass == 1
			refs = PO3_SKSEFunctions.FindAllReferencesOfFormType(p, 30, radius)  ; ingredients au sol (type de la base)
			refCount = refs.Length
		ElseIf pass == 2
			refs = PO3_SKSEFunctions.FindAllReferencesOfType(p, ft.NirnrootActivators, radius)
			refCount = refs.Length
		ElseIf corpsesOn
			refs = PO3_SKSEFunctions.FindAllReferencesOfFormType(p, 43, radius)  ; acteurs (base NPC_), filtres : morts, creatures
			refCount = refs.Length
		EndIf
		Int i = 0
		While i < refCount && n < 128
			ObjectReference r = refs[i]
			If r && !StorageUtil.FormListHas(None, VisitedKey, r) && !PO3_SKSEFunctions.IsQuestItem(r) && !r.IsDisabled()
				Float dp = p.GetDistance(r)
				If dp >= PlayerExclusion
					Float z = r.GetPositionZ()
					Float dz = z - pz
					If dz <= MaxHeightDiff && dz >= -MaxHeightDiff && !r.GetActorOwner() && !r.GetFactionOwner()
						Cell c = r.GetParentCell()
						If c != lastCell
							lastCell = c
							lastCellPrivate = False
							If c && (c.GetActorOwner() || c.GetFactionOwner())
								lastCellPrivate = True
							EndIf
						EndIf
						If !lastCellPrivate
							Bool ok = False
							Bool isRare = False
							If pass == 3
								ok = dp <= maxUseful && ft.IsLootableCorpse(r as Actor)
							Else
								Form base = r.GetBaseObject()
								ok = ft.PassesFilters(base)
								isRare = ok && rareDetour && ft.IsRare(base)
								ok = ok && (isRare || dp <= maxUseful)
							EndIf
							If ok
								cand[n] = r
								cx[n] = r.GetPositionX()
								cy[n] = r.GetPositionY()
								cz[n] = z
								rare[n] = isRare
								n += 1
							EndIf
						EndIf
					EndIf
				EndIf
			EndIf
			i += 1
		EndWhile
		pass += 1
	EndWhile
	Return n
EndFunction

; Chaine du plus proche voisin. Premiere cible : la plante rare la plus proche de Faelia s'il y en a une (detour),
; sinon la plus proche de Faelia parmi celles a moins de NPCH_BatchStartDist du joueur.
; Suivantes : la plus proche de la precedente, a moins de NPCH_BatchLinkDist.
Int Function BuildChain(NPCH_MainQuestScript q, Actor f, Actor p, Int n, Float[] cx, Float[] cy, Float[] cz, Bool[] rare, Int[] order)
	Float startDist = ClampPositive(q.NPCH_BatchStartDist.GetValue(), 1050.0)
	Float link = ClampPositive(q.NPCH_BatchLinkDist.GetValue(), 500.0)
	Int maxPlants = q.NPCH_BatchMaxPlants.GetValueInt()
	If maxPlants < 1
		maxPlants = 1
	ElseIf maxPlants > 128
		maxPlants = 128
	EndIf
	Float px = p.GetPositionX()
	Float py = p.GetPositionY()
	Float pz = p.GetPositionZ()
	Float fx = f.GetPositionX()
	Float fy = f.GetPositionY()
	Float fz = f.GetPositionZ()

	Int cur = -1
	Float best = 1000000000000.0 ; Papyrus n'accepte pas la notation 1e12
	Int i = 0
	While i < n
		If rare[i]
			Float dr = Dist2(cx[i], cy[i], cz[i], fx, fy, fz)
			If dr < best
				best = dr
				cur = i
			EndIf
		EndIf
		i += 1
	EndWhile

	Bool haveRare = cur >= 0
	Float start2 = startDist * startDist
	i = 0
	While i < n && !haveRare
		If Dist2(cx[i], cy[i], cz[i], px, py, pz) <= start2
			Float d2 = Dist2(cx[i], cy[i], cz[i], fx, fy, fz)
			If d2 < best
				best = d2
				cur = i
			EndIf
		EndIf
		i += 1
	EndWhile

	Bool[] used = new Bool[128]
	Float link2 = link * link
	Int k = 0
	While cur >= 0 && k < maxPlants
		order[k] = cur
		used[cur] = True
		k += 1
		Int nxt = -1
		best = link2
		Int j = 0
		While j < n
			If !used[j]
				Float dj = Dist2(cx[j], cy[j], cz[j], cx[cur], cy[cur], cz[cur])
				If dj <= best
					best = dj
					nxt = j
				EndIf
			EndIf
			j += 1
		EndWhile
		cur = nxt
	EndWhile
	Return k
EndFunction

; Reglage console absurde (0 ou negatif) : on retombe sur la valeur par defaut
Float Function ClampPositive(Float afValue, Float afDefault)
	If afValue <= 0.0
		Return afDefault
	EndIf
	Return afValue
EndFunction

Float Function Dist2(Float ax, Float ay, Float az, Float bx, Float by, Float bz)
	Float dx = ax - bx
	Float dy = ay - by
	Float dz = az - bz
	Return dx * dx + dy * dy + dz * dz
EndFunction

; ---------------------------------------------------------------- reactions vocales

Function React(Actor f, Topic akTopic, Int aiChance)
	If !akTopic || aiChance <= 0
		Return
	EndIf
	Float now = Utility.GetCurrentRealTime()
	If now < lastReactionTime
		lastReactionTime = 0.0 ; le compteur repart de zero a chaque lancement du jeu
	EndIf
	If now - lastReactionTime < ReactionCooldown
		Return
	EndIf
	If Utility.RandomInt(1, 100) <= aiChance
		lastReactionTime = now
		f.Say(akTopic)
		Debug.Trace("[NPCHerbo] Reaction vocale : " + akTopic + " (chance " + aiChance + " %)")
	EndIf
EndFunction

; ---------------------------------------------------------------- plantes deja visitees

Function MarkVisited(ObjectReference r)
	If StorageUtil.FormListHas(None, VisitedKey, r)
		Return ; deja presente : evite de desynchroniser les deux listes
	EndIf
	StorageUtil.FormListAdd(None, VisitedKey, r, False)
	StorageUtil.FloatListAdd(None, VisitedTimeKey, Utility.GetCurrentGameTime(), True)
	If StorageUtil.FormListCount(None, VisitedKey) > MaxVisited
		StorageUtil.FormListRemoveAt(None, VisitedKey, 0)
		StorageUtil.FloatListRemoveAt(None, VisitedTimeKey, 0)
	EndIf
EndFunction

Function PurgeVisited()
	lastPurgeTime = Utility.GetCurrentGameTime()
	; les entrees sont ajoutees dans l'ordre chronologique : on retire les plus anciennes en tete
	Float limit = lastPurgeTime - RevisitDays
	While StorageUtil.FloatListCount(None, VisitedTimeKey) > 0 && StorageUtil.FloatListGet(None, VisitedTimeKey, 0) < limit
		StorageUtil.FormListRemoveAt(None, VisitedKey, 0)
		StorageUtil.FloatListRemoveAt(None, VisitedTimeKey, 0)
	EndWhile
	; securite : si les deux listes divergent, on repart de zero
	If StorageUtil.FormListCount(None, VisitedKey) != StorageUtil.FloatListCount(None, VisitedTimeKey)
		StorageUtil.FormListClear(None, VisitedKey)
		StorageUtil.FloatListClear(None, VisitedTimeKey)
	EndIf
EndFunction
