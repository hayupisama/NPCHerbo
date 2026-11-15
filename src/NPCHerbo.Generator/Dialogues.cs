using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace NPCHerbo.Generator;

/// <summary>Dialogues de Faelia (texte FR sans voix : Fuz Ro D-oh affiche les sous-titres).</summary>
public sealed partial class PluginBuilder
{
    const string FragmentScript = "TIF_NPCH_Dialogue";

    /// <summary>Paliers du sac : prix pour atteindre le palier, capacité obtenue.
    /// DOIT rester synchronisé avec NPCH_MainQuestScript.PriceForTier / CapacityForTier.</summary>
    static readonly (int Price, int Capacity)[] BagTiers =
    {
        (1000, 600),    // palier 1
        (3000, 1000),   // palier 2
        (7500, 1500),   // palier 3
        (15000, 2500),  // palier 4
    };

    record Line(string? Prompt, string Response, string? Fragment, params Condition[] Conditions);

    void BuildDialogues(Quest quest, Npc npc, Global gBagTier, Global gHarvestEnabled)
    {
        // 1. Besace
        AddTopLevel("NPCH_TopicShowBag", "Montre-moi ta besace.", quest, npc,
            new Line(null, "Bien sûr ! Servez-vous, tout est bien rangé.", "Fragment_OpenBag"));

        // 2. État de la besace
        AddTopLevel("NPCH_TopicBagStatus", "Où en est ta besace ?", quest, npc,
            new Line(null, "Laissez-moi compter...", "Fragment_BagStatus"));

        // 3. Achat d'un sac plus grand : une réplique par palier, selon l'or du joueur
        var upgrade = new List<Line>();
        for (int tier = 0; tier < BagTiers.Length; tier++)
        {
            var (price, cap) = BagTiers[tier];
            upgrade.Add(new Line(
                $"Voici {price} pièces d'or pour un sac plus grand ({cap} places).",
                $"Oh, merci ! Avec ça, je pourrai porter jusqu'à {cap} ingrédients !",
                "Fragment_UpgradeBag",
                GlobalEquals(gBagTier, tier), PlayerGold(CompareOperator.GreaterThanOrEqualTo, price)));
            upgrade.Add(new Line(
                "J'aimerais t'offrir un plus grand sac.",
                $"C'est gentil, mais un sac de {cap} places coûte {price} pièces d'or. Revenez quand vous les aurez !",
                null,
                GlobalEquals(gBagTier, tier), PlayerGold(CompareOperator.LessThan, price)));
        }
        AddTopLevel("NPCH_TopicUpgradeBag", "J'aimerais t'offrir un plus grand sac.", quest, npc, upgrade.ToArray());

        // 4. Arrêter / reprendre la cueillette
        AddTopLevel("NPCH_TopicHarvestToggle", "Arrête de cueillir pour l'instant.", quest, npc,
            new Line("Arrête de cueillir pour l'instant.", "D'accord, je reste près de vous.", "Fragment_HarvestOff",
                GlobalEquals(gHarvestEnabled, 1)),
            new Line("Tu peux recommencer à cueillir.", "Avec plaisir ! J'ouvre l'œil.", "Fragment_HarvestOn",
                GlobalEquals(gHarvestEnabled, 0)));
    }

    /// <summary>Branche de premier niveau + son topic. Chaque réplique est réservée à Faelia.</summary>
    void AddTopLevel(string topicEditorId, string playerText, Quest quest, Npc npc, params Line[] lines)
    {
        var branchEditorId = topicEditorId.Replace("Topic", "Branch");
        Reserve(branchEditorId);
        var branch = new DialogBranch(_mod, branchEditorId)
        {
            Category = DialogBranch.CategoryType.Player,
            Flags = DialogBranch.Flag.TopLevel,
        };
        branch.Quest.SetTo(quest);

        Reserve(topicEditorId);
        var topic = new DialogTopic(_mod, topicEditorId)
        {
            Name = playerText,
            Category = DialogTopic.CategoryEnum.Topic,
            Subtype = DialogTopic.SubtypeEnum.Custom,
            SubtypeName = new RecordType("CUST"),
            Priority = 50f,
        };
        topic.Quest.SetTo(quest);
        topic.Branch.SetTo(branch);
        branch.StartingTopic.SetTo(topic);

        int i = 1;
        foreach (var line in lines)
        {
            var info = new DialogResponses(Key($"{topicEditorId}_Info{i++}"), SkyrimRelease.SkyrimSE)
            {
                Flags = new DialogResponseFlags(),
            };
            if (line.Prompt != null) info.Prompt = line.Prompt;
            info.Responses.Add(new DialogResponse
            {
                Text = line.Response,
                ResponseNumber = 1,
                Emotion = Emotion.Neutral,
                EmotionValue = 50,
            });
            info.Conditions.Add(IsFaelia(npc));
            info.Conditions.AddRange(line.Conditions);
            if (line.Fragment != null)
            {
                info.VirtualMachineAdapter = new DialogResponsesAdapter
                {
                    Scripts = { new ScriptEntry { Name = FragmentScript } },
                    ScriptFragments = new ScriptFragments
                    {
                        FileName = FragmentScript,
                        OnEnd = new ScriptFragment { ScriptName = FragmentScript, FragmentName = line.Fragment },
                    },
                };
            }
            topic.Responses.Add(info);
        }
        _mod.DialogBranches.Add(branch);
        _mod.DialogTopics.Add(topic);
    }

    // ---------------------------------------------------------------- conditions

    static Condition IsFaelia(Npc npc)
    {
        var d = new GetIsIDConditionData { RunOnType = Condition.RunOnType.Subject };
        d.Object.Link.SetTo(npc.FormKey);
        return new ConditionFloat { CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f, Data = d };
    }

    static Condition GlobalEquals(Global g, int value)
    {
        var d = new GetGlobalValueConditionData { RunOnType = Condition.RunOnType.Subject };
        d.Global.Link.SetTo(g.FormKey);
        return new ConditionFloat { CompareOperator = CompareOperator.EqualTo, ComparisonValue = value, Data = d };
    }

    static Condition PlayerGold(CompareOperator op, int amount)
    {
        var d = new GetItemCountConditionData { RunOnType = Condition.RunOnType.Reference };
        d.Reference.SetTo(Vanilla.PlayerRef);
        d.ItemOrList.Link.SetTo(Vanilla.Gold001);
        return new ConditionFloat { CompareOperator = op, ComparisonValue = amount, Data = d };
    }
}
