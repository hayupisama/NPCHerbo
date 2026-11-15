using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using NPCHerbo.Generator;

// Dossier Data du jeu : variable d'environnement NPCH_GAME_DATA (définie par tools\build.ps1), sinon chemin Steam par défaut
var GameData = Environment.GetEnvironmentVariable("NPCH_GAME_DATA")
               ?? @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition\Data";
var skyrimEsm = Path.Combine(GameData, "Skyrim.esm");

if (args.Length > 0 && args[0] == "explore")
{
    Explore.Run(skyrimEsm);
    return 0;
}

if (args.Length > 1 && args[0] == "explore-lines")
{
    Explore.RunLines(skyrimEsm, args.Skip(1).ToArray());
    return 0;
}

if (args.Length > 0 && args[0] == "explore-voice")
{
    Explore.RunVoiceLines(skyrimEsm, 0x013ADC, args.Skip(1).ToArray());
    return 0;
}

if (args.Length > 1 && args[0] == "find")
{
    // dotnet run -- find <motif>... : enregistrements de Skyrim.esm dont l'EditorID contient un motif (+ nom FR, valeur)
    using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsm, SkyrimRelease.SkyrimSE,
        new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters
        { StringsParam = new Mutagen.Bethesda.Strings.StringsReadParameters { TargetLanguage = Mutagen.Bethesda.Strings.Language.French } });
    foreach (var r in esm.EnumerateMajorRecords())
        if (r.EditorID != null && args.Skip(1).Any(a => r.EditorID.Contains(a, StringComparison.OrdinalIgnoreCase)))
        {
            var extra = r switch
            {
                IIngredientGetter i => $"\"{i.Name?.String}\" valeur={i.Value}",
                IMagicEffectGetter m => $"\"{m.Name?.String}\"",
                IFloraGetter fl => $"\"{fl.Name?.String}\" ingr={fl.Ingredient.FormKey}",
                _ => "",
            };
            Console.WriteLine($"{r.FormKey} {r.GetType().Name.Replace("BinaryOverlay", "")} {r.EditorID} {extra}");
        }
    return 0;
}

if (args.Length > 2 && args[0] == "bsa-list")
{
    // dotnet run -- bsa-list <archive.bsa> <motif> : fichiers d'une archive dont le chemin contient le motif
    var reader = Mutagen.Bethesda.Archives.Archive.CreateReader(GameRelease.SkyrimSE, args[1]);
    foreach (var f in reader.Files.Where(f => f.Path.Contains(args[2], StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(f.Path);
    return 0;
}

if (args.Length > 3 && args[0] == "bsa-extract")
{
    // dotnet run -- bsa-extract <archive.bsa> <motif> <dossier> : extrait les fichiers correspondants
    var reader = Mutagen.Bethesda.Archives.Archive.CreateReader(GameRelease.SkyrimSE, args[1]);
    foreach (var f in reader.Files.Where(f => f.Path.Contains(args[2], StringComparison.OrdinalIgnoreCase)))
    {
        var dest = Path.Combine(args[3], Path.GetFileName(f.Path));
        Directory.CreateDirectory(args[3]);
        File.WriteAllBytes(dest, f.GetBytes());
        Console.WriteLine($"extrait : {dest}");
    }
    return 0;
}

if (args.Length > 1 && args[0] == "npcinfo")
{
    // dotnet run -- npcinfo <EditorID> : classe (formation), factions, et topics de sous-type Training
    using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsm, SkyrimRelease.SkyrimSE,
        new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters
        { StringsParam = new Mutagen.Bethesda.Strings.StringsReadParameters { TargetLanguage = Mutagen.Bethesda.Strings.Language.French } });
    var n = esm.Npcs.First(x => x.EditorID == args[1]);
    var cls = esm.Classes.First(c => c.FormKey == n.Class.FormKey);
    Console.WriteLine($"{n.EditorID} classe={cls.EditorID} ({cls.FormKey}) teaches={cls.Teaches} maxTraining={cls.MaxTrainingLevel}");
    foreach (var f in n.Factions)
    {
        var fac = esm.Factions.FirstOrDefault(x => x.FormKey == f.Faction.FormKey);
        Console.WriteLine($"  faction {fac?.EditorID ?? f.Faction.FormKey.ToString()} rank={f.Rank} flags={fac?.Flags}");
    }
    var trainerFac = esm.Factions.First(x => x.EditorID == "JobTrainerFaction").FormKey;
    Console.WriteLine($"JobTrainerFaction = {trainerFac}");
    foreach (var t in esm.DialogTopics.Where(t => t.Responses.Any(i => i.Conditions.Any(c =>
                 c.Data.EnumerateFormLinks().Any(l => l.FormKey == trainerFac)))))
    {
        Console.WriteLine($"TOPIC {t.EditorID} [{t.Subtype}] \"{t.Name?.String}\" branch={t.Branch.FormKey}");
        foreach (var i in t.Responses.Take(3))
            Console.WriteLine($"   INFO {i.FormKey.ID:X6} conds=" + string.Join(" & ", i.Conditions.Select(c => c.Data.GetType().Name.Replace("ConditionDataBinaryOverlay", "") + "(" +
                string.Join(",", c.Data.EnumerateFormLinks().Select(l => l.FormKey.ID.ToString("X6"))) + ")")));
    }
    return 0;
}

if (args.Length > 1 && args[0] == "cells")
{
    // dotnet run -- cells <motif>... : cellules intérieures vanilla dont l'EditorID contient un motif
    using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsm, SkyrimRelease.SkyrimSE);
    foreach (var c in esm.EnumerateMajorRecords<ICellGetter>())
        if (c.EditorID != null && c.Flags.HasFlag(Cell.Flag.IsInteriorCell)
            && args.Skip(1).Any(a => c.EditorID.Contains(a, StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"{c.FormKey} {c.EditorID} persistent={c.Persistent.Count} temporary={c.Temporary.Count}");
    return 0;
}

if (args.Length > 1 && args[0] == "reflect")
{
    // dotnet run -- reflect <Type1> <Type2>... : propriétés modifiables + enums imbriqués (aide au développement)
    var asm = typeof(DialogResponses).Assembly;
    foreach (var n in args.Skip(1))
    {
        var t = asm.GetTypes().First(x => x.Name == n && x.Namespace == "Mutagen.Bethesda.Skyrim");
        Console.WriteLine(n + ": " + string.Join(", ", t.GetProperties().Where(p => p.DeclaringType == t).Select(p => p.Name + ":" + p.PropertyType.Name)));
        foreach (var e in t.GetNestedTypes().Where(x => x.IsEnum))
            Console.WriteLine($"  enum {e.Name}: {string.Join(",", Enum.GetNames(e).Take(40))}");
    }
    return 0;
}

if (args.Length > 1 && args[0] == "explore-voicefiles")
{
    Explore.RunVoiceFiles(skyrimEsm, Path.Combine(GameData, "Skyrim - Voices_fr0.bsa"), "femaleyoungeager", args[1]);
    return 0;
}

if (args.Length > 0 && args[0] == "explore-travel")
{
    Explore.RunTravel(skyrimEsm);
    return 0;
}

if (args.Length > 0 && args[0] == "dump")
{
    // dotnet run -- dump <chemin.esp> : affiche le contenu d'un plugin (vérification)
    using var p = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    Console.WriteLine($"Header: version={p.ModHeader.Stats.Version} flags={p.ModHeader.Flags} next={p.ModHeader.Stats.NextFormID:X} masters={string.Join(",", p.ModHeader.MasterReferences.Select(m => m.Master))}");
    foreach (var r in p.EnumerateMajorRecords())
        Console.WriteLine($"  {r.FormKey} {r.GetType().Name.Replace("BinaryOverlay", "")} {r.EditorID}");
    return 0;
}

if (args.Length > 1 && args[0] == "dialogues")
{
    // dotnet run -- dialogues <plugin.esp> : relecture des branches, topics et répliques
    using var p = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    foreach (var b in p.DialogBranches)
        Console.WriteLine($"BRANCHE {b.EditorID} flags={b.Flags} cat={b.Category} start={b.StartingTopic.FormKey.ID:X6} quest={b.Quest.FormKey.ID:X6}");
    foreach (var t in p.DialogTopics)
    {
        Console.WriteLine($"TOPIC {t.EditorID} \"{t.Name?.String}\" branch={t.Branch.FormKey.ID:X6} cat={t.Category}/{t.Subtype}");
        foreach (var i in t.Responses)
        {
            var conds = string.Join(" & ", i.Conditions.Select(c =>
                $"{c.Data.GetType().Name.Replace("ConditionDataBinaryOverlay", "").Replace("ConditionData", "")}" +
                $"({string.Join(",", c.Data.EnumerateFormLinks().Select(l => l.FormKey.ID.ToString("X6")))}) {c.CompareOperator} {(c as IConditionFloatGetter)?.ComparisonValue}"));
            var frag = i.VirtualMachineAdapter?.ScriptFragments?.OnEnd;
            Console.WriteLine($"  INFO {i.FormKey.ID:X6} prompt=\"{i.Prompt?.String}\" shared={i.ResponseData.FormKey.ID:X6}");
            foreach (var r in i.Responses) Console.WriteLine($"     -> \"{r.Text?.String}\"");
            Console.WriteLine($"     conds: {conds}");
            if (frag != null) Console.WriteLine($"     fragment: {frag.ScriptName}.{frag.FragmentName} scripts={string.Join(",", i.VirtualMachineAdapter!.Scripts.Select(s => s.Name))}");
            if (i.LinkTo.Count > 0) Console.WriteLine($"     liens: {string.Join(",", i.LinkTo.Select(l => l.FormKey.ID.ToString("X6")))}");
            if (i.Conditions.Any(c => c.Flags.HasFlag(Condition.Flag.OR))) Console.WriteLine("     (contient une condition OU)");
        }
    }
    return 0;
}

if (args.Length > 2 && args[0] == "inspect")
{
    // dotnet run -- inspect <plugin.esp> <EditorID> : affichage détaillé d'un enregistrement
    using var p = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    var rec = p.EnumerateMajorRecords().First(r => r.EditorID == args[2]);
    Console.WriteLine($"{rec.FormKey} flagsRaw=0x{rec.MajorRecordFlagsRaw:X}");
    if (rec is IPackageGetter pk)
        foreach (var c in pk.Conditions)
            if (c.Data is IGetGlobalValueConditionDataGetter gv)
                Console.WriteLine($"  Condition GetGlobalValue({gv.Global.Link.FormKey}) {c.CompareOperator} {(c as IConditionFloatGetter)?.ComparisonValue}");
    if (rec is INpcGetter n)
    {
        Console.WriteLine($"  Name={n.Name} Race={n.Race.FormKey} Voice={n.Voice.FormKey} Class={n.Class.FormKey} Outfit={n.DefaultOutfit.FormKey}");
        Console.WriteLine($"  Flags={n.Configuration.Flags} Template={n.Template.FormKey} TemplateFlags={n.Configuration.TemplateFlags}");
        Console.WriteLine($"  AI: aggr={n.AIData.Aggression} conf={n.AIData.Confidence} assist={n.AIData.Assistance} resp={n.AIData.Responsibility}");
        Console.WriteLine($"  Spells={string.Join(",", n.ActorEffect?.Select(s => s.FormKey.ToString()) ?? [])} Factions={string.Join(",", n.Factions.Select(f => f.Faction.FormKey.ToString()))}");
        Console.WriteLine($"  Packages={n.Packages.Count} Items={n.Items?.Count ?? 0} Perks={n.Perks?.Count ?? 0} HeadParts={n.HeadParts.Count} Tints={n.TintLayers.Count}");
        Console.WriteLine($"  DefaultPackageList={n.DefaultPackageList.FormKey} CombatStyle={n.CombatStyle.FormKey} CrimeFaction={n.CrimeFaction.FormKey}");
        return 0;
    }
    Explore.DumpObj(rec, "  ", 0);
    return 0;
}

if (args.Length > 0 && args[0] == "build")
{
    var outPath = args.Length > 1 ? args[1] : Path.GetFullPath("NPCHerbo.esp");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsm, SkyrimRelease.SkyrimSE);
    // formids.json : à la racine du projet (dossier contenant PLAN.md)
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root != null && !File.Exists(Path.Combine(root.FullName, "PLAN.md"))) root = root.Parent;
    if (root == null) throw new InvalidOperationException("Racine du projet introuvable (PLAN.md).");
    var registry = new FormIdRegistry(Path.Combine(root.FullName, "formids.json"));

    var builder = new PluginBuilder(esm, registry);
    var mod = builder.Build();
    mod.WriteToBinary(outPath);

    // Menu MCM (MCM Helper) : config JSON + traductions SkyUI (UTF-16 LE avec BOM, format attendu par le jeu)
    var outDir = Path.GetDirectoryName(outPath)!;
    if (builder.McmConfigJson != null)
    {
        var cfgDir = Path.Combine(outDir, "MCM", "Config", "NPCHerbo");
        Directory.CreateDirectory(cfgDir);
        File.WriteAllText(Path.Combine(cfgDir, "config.json"), builder.McmConfigJson, new System.Text.UTF8Encoding(false));
        var trDir = Path.Combine(outDir, "Interface", "Translations");
        Directory.CreateDirectory(trDir);
        foreach (var lang in new[] { "FRENCH", "ENGLISH" })
            File.WriteAllText(Path.Combine(trDir, $"NPCHerbo_{lang}.txt"), builder.McmTranslations!, new System.Text.UnicodeEncoding(false, true));
        Console.WriteLine("MCM écrit : config.json + traductions");
    }
    registry.Save();

    // SEQ : obligatoire pour qu'une quête « Start Game Enabled » puisse faire parler ses PNJ.
    // Contenu = FormIDs (tels qu'écrits dans le plugin : index du master en octet de poids fort) des quêtes concernées.
    var seqDir = Path.Combine(Path.GetDirectoryName(outPath)!, "SEQ");
    Directory.CreateDirectory(seqDir);
    uint masterCount;
    using (var written = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE))
        masterCount = (uint)written.ModHeader.MasterReferences.Count; // les masters ne sont calculés qu'à l'écriture
    var questIds = mod.Quests.Where(q => q.Flags.HasFlag(Quest.Flag.StartGameEnabled))
        .Select(q => (masterCount << 24) | q.FormKey.ID).ToList();
    using (var bw = new BinaryWriter(File.Create(Path.Combine(seqDir, "NPCHerbo.seq"))))
        foreach (var id in questIds) bw.Write(id);
    Console.WriteLine($"SEQ écrit : {questIds.Count} quête(s) ({string.Join(",", questIds.Select(i => i.ToString("X8")))})");
    foreach (var e in registry.Added) Console.WriteLine($"Nouveau FormID : {e} = {registry.Get(e):X6}");
    Console.WriteLine($"Plugin écrit : {outPath} ({mod.EnumerateMajorRecords().Count()} enregistrements)");
    return 0;
}

Console.WriteLine("Usage : dotnet run -- build <sortie.esp> | dump <plugin.esp> | explore");
return 1;
