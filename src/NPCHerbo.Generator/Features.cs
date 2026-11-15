using System.Text;
using System.Text.Json;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace NPCHerbo.Generator;

/// <summary>Fonctionnalités v2 : savoir, filtres de cueillette, dépôt, menu MCM (MCM Helper).</summary>
public sealed partial class PluginBuilder
{
    /// <summary>Contenu MCM généré (écrit par Program.cs à côté du plugin).</summary>
    public string? McmConfigJson { get; private set; }
    public string? McmTranslations { get; private set; }

    const string ModFile = "NPCHerbo.esp";

    void AddFeatures(Quest quest, Npc npc)
    {
        // ------------------------------------------------------------ réglages
        var gDailyLesson = AddGlobalShort("NPCH_DailyLesson", 1);
        var gWeather = AddGlobalShort("NPCH_WeatherFlavour", 1);
        var gCorpses = AddGlobalShort("NPCH_LootCorpses", 1);
        var gRareAlert = AddGlobalShort("NPCH_RareAlert", 1);
        var gRareOnly = AddGlobalShort("NPCH_RareOnly", 0);
        var gTarget = AddGlobalShort("NPCH_TargetEffect", 0);
        var gDepositSet = AddGlobalShort("NPCH_DepositSet", 0);
        var gLastSet = AddGlobalShort("NPCH_LastHarvestSet", 0);
        var gExclCount = AddGlobalShort("NPCH_ExclusionCount", 0);

        // ------------------------------------------------------------ ingrédients rares
        Reserve("NPCH_RareIngredients");
        var rareList = new FormList(_mod, "NPCH_RareIngredients");
        foreach (var id in Vanilla.RareIngredients) rareList.Items.Add(new FormLink<ISkyrimMajorRecordGetter>(id));
        _mod.FormLists.Add(rareList);

        // les Nirnroot sont des ACTIVATEURS scriptés (pas des Flora, et sans mot-clé) : liste explicite
        Reserve("NPCH_NirnrootActivators");
        var nirnList = new FormList(_mod, "NPCH_NirnrootActivators");
        nirnList.Items.Add(new FormLink<ISkyrimMajorRecordGetter>(FindEditorId(_esm.EnumerateMajorRecords(), "TreeFloraNirnroot01")));
        nirnList.Items.Add(new FormLink<ISkyrimMajorRecordGetter>(FindEditorId(_esm.EnumerateMajorRecords(), "TreeFloraNirnrootRed01")));
        _mod.FormLists.Add(nirnList);

        // ------------------------------------------------------------ alias du coffre de dépôt
        const uint AliasDeposit = 4;
        quest.Aliases.Add(new QuestAlias
        {
            ID = AliasDeposit,
            Name = "DepositChest",
            Type = QuestAlias.TypeEnum.Reference,
            Flags = QuestAlias.Flag.Optional | QuestAlias.Flag.AllowReserved | QuestAlias.Flag.AllowReuseInQuest,
        });
        quest.NextAliasID = 5;

        // ------------------------------------------------------------ message carnet
        Reserve("NPCH_MsgDiscoveries");
        var msgDisc = new Message(_mod, "NPCH_MsgDiscoveries")
        {
            Name = "Faelia",
            Description = "Faelia a découvert %.0f sortes d'ingrédients.",
            Flags = 0,
        };
        msgDisc.Quest.SetTo(quest);
        _mod.Messages.Add(msgDisc);

        // ------------------------------------------------------------ menu de réglages intégré (boîtes de dialogue natives :
        // fonctionne même si SkyUI a atteint sa limite de 128 menus MCM)
        var msgSettings1 = AddMenuMessage("NPCH_MsgSettings1", quest,
            "Réglages de Faelia\n\nRayon de recherche : %.0f m\nPlantes par tournée : %.0f\nRéactions vocales : %.0f sur 100\n" +
            "Notifications de cueillette : %.0f\nLogs à l'écran : %.0f\n\n(1 = oui, 0 = non)",
            "Rayon -5 m", "Rayon +5 m", "Tournée -1", "Tournée +1", "Réactions vocales", "Notifications",
            "Logs à l'écran", "Plus de réglages...", "Fermer");
        var msgSettings2 = AddMenuMessage("NPCH_MsgSettings2", quest,
            "Réglages de Faelia (suite)\n\nRécolte active : %.0f\nCréatures mortes : %.0f\nLeçon du jour : %.0f\n" +
            "Nuit et météo : %.0f\nAlerte plantes rares : %.0f\n\n(1 = oui, 0 = non)",
            "Récolte", "Créatures mortes", "Leçon du jour", "Nuit et météo", "Alerte rares", "Retour", "Fermer");

        // ------------------------------------------------------------ script des fonctionnalités (même quête)
        ScriptObjectProperty Obj(string name, FormKey fk) => new() { Name = name, Object = new FormLink<ISkyrimMajorRecordGetter>(fk) };
        ScriptObjectProperty G(string name, Global g) => Obj(name, g.FormKey);
        FormKey Esm(string editorId) => FindEditorId(_esm.EnumerateMajorRecords(), editorId);

        var vmad = quest.VirtualMachineAdapter!;
        vmad.Scripts[0].Properties.Add(Obj("Features", quest.FormKey));
        vmad.Scripts.Add(new ScriptEntry
        {
            Name = "NPCH_FeaturesScript",
            Properties =
            {
                Obj("Main", quest.FormKey),
                G("NPCH_DailyLesson", gDailyLesson),
                G("NPCH_WeatherFlavour", gWeather),
                G("NPCH_LootCorpses", gCorpses),
                G("NPCH_RareAlert", gRareAlert),
                G("NPCH_RareOnly", gRareOnly),
                G("NPCH_TargetEffect", gTarget),
                G("NPCH_DepositSet", gDepositSet),
                G("NPCH_LastHarvestSet", gLastSet),
                G("NPCH_ExclusionCount", gExclCount),
                Obj("RareIngredients", rareList.FormKey),
                Obj("EffRestoreHealth", Esm("AlchRestoreHealth")),
                Obj("EffRestoreHealthAll", Esm("AlchRestoreHealthAll")),
                Obj("EffRestoreMagicka", Esm("AlchRestoreMagicka")),
                Obj("EffRestoreMagickaAll", Esm("AlchRestoreMagickaAll")),
                Obj("EffRestoreStamina", Esm("AlchRestoreStamina")),
                Obj("EffRestoreStaminaAll", Esm("AlchRestoreStaminaAll")),
                Obj("KwActorTypeAnimal", Esm("ActorTypeAnimal")),
                Obj("KwActorTypeCreature", Esm("ActorTypeCreature")),
                Obj("NirnrootActivators", nirnList.FormKey),
                Obj("LocTypePlayerHouse", Esm("LocTypePlayerHouse")),
                Obj("NirnrootRedActivator", Esm("TreeFloraNirnrootRed01")),
                Obj("IngNirnroot", Esm("Nirnroot")),
                Obj("IngNirnrootRed", Esm("NirnrootRed")),
                new ScriptObjectProperty { Name = "DepositChestAlias", Object = quest.ToLink<ISkyrimMajorRecordGetter>(), Alias = (short)AliasDeposit },
                Obj("MsgDiscoveries", msgDisc.FormKey),
                Obj("MsgSettings1", msgSettings1.FormKey),
                Obj("MsgSettings2", msgSettings2.FormKey),
            },
        });

        // ------------------------------------------------------------ dialogues
        AddTopLevel("NPCH_TopicTraining", "Tu pourrais m'apprendre l'alchimie ?", quest, npc,
            new Line(null, "Avec plaisir ! Voyons ce que vous savez déjà.", "Fragment_Training"));

        var gHarvestEnabled = _mod.Globals.First(g => g.EditorID == "NPCH_HarvestEnabled");

        // Sous-menu « cueillette » (les FormIDs des répliques existantes sont conservés via leurs EditorID)
        var targetChildren = new[]
        {
            Leaf("NPCH_TopicTargetHeal", "De quoi soigner.", "Je ne cueillerai que ce qui soigne.", "Fragment_TargetHeal"),
            Leaf("NPCH_TopicTargetMagicka", "De quoi restaurer la magie.", "Je cherche ce qui ravive la magie.", "Fragment_TargetMagicka"),
            Leaf("NPCH_TopicTargetStamina", "De quoi restaurer la vigueur.", "Va pour la vigueur !", "Fragment_TargetStamina"),
            Leaf("NPCH_TopicTargetPoison", "De quoi faire des poisons.", "Hmm... je garderai mes gants.", "Fragment_TargetPoison"),
            Leaf("NPCH_TopicTargetRare", "Seulement les ingrédients rares.", "Je ne m'arrêterai que pour les vraies trouvailles.", "Fragment_TargetRare"),
            Leaf("NPCH_TopicTargetClear", "Cueille de nouveau de tout.", "Comme avant, alors !", "Fragment_TargetClear",
                Or(GlobalCompare(gTarget, CompareOperator.GreaterThan, 0)), GlobalEquals(gRareOnly, 1)),
            Leaf("NPCH_TopicTargetNone", "Rien, laisse.", "D'accord.", null),
        };
        AddMenuBranch("NPCH_BranchHarvestMenu", new MenuNode("NPCH_TopicHarvestMenu", "Parlons de ta cueillette.",
            new[] { new Line(null, "Oui ?", null) },
            new[]
            {
                new MenuNode("NPCH_TopicHarvestToggle", "Arrête de cueillir pour l'instant.", new[]
                {
                    new Line("Arrête de cueillir pour l'instant.", "D'accord, je reste près de vous.", "Fragment_HarvestOff", GlobalEquals(gHarvestEnabled, 1)),
                    new Line("Tu peux recommencer à cueillir.", "Avec plaisir ! J'ouvre l'œil.", "Fragment_HarvestOn", GlobalEquals(gHarvestEnabled, 0)),
                }, Array.Empty<MenuNode>()),
                new MenuNode("NPCH_TopicTargeted", "J'ai besoin d'ingrédients précis.", new[] { new Line(null, "Lesquels ?", null) }, targetChildren),
                Leaf("NPCH_TopicExcludeLast", "Ne cueille plus ce que tu viens de ramasser.", "Entendu, je le laisserai pousser tranquille.",
                    "Fragment_ExcludeLast", GlobalEquals(gLastSet, 1)),
                Leaf("NPCH_TopicResetExclusions", "Tu peux de nouveau tout cueillir.", "Parfait, j'ouvre l'œil pour tout ce qui pousse !",
                    "Fragment_ResetExclusions", GlobalCompare(gExclCount, CompareOperator.GreaterThan, 0)),
                Leaf("NPCH_TopicSettings", "Changeons tes réglages.", "Bien sûr, dites-moi.", "Fragment_Settings"),
            }), quest, npc);

        // Sous-menu « maison »
        AddMenuBranch("NPCH_BranchHomeMenu", new MenuNode("NPCH_TopicHomeMenu", "À propos de notre maison...",
            new[] { new Line(null, "Oui ?", null) },
            new[]
            {
                Leaf("NPCH_TopicSetDeposit", "Ce coffre sera ton dépôt.", "Voyons voir...", "Fragment_SetDeposit", PlayerInInterior()),
                Leaf("NPCH_TopicDepositAll", "Range ta besace dans notre coffre.", "Tout de suite !", "Fragment_DepositAll", GlobalEquals(gDepositSet, 1)),
            }), quest, npc);

        // ------------------------------------------------------------ menu MCM (MCM Helper : config JSON reliée aux globales)
        BuildMcm(quest);
    }

    /// <summary>Nœud d'un menu de dialogue : un topic, ses répliques, et ses sous-choix (reliés par LinkTo).</summary>
    record MenuNode(string Id, string Player, Line[] Lines, MenuNode[] Children);

    static MenuNode Leaf(string id, string player, string response, string? fragment, params Condition[] conds) =>
        new(id, player, new[] { new Line(null, response, fragment, conds) }, Array.Empty<MenuNode>());

    /// <summary>Branche de premier niveau dont le topic racine ouvre un sous-menu (arbre de topics liés par LinkTo).</summary>
    void AddMenuBranch(string branchId, MenuNode root, Quest quest, Npc npc)
    {
        Reserve(branchId);
        var branch = new DialogBranch(_mod, branchId)
        {
            Category = DialogBranch.CategoryType.Player,
            Flags = DialogBranch.Flag.TopLevel,
        };
        branch.Quest.SetTo(quest);

        DialogTopic Build(MenuNode node)
        {
            Reserve(node.Id); // juste avant la création : le FormID stable est consommé par ce « new »
            var topic = new DialogTopic(_mod, node.Id)
            {
                Name = node.Player,
                Category = DialogTopic.CategoryEnum.Topic,
                Subtype = DialogTopic.SubtypeEnum.Custom,
                SubtypeName = new RecordType("CUST"),
                Priority = 50f,
            };
            topic.Quest.SetTo(quest);
            topic.Branch.SetTo(branch);
            var children = node.Children.Select(Build).ToList();
            int i = 1;
            foreach (var line in node.Lines)
            {
                var info = MakeInfo($"{node.Id}_Info{i++}", line, npc);
                foreach (var child in children) info.LinkTo.Add(child.ToLink());
                topic.Responses.Add(info);
            }
            _mod.DialogTopics.Add(topic);
            return topic;
        }

        branch.StartingTopic.SetTo(Build(root));
        _mod.DialogBranches.Add(branch);
    }

    DialogResponses MakeInfo(string key, Line line, Npc npc)
    {
        var info = new DialogResponses(Key(key), SkyrimRelease.SkyrimSE) { Flags = new DialogResponseFlags() };
        if (line.Prompt != null) info.Prompt = line.Prompt;
        info.Responses.Add(new DialogResponse { Text = line.Response, ResponseNumber = 1, Emotion = Emotion.Neutral, EmotionValue = 50 });
        info.Conditions.Add(IsFaelia(npc));
        info.Conditions.AddRange(line.Conditions);
        if (line.Fragment != null)
            info.VirtualMachineAdapter = new DialogResponsesAdapter
            {
                Scripts = { new ScriptEntry { Name = FragmentScript } },
                ScriptFragments = new ScriptFragments
                {
                    FileName = FragmentScript,
                    OnEnd = new ScriptFragment { ScriptName = FragmentScript, FragmentName = line.Fragment },
                },
            };
        return info;
    }

    /// <summary>Boîte de dialogue (MessageBox) avec boutons ; les %.0f sont remplis par Message.Show(...).</summary>
    Message AddMenuMessage(string editorId, Quest quest, string text, params string[] buttons)
    {
        Reserve(editorId);
        var m = new Message(_mod, editorId) { Name = "Faelia", Description = text, Flags = Message.Flag.MessageBox };
        m.Quest.SetTo(quest);
        foreach (var b in buttons) m.MenuButtons.Add(new MessageButton { Text = b });
        _mod.Messages.Add(m);
        return m;
    }

    /// <summary>Marque une condition « OU » avec la suivante.</summary>
    static Condition Or(Condition c)
    {
        c.Flags |= Condition.Flag.OR;
        return c;
    }

    static Condition PlayerInInterior()
    {
        var d = new IsInInteriorConditionData { RunOnType = Condition.RunOnType.Reference };
        d.Reference.SetTo(Vanilla.PlayerRef);
        return new ConditionFloat { CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f, Data = d };
    }

    static Condition GlobalCompare(Global g, CompareOperator op, int value)
    {
        var d = new GetGlobalValueConditionData { RunOnType = Condition.RunOnType.Subject };
        d.Global.Link.SetTo(g.FormKey);
        return new ConditionFloat { CompareOperator = op, ComparisonValue = value, Data = d };
    }

    // ================================================================ MCM

    void BuildMcm(Quest mainQuest)
    {
        // Quête MCM : MCM_ConfigBase (MCM Helper) attaché directement + alias joueur SkyUI (ré-enregistrement au chargement)
        Reserve("NPCH_MCMQuest");
        var mcm = new Quest(_mod, "NPCH_MCMQuest")
        {
            Name = "NPCHerbo - Menu",
            Flags = Quest.Flag.StartGameEnabled | Quest.Flag.RunOnce,
            Priority = 10,
        };
        mcm.Aliases.Add(new QuestAlias
        {
            ID = 0,
            Name = "PlayerAlias",
            Type = QuestAlias.TypeEnum.Reference,
            ForcedReference = new FormLinkNullable<IPlacedGetter>(Vanilla.PlayerRef),
        });
        mcm.NextAliasID = 1;
        mcm.VirtualMachineAdapter = new QuestAdapter
        {
            Scripts =
            {
                new ScriptEntry
                {
                    Name = "MCM_ConfigBase",
                    Properties = { new ScriptStringProperty { Name = "ModName", Data = "NPCHerbo" } },
                },
            },
            Aliases =
            {
                new QuestFragmentAlias
                {
                    Property = new ScriptObjectProperty { Name = "", Object = mcm.ToLink<ISkyrimMajorRecordGetter>(), Alias = 0 },
                    Scripts = { new ScriptEntry { Name = "SKI_PlayerLoadGameAlias" } },
                },
            },
        };
        _mod.Quests.Add(mcm);

        string Ref(string editorId) => $"{ModFile}|{_reg.Get(editorId):X}";
        object Slider(string key, string glob, double min, double max, double step, string fmt = "{0}") => new
        {
            text = $"$NPCH_{key}", help = $"$NPCH_{key}_Help", type = "slider",
            valueOptions = new { min, max, step, formatString = fmt, sourceType = "GlobalValue", sourceForm = Ref(glob) },
        };
        object Toggle(string key, string glob) => new
        {
            text = $"$NPCH_{key}", help = $"$NPCH_{key}_Help", type = "toggle",
            valueOptions = new { sourceType = "GlobalValue", sourceForm = Ref(glob) },
        };
        object Header(string key) => new { text = $"$NPCH_{key}", type = "header" };
        object Button(string key, string script, string function) => new
        {
            text = $"$NPCH_{key}", help = $"$NPCH_{key}_Help", type = "text",
            action = new { type = "CallFunction", form = Ref("NPCH_MainQuest"), scriptName = script, function },
        };

        var config = new
        {
            modName = "NPCHerbo",
            displayName = "$NPCH_ModName",
            minMcmVersion = 9,
            pages = new object[]
            {
                new
                {
                    pageDisplayName = "$NPCH_PageHarvest",
                    cursorFillMode = "topToBottom",
                    content = new object[]
                    {
                        Header("HdrHarvest"),
                        Toggle("HarvestEnabled", "NPCH_HarvestEnabled"),
                        Slider("ScanRadius", "NPCH_ScanRadius", 500, 4000, 50),
                        Slider("BatchStartDist", "NPCH_BatchStartDist", 300, 3000, 50),
                        Slider("BatchLinkDist", "NPCH_BatchLinkDist", 100, 1500, 25),
                        Slider("BatchMaxPlants", "NPCH_BatchMaxPlants", 1, 20, 1),
                        Slider("BatchMaxTime", "NPCH_BatchMaxTime", 5, 120, 5, "{0} s"),
                        Toggle("LootCorpses", "NPCH_LootCorpses"),
                        Header("HdrFilters"),
                        new
                        {
                            text = "$NPCH_TargetEffect", help = "$NPCH_TargetEffect_Help", type = "enum",
                            valueOptions = new
                            {
                                options = new[] { "$NPCH_TgtAll", "$NPCH_TgtHeal", "$NPCH_TgtMagicka", "$NPCH_TgtStamina", "$NPCH_TgtPoison" },
                                sourceType = "GlobalValue", sourceForm = Ref("NPCH_TargetEffect"),
                            },
                        },
                        Toggle("RareOnly", "NPCH_RareOnly"),
                        Toggle("RareAlert", "NPCH_RareAlert"),
                        Button("ResetExclusions", "NPCH_FeaturesScript", "ResetExclusions"),
                    },
                },
                new
                {
                    pageDisplayName = "$NPCH_PageFaelia",
                    cursorFillMode = "topToBottom",
                    content = new object[]
                    {
                        Header("HdrFaelia"),
                        Slider("ReactionChance", "NPCH_ReactionChance", 0, 100, 5, "{0} %"),
                        Toggle("ShowHarvest", "NPCH_ShowHarvest"),
                        Toggle("DailyLesson", "NPCH_DailyLesson"),
                        Toggle("WeatherFlavour", "NPCH_WeatherFlavour"),
                        Header("HdrDebug"),
                        Toggle("Debug", "NPCH_Debug"),
                    },
                },
            },
        };
        McmConfigJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        // Traductions SkyUI ($clé<TAB>texte), écrites en UTF-16 LE par Program.cs
        var t = new (string Key, string Text)[]
        {
            ("ModName", "NPCHerbo - Faelia"),
            ("PageHarvest", "Récolte"), ("PageFaelia", "Faelia"),
            ("HdrHarvest", "Tournées de récolte"), ("HdrFilters", "Préférences de cueillette"),
            ("HdrFaelia", "Comportement"), ("HdrDebug", "Développement"),
            ("HarvestEnabled", "Récolte active"), ("HarvestEnabled_Help", "Faelia cueille les ingrédients autour de vous."),
            ("ScanRadius", "Rayon de recherche"), ("ScanRadius_Help", "Distance autour de vous où Faelia repère les plantes (70 unités = 1 m)."),
            ("BatchStartDist", "Distance de la 1re plante"), ("BatchStartDist_Help", "Une tournée démarre si une plante est à moins de cette distance de vous."),
            ("BatchLinkDist", "Écart entre deux plantes"), ("BatchLinkDist_Help", "Distance maximale entre deux plantes d'une même tournée."),
            ("BatchMaxPlants", "Plantes par tournée"), ("BatchMaxPlants_Help", "Nombre maximal de plantes avant de revenir vers vous."),
            ("BatchMaxTime", "Durée max d'une tournée"), ("BatchMaxTime_Help", "Au-delà, Faelia revient vers vous."),
            ("LootCorpses", "Créatures mortes"), ("LootCorpses_Help", "Faelia prélève les ingrédients sur les créatures mortes (jamais sur les humanoïdes)."),
            ("TargetEffect", "Récolte ciblée"), ("TargetEffect_Help", "Ne cueillir que les ingrédients ayant cet effet. Les plantes rares sont toujours cueillies."),
            ("TgtAll", "Tout"), ("TgtHeal", "Soins"), ("TgtMagicka", "Magie"), ("TgtStamina", "Vigueur"), ("TgtPoison", "Poisons"),
            ("RareOnly", "Seulement les rares"), ("RareOnly_Help", "Faelia ne s'arrête que pour les ingrédients rares (Nirnroot...)."),
            ("RareAlert", "Alerte plantes rares"), ("RareAlert_Help", "Faelia signale les plantes rares et fait un détour pour les cueillir."),
            ("ResetExclusions", "Oublier les exclusions"), ("ResetExclusions_Help", "Faelia cueillera de nouveau les ingrédients que vous lui aviez demandé d'ignorer."),
            ("ReactionChance", "Réactions vocales"), ("ReactionChance_Help", "Chance que Faelia commente une trouvaille (0 = muette)."),
            ("ShowHarvest", "Notifications de cueillette"), ("ShowHarvest_Help", "Affiche « Faelia : +3 Lys des cimes » à chaque cueillette."),
            ("DailyLesson", "Leçon du jour"), ("DailyLesson_Help", "Une fois par jour, Faelia vous apprend un effet inconnu d'un ingrédient de sa besace."),
            ("WeatherFlavour", "Nuit et météo"), ("WeatherFlavour_Help", "Répliques d'ambiance et petit bonus de cueillette sous la pluie ou la nuit."),
            ("Debug", "Notifications de debug"), ("Debug_Help", "Affiche le détail des actions de Faelia (le journal Papyrus est toujours complet)."),
        };
        var sb = new StringBuilder();
        foreach (var (key, text) in t) sb.Append($"$NPCH_{key}\t{text}\r\n");
        McmTranslations = sb.ToString();
    }
}
