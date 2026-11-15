;BEGIN FRAGMENT CODE - NPCHerbo (genere par le generateur, edite a la main)
Scriptname TIF_NPCH_Dialogue extends TopicInfo Hidden
{NPCHerbo - fragments des dialogues de Faelia (appeles en fin de replique).}

NPCH_MainQuestScript Function Main()
	Return GetOwningQuest() as NPCH_MainQuestScript
EndFunction

Function Fragment_OpenBag(ObjectReference akSpeakerRef)
	Main().OpenBag()
EndFunction

Function Fragment_BagStatus(ObjectReference akSpeakerRef)
	Main().ShowBagStatus()
EndFunction

Function Fragment_UpgradeBag(ObjectReference akSpeakerRef)
	Main().UpgradeBag()
EndFunction

Function Fragment_HarvestOff(ObjectReference akSpeakerRef)
	Main().SetHarvestEnabled(False)
EndFunction

Function Fragment_HarvestOn(ObjectReference akSpeakerRef)
	Main().SetHarvestEnabled(True)
EndFunction
;END FRAGMENT CODE
