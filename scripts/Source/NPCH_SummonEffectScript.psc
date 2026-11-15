Scriptname NPCH_SummonEffectScript extends ActiveMagicEffect
{NPCHerbo - pouvoir "Appel de Faelia" : appelle ou renvoie Faelia.}

NPCH_MainQuestScript Property MainQuest Auto

Event OnEffectStart(Actor akTarget, Actor akCaster)
	MainQuest.ToggleFaelia()
EndEvent
