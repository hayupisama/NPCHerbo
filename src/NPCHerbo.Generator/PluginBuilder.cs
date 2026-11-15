using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace NPCHerbo.Generator;

/// <summary>Construit NPCHerbo.esp (ESL, en-tête 1.70 pour Skyrim SE 1.5.97).</summary>
public sealed partial class PluginBuilder
{
    readonly ISkyrimModDisposableGetter _esm;
    readonly SkyrimMod _mod;
    readonly FormIdRegistry _reg;

    public PluginBuilder(ISkyrimModDisposableGetter skyrimEsm, FormIdRegistry registry)
    {
        _esm = skyrimEsm;
        _reg = registry;
        _mod = new SkyrimMod(ModKey.FromFileName("NPCHerbo.esp"), SkyrimRelease.SkyrimSE);
        _mod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
        _mod.ModHeader.Stats.Version = 1.7f;
        _mod.ModHeader.Stats.NextFormID = 0x800;
        _mod.ModHeader.Author = "NPCHerbo";
        _mod.ModHeader.Description = "Faelia, herboriste Bosmer qui recolte les ingredients d'alchimie.";
    }

    static IFormLink<T> L<T>(FormKey fk) where T : class, IMajorRecordGetter => new FormLink<T>(fk);

    /// <summary>FormKey stable (registre formids.json) pour un EditorID.</summary>
    FormKey Key(string editorId) => new(_mod.ModKey, _reg.Get(editorId));

    /// <summary>Le prochain « new X(_mod, editorId) » recevra le FormID stable de cet EditorID.</summary>
    void Reserve(string editorId) => _mod.ModHeader.Stats.NextFormID = _reg.Get(editorId);

    public SkyrimMod Build()
    {
        // ------------------------------------------------------------ globales
        var gFollowing = AddGlobalShort("NPCH_Following", 0);
        var gHarvestEnabled = AddGlobalShort("NPCH_HarvestEnabled", 1);
        var gHarvestMethod = AddGlobalShort("NPCH_HarvestMethod", 0);
        var gDebug = AddGlobalShort("NPCH_Debug", 1);
        var gHarvesting = AddGlobalShort("NPCH_Harvesting", 0);
        var gShowHarvest = AddGlobalShort("NPCH_ShowHarvest", 1);
        // Réglages des tournées (modifiables en console : set NPCH_ScanRadius to 1200 ...).
        // NPCH_HarvestRadius (000804) n'est plus utilisé : son FormID reste réservé dans formids.json.
        var gScanRadius = AddGlobalFloat("NPCH_ScanRadius", 1950f);       // ~28 m autour du joueur
        var gBatchStart = AddGlobalFloat("NPCH_BatchStartDist", 1050f);   // ~15 m : 1re plante d'une tournée
        var gBatchLink = AddGlobalFloat("NPCH_BatchLinkDist", 500f);      // ~7 m entre deux plantes d'une tournée
        var gBatchMaxPlants = AddGlobalShort("NPCH_BatchMaxPlants", 8);
        var gBatchMaxTime = AddGlobalFloat("NPCH_BatchMaxTime", 30f);     // secondes
        var gLeash = AddGlobalFloat("NPCH_LeashDistance", 5600f);
        var gBagTier = AddGlobalShort("NPCH_BagTier", 0);
        var gBagCapacity = AddGlobalShort("NPCH_BagCapacity", 300);
        var gBagCount = AddGlobalShort("NPCH_BagCount", 0);

        // catalogue des plantes récoltables, rempli par script au chargement
        Reserve("NPCH_HarvestableList");
        var harvestable = new FormList(_mod, "NPCH_HarvestableList");
        _mod.FormLists.Add(harvestable);

        // ------------------------------------------------------------ faction
        Reserve("NPCH_Faction");
        var faction = new Faction(_mod, "NPCH_Faction")
        {
            Name = "Faelia",
            Flags = Faction.FactionFlag.HiddenFromPC,
        };
        faction.Relations.Add(new Relation { Target = L<IRelatableGetter>(Vanilla.PlayerFaction), Reaction = CombatReaction.Ally });
        _mod.Factions.Add(faction);

        // ------------------------------------------------------------ capacité de vitesse
        Reserve("NPCH_AbAgilityEffect");
        var speedEffect = new MagicEffect(_mod, "NPCH_AbAgilityEffect")
        {
            Name = "Agilité de Faelia",
            Flags = MagicEffect.Flag.Recover | MagicEffect.Flag.HideInUI | MagicEffect.Flag.NoArea | MagicEffect.Flag.NoDuration,
            Archetype = new MagicEffectArchetype(MagicEffectArchetype.TypeEnum.ValueModifier) { ActorValue = ActorValue.SpeedMult },
            CastType = CastType.ConstantEffect,
            TargetType = TargetType.Self,
            MagicSkill = ActorValue.None,
            ResistValue = ActorValue.None,
        };
        _mod.MagicEffects.Add(speedEffect);

        Reserve("NPCH_AbAgility");

        var speedAbility = new Spell(_mod, "NPCH_AbAgility")
        {
            Name = "Agilité de Faelia",
            Type = SpellType.Ability,
            CastType = CastType.ConstantEffect,
            TargetType = TargetType.Self,
            Flags = SpellDataFlag.ManualCostCalc,
        };
        speedAbility.Effects.Add(new Effect
        {
            BaseEffect = speedEffect.ToNullableLink(),
            Data = new EffectData { Magnitude = 50f },
        });
        // discrétion : elle se cache bien pendant les combats (package « à l'abri » accroupi)
        Reserve("NPCH_AbStealthEffect");
        var stealthEffect = new MagicEffect(_mod, "NPCH_AbStealthEffect")
        {
            Name = "Discrétion de Faelia",
            Flags = MagicEffect.Flag.Recover | MagicEffect.Flag.HideInUI | MagicEffect.Flag.NoArea | MagicEffect.Flag.NoDuration,
            Archetype = new MagicEffectArchetype(MagicEffectArchetype.TypeEnum.ValueModifier) { ActorValue = ActorValue.Sneak },
            CastType = CastType.ConstantEffect,
            TargetType = TargetType.Self,
            MagicSkill = ActorValue.None,
            ResistValue = ActorValue.None,
        };
        _mod.MagicEffects.Add(stealthEffect);
        speedAbility.Effects.Add(new Effect
        {
            BaseEffect = stealthEffect.ToNullableLink(),
            Data = new EffectData { Magnitude = 80f },
        });
        _mod.Spells.Add(speedAbility);

        // ------------------------------------------------------------ besace (conteneur)
        var satchel = _esm.Containers.First(c => c.FormKey == Vanilla.SatchelContainer);
        var bag = satchel.Duplicate(Key("NPCH_BagContainer"));
        bag.EditorID = "NPCH_BagContainer";
        bag.Name = "Besace de Faelia";
        bag.Items = null;
        bag.Flags = 0;
        bag.VirtualMachineAdapter = null;
        _mod.Containers.Add(bag);

        // ------------------------------------------------------------ PNJ
        var npc = BuildNpc(faction, speedAbility);

        // ------------------------------------------------------------ package de suivi (copie du package vanilla)
        var followSrc = _esm.Packages.First(p => p.FormKey == Vanilla.FollowPlayerPackage);
        var follow = followSrc.Duplicate(Key("NPCH_PkgFollow"));
        follow.EditorID = "NPCH_PkgFollow";
        follow.VirtualMachineAdapter = null;
        ((PackageDataFloat)follow.Data[1]).Data = 200f; // rayon mini
        ((PackageDataFloat)follow.Data[2]).Data = 450f; // rayon maxi
        follow.Flags |= Package.Flag.AllowSwimming;
        _mod.Packages.Add(follow);

        // ------------------------------------------------------------ package « à l'abri » : pendant les combats du joueur,
        // elle le suit de loin (15-25 m), accroupie. Si on l'attaque, sa confiance « couarde » la fait fuir.
        var hide = followSrc.Duplicate(Key("NPCH_PkgHide"));
        hide.EditorID = "NPCH_PkgHide";
        ((PackageDataFloat)hide.Data[1]).Data = 1050f; // rayon mini (~15 m)
        ((PackageDataFloat)hide.Data[2]).Data = 1750f; // rayon maxi (~25 m)
        hide.Flags |= Package.Flag.AllowSwimming | Package.Flag.AlwaysSneak | Package.Flag.NoCombatAlert;
        hide.Conditions.Clear();
        var playerInCombat = new GetCombatStateConditionData { RunOnType = Condition.RunOnType.Reference };
        playerInCombat.Reference.SetTo(Vanilla.PlayerRef);
        hide.Conditions.Add(new ConditionFloat
        {
            CompareOperator = CompareOperator.GreaterThanOrEqualTo,
            ComparisonValue = 1f,
            Data = playerInCombat,
        });
        _mod.Packages.Add(hide);

        // ------------------------------------------------------------ package de récolte : Travel vers l'alias HarvestTarget
        const uint AliasHarvestTarget = 3;
        var travelSrc = _esm.Packages.First(p => p.EditorID == Vanilla.TravelToAliasPackageEditorId);
        var harvestPkg = travelSrc.Duplicate(Key("NPCH_PkgHarvest"));
        harvestPkg.EditorID = "NPCH_PkgHarvest";
        harvestPkg.VirtualMachineAdapter = null; // retire le fragment vanilla (SetStage 175 de la quête VC01) hérité de la copie
        var place = harvestPkg.Data.Values.OfType<PackageDataLocation>().First();
        place.Location.Target = new LocationFallback { Type = LocationTargetRadius.LocationType.AliasForReference, Data = (int)AliasHarvestTarget };
        place.Location.Radius = 60;
        harvestPkg.PreferredSpeed = Package.Speed.Run;
        harvestPkg.Flags |= Package.Flag.AllowSwimming;
        harvestPkg.Conditions.Clear();
        var harvestingCond = new GetGlobalValueConditionData { RunOnType = Condition.RunOnType.Subject };
        harvestingCond.Global.Link.SetTo(gHarvesting);
        harvestPkg.Conditions.Add(new ConditionFloat
        {
            CompareOperator = CompareOperator.EqualTo,
            ComparisonValue = 1f,
            Data = harvestingCond,
        });
        _mod.Packages.Add(harvestPkg);

        // ------------------------------------------------------------ cellule de garde + références
        // Un plugin ESL ne doit pas créer de CELL : les modifications de son contenu (la besace !) risquent de ne pas
        // être sauvegardées. On surcharge donc une cellule de développement vanilla inaccessible en jeu et on y place
        // nos références persistantes. NPCH_HoldingCell (000810) n'est plus utilisé : son FormID reste réservé.
        var holdSrc = _esm.EnumerateMajorRecords<ICellGetter>().First(c => c.EditorID == Vanilla.HoldingCellEditorId);
        var cell = holdSrc.DeepCopy();
        cell.Persistent.Clear();
        cell.Temporary.Clear();
        cell.NavigationMeshes.Clear();
        cell.Landscape = null;
        Reserve("NPCH_HoldingMarker");
        var marker = new PlacedObject(_mod, "NPCH_HoldingMarker")
        {
            Base = L<IPlaceableObjectGetter>(Vanilla.XMarker).AsNullable(),
            Placement = new Placement { Position = new P3Float(0, 0, 0), Rotation = new P3Float(0, 0, 0) },
        };
        Reserve("NPCH_BagChestRef");
        var bagRef = new PlacedObject(_mod, "NPCH_BagChestRef")
        {
            Base = bag.ToNullableLink<IPlaceableObjectGetter>(),
            Placement = new Placement { Position = new P3Float(0, 200, 0), Rotation = new P3Float(0, 0, 0) },
        };
        Reserve("NPCH_FaeliaRef");
        var faeliaRef = new PlacedNpc(_mod, "NPCH_FaeliaRef")
        {
            Base = npc.ToNullableLink(),
            Placement = new Placement { Position = new P3Float(100, 0, 0), Rotation = new P3Float(0, 0, 0) },
        };
        const int Persistent = 0x400, InitiallyDisabled = 0x800;
        marker.MajorRecordFlagsRaw |= Persistent;
        bagRef.MajorRecordFlagsRaw |= Persistent;
        faeliaRef.MajorRecordFlagsRaw |= Persistent | InitiallyDisabled;
        cell.Persistent.Add(marker);
        cell.Persistent.Add(bagRef);
        cell.Persistent.Add(faeliaRef);
        AddInteriorCell(cell);

        // ------------------------------------------------------------ pouvoir « Appel de Faelia »
        Reserve("NPCH_MainQuest");
        var quest = new Quest(_mod, "NPCH_MainQuest");

        Reserve("NPCH_PowerSummonEffect");

        var summonEffect = new MagicEffect(_mod, "NPCH_PowerSummonEffect")
        {
            Name = "Appel de Faelia",
            Flags = MagicEffect.Flag.NoMagnitude | MagicEffect.Flag.NoArea | MagicEffect.Flag.NoDuration
                    | MagicEffect.Flag.Painless | MagicEffect.Flag.HideInUI,
            Archetype = new MagicEffectArchetype(MagicEffectArchetype.TypeEnum.Script),
            CastType = CastType.FireAndForget,
            TargetType = TargetType.Self,
            MagicSkill = ActorValue.None,
            ResistValue = ActorValue.None,
            VirtualMachineAdapter = new VirtualMachineAdapter
            {
                Scripts =
                {
                    new ScriptEntry
                    {
                        Name = "NPCH_SummonEffectScript",
                        Properties = { new ScriptObjectProperty { Name = "MainQuest", Object = quest.ToLink<ISkyrimMajorRecordGetter>() } },
                    },
                },
            },
        };
        _mod.MagicEffects.Add(summonEffect);

        Reserve("NPCH_PowerSummon");

        var power = new Spell(_mod, "NPCH_PowerSummon")
        {
            Name = "Appel de Faelia",
            Description = "Appelle Faelia l'herboriste à vos côtés, ou la renvoie à son repaire.",
            Type = SpellType.LesserPower,
            CastType = CastType.FireAndForget,
            TargetType = TargetType.Self,
            Flags = SpellDataFlag.ManualCostCalc,
        };
        // même type d'équipement que les pouvoirs mineurs vanilla (emplacement « voix »)
        var vanillaPower = _esm.Spells.First(s => s.Type == SpellType.LesserPower && !s.EquipmentType.IsNull);
        power.EquipmentType.SetTo(vanillaPower.EquipmentType.FormKey);
        Console.WriteLine($"Pouvoir : type d'equipement repris de {vanillaPower.EditorID} ({vanillaPower.EquipmentType.FormKey})");
        power.Effects.Add(new Effect { BaseEffect = summonEffect.ToNullableLink(), Data = new EffectData() });
        _mod.Spells.Add(power);

        // ------------------------------------------------------------ réactions vocales (répliques vanilla partagées)
        var gReactionChance = AddGlobalShort("NPCH_ReactionChance", 60);
        var topicFound = AddSharedLinesTopic("NPCH_TopicFoundPlant", quest, npc, Vanilla.LinesFoundPlant);
        var topicDone = AddSharedLinesTopic("NPCH_TopicHarvestDone", quest, npc, Vanilla.LinesHarvestDone);

        // ------------------------------------------------------------ dialogues + message d'état de la besace
        BuildDialogues(quest, npc, gBagTier, gHarvestEnabled);
        Reserve("NPCH_MsgBagStatus");
        var msgBagStatus = new Message(_mod, "NPCH_MsgBagStatus")
        {
            Name = "Besace de Faelia",
            Description = "Besace de Faelia : %.0f / %.0f ingrédients",
            Flags = 0, // sans MessageBox : affiché en notification
        };
        msgBagStatus.Quest.SetTo(quest);
        _mod.Messages.Add(msgBagStatus);

        // messages courts (notifications) : les textes accentués vivent dans l'ESP, pas dans les scripts
        var msgInstalled = AddNotification("NPCH_MsgInstalled", "Nouveau pouvoir : « Appel de Faelia »", quest);
        var msgSummon = AddNotification("NPCH_MsgSummon", "Faelia vous rejoint.", quest);
        var msgDismiss = AddNotification("NPCH_MsgDismiss", "Faelia retourne à son repaire.", quest);
        var msgBagFull = AddNotification("NPCH_MsgBagFull", "Faelia : « Ma besace est pleine ! »", quest);
        var msgRefused = AddNotification("NPCH_MsgRefused", "Faelia : « Je ne garde que les ingrédients d'alchimie ! »", quest);
        var msgNoRoom = AddNotification("NPCH_MsgNoRoom", "Faelia : « Plus de place dans ma besace ! »", quest);
        var msgUpgraded = AddNotification("NPCH_MsgUpgraded", "Faelia a un nouveau sac : %.0f places.", quest);

        // ------------------------------------------------------------ quête principale
        quest.Name = "NPCHerbo";
        quest.Flags = Quest.Flag.StartGameEnabled;
        quest.Priority = 50;

        const uint AliasPlayer = 0, AliasFaelia = 1, AliasBag = 2;
        quest.Aliases.Add(new QuestAlias
        {
            ID = AliasPlayer,
            Name = "Player",
            Type = QuestAlias.TypeEnum.Reference,
            ForcedReference = L<IPlacedGetter>(Vanilla.PlayerRef).AsNullable(),
        });
        var faeliaAlias = new QuestAlias
        {
            ID = AliasFaelia,
            Name = "Faelia",
            Type = QuestAlias.TypeEnum.Reference,
            Flags = QuestAlias.Flag.AllowDisabled | QuestAlias.Flag.AllowReserved,
            ForcedReference = faeliaRef.ToNullableLink<IPlacedGetter>(),
        };
        // ordre = priorité : à l'abri (combat du joueur) > récolte (NPCH_Harvesting == 1) > suivi
        faeliaAlias.PackageData.Add(hide.ToLink());
        faeliaAlias.PackageData.Add(harvestPkg.ToLink());
        faeliaAlias.PackageData.Add(follow.ToLink());
        quest.Aliases.Add(faeliaAlias);
        quest.Aliases.Add(new QuestAlias
        {
            ID = AliasBag,
            Name = "BagChest",
            Type = QuestAlias.TypeEnum.Reference,
            Flags = QuestAlias.Flag.AllowDisabled | QuestAlias.Flag.AllowReserved,
            ForcedReference = bagRef.ToNullableLink<IPlacedGetter>(),
        });
        quest.Aliases.Add(new QuestAlias
        {
            ID = AliasHarvestTarget,
            Name = "HarvestTarget",
            Type = QuestAlias.TypeEnum.Reference,
            Flags = QuestAlias.Flag.Optional | QuestAlias.Flag.AllowReserved | QuestAlias.Flag.AllowReuseInQuest,
        });
        quest.NextAliasID = 4;
        harvestPkg.OwnerQuest.SetTo(quest);

        ScriptObjectProperty Obj(string name, IFormLinkGetter<ISkyrimMajorRecordGetter> link) => new() { Name = name, Object = link.AsSetter() };
        ScriptObjectProperty AliasProp(string name, uint aliasId) =>
            new() { Name = name, Object = quest.ToLink<ISkyrimMajorRecordGetter>(), Alias = (short)aliasId };

        quest.VirtualMachineAdapter = new QuestAdapter
        {
            Scripts =
            {
                new ScriptEntry
                {
                    Name = "NPCH_MainQuestScript",
                    Properties =
                    {
                        Obj("PlayerRef", L<ISkyrimMajorRecordGetter>(Vanilla.PlayerRef)),
                        AliasProp("FaeliaAlias", AliasFaelia),
                        AliasProp("PlayerAlias", AliasPlayer),
                        AliasProp("HarvestTargetAlias", AliasHarvestTarget),
                        Obj("NPCH_Harvesting", gHarvesting.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_ShowHarvest", gShowHarvest.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_ReactionChance", gReactionChance.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("TopicFoundPlant", topicFound.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("TopicHarvestDone", topicDone.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgBagStatus", msgBagStatus.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgInstalled", msgInstalled.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgSummon", msgSummon.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgDismiss", msgDismiss.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgBagFull", msgBagFull.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgRefused", msgRefused.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgNoRoom", msgNoRoom.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("MsgUpgraded", msgUpgraded.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("Gold001", L<ISkyrimMajorRecordGetter>(Vanilla.Gold001)),
                        Obj("BagChest", bagRef.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("HoldingMarker", marker.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("PowerSummon", power.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("IdleHarvest", L<ISkyrimMajorRecordGetter>(Vanilla.IdleSearchingChest)),
                        Obj("HarvestableList", harvestable.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_Following", gFollowing.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_HarvestEnabled", gHarvestEnabled.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_ScanRadius", gScanRadius.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BatchStartDist", gBatchStart.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BatchLinkDist", gBatchLink.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BatchMaxPlants", gBatchMaxPlants.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BatchMaxTime", gBatchMaxTime.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_LeashDistance", gLeash.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_HarvestMethod", gHarvestMethod.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_Debug", gDebug.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BagTier", gBagTier.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BagCapacity", gBagCapacity.ToLink<ISkyrimMajorRecordGetter>()),
                        Obj("NPCH_BagCount", gBagCount.ToLink<ISkyrimMajorRecordGetter>()),
                    },
                },
            },
            Aliases =
            {
                new QuestFragmentAlias
                {
                    Property = AliasProp("", AliasPlayer),
                    Scripts = { new ScriptEntry { Name = "NPCH_PlayerAliasScript" } },
                },
                new QuestFragmentAlias
                {
                    Property = AliasProp("", AliasFaelia),
                    Scripts = { new ScriptEntry { Name = "NPCH_FaeliaAliasScript" } },
                },
                new QuestFragmentAlias
                {
                    Property = AliasProp("", AliasBag),
                    Scripts = { new ScriptEntry { Name = "NPCH_BagScript" } },
                },
            },
        };
        AddFeatures(quest, npc); // fonctionnalités v2 (Features.cs)
        _mod.Quests.Add(quest);

        _mod.ModHeader.Stats.NextFormID = _reg.Max + 1;
        return _mod;
    }

    // ================================================================ PNJ

    Npc BuildNpc(Faction faction, Spell ability)
    {
        var tpl = _esm.Npcs.First(n => n.FormKey == Vanilla.FaceTemplate);
        var npc = tpl.Duplicate(Key("NPCH_Faelia"));
        npc.EditorID = "NPCH_Faelia";
        npc.Name = "Faelia";
        npc.ShortName = "Faelia";
        npc.Race.SetTo(Vanilla.WoodElfRace);
        npc.Voice.SetTo(Vanilla.FemaleYoungEager);
        npc.Class.SetTo(Vanilla.ClassTrainerAlchemyExpert); // formatrice d'alchimie
        npc.DefaultOutfit.SetTo(Vanilla.HunterOutfit01);
        npc.Template.Clear();
        npc.Configuration.Flags = NpcConfiguration.Flag.Female | NpcConfiguration.Flag.Essential
                                  | NpcConfiguration.Flag.Unique | NpcConfiguration.Flag.AutoCalcStats;
        npc.Configuration.TemplateFlags = 0;
        npc.Configuration.Level = new NpcLevel { Level = 10 };
        npc.Factions.Clear();
        npc.Factions.Add(new RankPlacement { Faction = faction.ToLink(), Rank = 0 });
        npc.Packages.Clear();
        npc.Items = null;
        npc.Perks = null;
        npc.Keywords = null;
        npc.VirtualMachineAdapter = null;
        npc.ActorEffect = new ExtendedList<IFormLinkGetter<ISpellRecordGetter>> { ability.ToLink() };
        npc.AIData.Aggression = Aggression.Unaggressive;
        npc.AIData.Confidence = Confidence.Cowardly;
        npc.AIData.Responsibility = Responsibility.NoCrime;
        npc.AIData.Assistance = Assistance.HelpsNobody;
        npc.AIData.Mood = Mood.Happy;
        npc.AIData.EnergyLevel = 80;
        _mod.Npcs.Add(npc);
        return npc;
    }

    // ================================================================ utilitaires

    Message AddNotification(string editorId, string text, Quest quest)
    {
        Reserve(editorId);
        var m = new Message(_mod, editorId) { Name = "Faelia", Description = text, Flags = 0 }; // sans MessageBox = notification
        m.Quest.SetTo(quest);
        _mod.Messages.Add(m);
        return m;
    }

    GlobalShort AddGlobalShort(string editorId, short value)
    {
        var g = new GlobalShort(Key(editorId), SkyrimRelease.SkyrimSE) { EditorID = editorId, Data = value };
        _mod.Globals.Add(g);
        return g;
    }

    GlobalFloat AddGlobalFloat(string editorId, float value)
    {
        var g = new GlobalFloat(Key(editorId), SkyrimRelease.SkyrimSE) { EditorID = editorId, Data = value };
        _mod.Globals.Add(g);
        return g;
    }

    /// <summary>
    /// Topic (sans branche, joué par script via Say) dont chaque réplique réutilise une réplique vanilla
    /// (ResponseData = « Shared Info ») : texte et audio vanilla, restreints à Faelia.
    /// </summary>
    DialogTopic AddSharedLinesTopic(string editorId, Quest quest, Npc npc, uint[] vanillaInfoIds)
    {
        Reserve(editorId);
        var topic = new DialogTopic(_mod, editorId)
        {
            Category = DialogTopic.CategoryEnum.Topic,
            Subtype = DialogTopic.SubtypeEnum.Custom,
            SubtypeName = new RecordType("CUST"),
            Priority = 50f,
        };
        topic.Quest.SetTo(quest);
        int i = 1;
        foreach (var vid in vanillaInfoIds)
        {
            var info = new DialogResponses(Key($"{editorId}_Info{i++}"), SkyrimRelease.SkyrimSE)
            {
                Flags = new DialogResponseFlags { Flags = DialogResponses.Flag.Random },
            };
            info.ResponseData.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), vid));
            var isFaelia = new GetIsIDConditionData { RunOnType = Condition.RunOnType.Subject };
            isFaelia.Object.Link.SetTo(npc.FormKey);
            info.Conditions.Add(new ConditionFloat { CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f, Data = isFaelia });
            topic.Responses.Add(info);
        }
        _mod.DialogTopics.Add(topic);
        return topic;
    }

    void AddInteriorCell(Cell cell)
    {
        // Règle Bethesda : bloc = dernier chiffre décimal de l'ID, sous-bloc = avant-dernier
        var id = cell.FormKey.ID;
        int blockNum = (int)(id % 10), subNum = (int)(id / 10 % 10);
        var block = _mod.Cells.Records.FirstOrDefault(b => b.BlockNumber == blockNum);
        if (block == null)
        {
            block = new CellBlock { BlockNumber = blockNum, GroupType = GroupTypeEnum.InteriorCellBlock };
            _mod.Cells.Records.Add(block);
        }
        var sub = block.SubBlocks.FirstOrDefault(s => s.BlockNumber == subNum);
        if (sub == null)
        {
            sub = new CellSubBlock { BlockNumber = subNum, GroupType = GroupTypeEnum.InteriorCellSubBlock };
            block.SubBlocks.Add(sub);
        }
        sub.Cells.Add(cell);
    }

    static FormKey FindEditorId<T>(IEnumerable<T> recs, string editorId) where T : IMajorRecordGetter =>
        recs.FirstOrDefault(r => r.EditorID == editorId)?.FormKey
        ?? throw new InvalidOperationException($"EditorID introuvable dans Skyrim.esm : {editorId}");
}
