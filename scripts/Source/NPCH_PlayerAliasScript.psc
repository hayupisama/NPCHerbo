Scriptname NPCH_PlayerAliasScript extends ReferenceAlias
{NPCHerbo - evenements du joueur : chargement de partie, capture des recoltes (methode 1).}

NPCH_MainQuestScript Function Main()
	Return GetOwningQuest() as NPCH_MainQuestScript
EndFunction

Event OnPlayerLoadGame()
	Main().OnGameLoaded()
EndEvent

Event OnPlayerFastTravelEnd(Float afTravelGameTimeHours)
	Main().OnPlayerFastTravelEnd()
EndEvent

Event OnItemAdded(Form akBaseItem, Int aiItemCount, ObjectReference akItemReference, ObjectReference akSourceContainer)
	; Methode 2 seulement : on ne capture que ce qui vient d'une plante (pas de conteneur source).
	; Un ingredient rendu par la besace (source = besace) ne doit surtout pas y repartir : boucle infinie.
	If akSourceContainer || !(akBaseItem as Ingredient)
		Return
	EndIf
	NPCH_MainQuestScript q = Main()
	If q.CaptureMode == 2
		q.OnCaptured(GetActorRef(), akBaseItem, aiItemCount)
	EndIf
EndEvent
