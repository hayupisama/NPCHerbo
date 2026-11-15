Scriptname NPCH_BagScript extends ReferenceAlias
{NPCHerbo - besace de Faelia : n'accepte que les ingredients, respecte le plafond, tient le compteur a jour.}

NPCH_MainQuestScript Function Main()
	Return GetOwningQuest() as NPCH_MainQuestScript
EndFunction

Event OnItemAdded(Form akBaseItem, Int aiItemCount, ObjectReference akItemReference, ObjectReference akSourceContainer)
	NPCH_MainQuestScript q = Main()
	ObjectReference bag = GetReference()

	If !(akBaseItem as Ingredient)
		; seuls les ingredients sont acceptes : on rend l'objet a celui qui l'a depose
		If akSourceContainer
			bag.RemoveItem(akBaseItem, aiItemCount, True, akSourceContainer)
		Else
			bag.RemoveItem(akBaseItem, aiItemCount, True, q.PlayerRef)
		EndIf
		If akSourceContainer == q.PlayerRef
			q.MsgRefused.Show()
		EndIf
		Return
	EndIf

	q.NPCH_BagCount.Mod(aiItemCount)

	Int over = q.NPCH_BagCount.GetValueInt() - q.GetCapacity()
	If over > aiItemCount
		over = aiItemCount ; on ne rend jamais plus que ce qui vient d'arriver
	EndIf
	If over > 0
		If akSourceContainer == q.PlayerRef && q.CaptureMode == 0
			; le joueur a trop depose a la main : on lui rend l'excedent (le compteur est corrige par OnItemRemoved)
			bag.RemoveItem(akBaseItem, over, True, q.PlayerRef)
			q.MsgNoRoom.Show()
		Else
			; recolte qui deborde : l'excedent est recree aux pieds de Faelia puis retire de la besace.
			; (pas de DropObject : la besace est dans une cellule non chargee ; pas de transfert vers un inventaire :
			;  il serait recapture et renvoye dans la besace en boucle)
			q.GetFaelia().PlaceAtMe(akBaseItem, over)
			bag.RemoveItem(akBaseItem, over, True)
		EndIf
	EndIf
EndEvent

Event OnItemRemoved(Form akBaseItem, Int aiItemCount, ObjectReference akItemReference, ObjectReference akDestContainer)
	If akBaseItem as Ingredient
		NPCH_MainQuestScript q = Main()
		q.NPCH_BagCount.Mod(-aiItemCount)
		If q.NPCH_BagCount.GetValueInt() < 0
			q.NPCH_BagCount.SetValueInt(0)
		EndIf
	EndIf
EndEvent
