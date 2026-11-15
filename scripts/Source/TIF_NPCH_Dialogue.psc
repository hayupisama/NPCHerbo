;BEGIN FRAGMENT CODE - NPCHerbo (genere par le generateur, edite a la main)
Scriptname TIF_NPCH_Dialogue extends TopicInfo Hidden
{NPCHerbo - fragments des dialogues de Faelia (appeles en fin de replique).}

NPCH_MainQuestScript Function Main()
	Return GetOwningQuest() as NPCH_MainQuestScript
EndFunction

NPCH_FeaturesScript Function Feat()
	Return GetOwningQuest() as NPCH_FeaturesScript
EndFunction

; --- besace
Function Fragment_OpenBag(ObjectReference akSpeakerRef)
	Main().OpenBag()
EndFunction

Function Fragment_BagStatus(ObjectReference akSpeakerRef)
	Main().ShowBagStatus()
	Feat().ShowDiscoveries()
EndFunction

Function Fragment_UpgradeBag(ObjectReference akSpeakerRef)
	Main().UpgradeBag()
EndFunction

; --- cueillette
Function Fragment_HarvestOff(ObjectReference akSpeakerRef)
	Main().SetHarvestEnabled(False)
EndFunction

Function Fragment_HarvestOn(ObjectReference akSpeakerRef)
	Main().SetHarvestEnabled(True)
EndFunction

Function Fragment_ExcludeLast(ObjectReference akSpeakerRef)
	Feat().ExcludeLastHarvested()
EndFunction

Function Fragment_ResetExclusions(ObjectReference akSpeakerRef)
	Feat().ResetExclusions()
EndFunction

; --- recolte ciblee
Function Fragment_TargetHeal(ObjectReference akSpeakerRef)
	Feat().SetTargetEffect(1)
EndFunction

Function Fragment_TargetMagicka(ObjectReference akSpeakerRef)
	Feat().SetTargetEffect(2)
EndFunction

Function Fragment_TargetStamina(ObjectReference akSpeakerRef)
	Feat().SetTargetEffect(3)
EndFunction

Function Fragment_TargetPoison(ObjectReference akSpeakerRef)
	Feat().SetTargetEffect(4)
EndFunction

Function Fragment_TargetRare(ObjectReference akSpeakerRef)
	Feat().SetRareOnly()
EndFunction

Function Fragment_TargetClear(ObjectReference akSpeakerRef)
	Feat().ClearTargeting()
EndFunction

; --- reglages
Function Fragment_Settings(ObjectReference akSpeakerRef)
	Feat().OpenSettings()
EndFunction

; --- savoir et maison
Function Fragment_Training(ObjectReference akSpeakerRef)
	Feat().OpenTraining()
EndFunction

Function Fragment_SetDeposit(ObjectReference akSpeakerRef)
	Feat().SetDepositChest()
EndFunction

Function Fragment_DepositAll(ObjectReference akSpeakerRef)
	Feat().DepositAll()
EndFunction
;END FRAGMENT CODE
