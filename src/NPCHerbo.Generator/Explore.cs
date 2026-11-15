using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace NPCHerbo.Generator;

/// <summary>Outil de diagnostic : liste des enregistrements vanilla utiles (dotnet run -- explore).</summary>
public static class Explore
{
    /// <summary>Affichage réflexif récursif (diagnostic).</summary>
    public static void DumpObj(object? o, string indent, int depth)
    {
        if (o == null) { Console.WriteLine(indent + "null"); return; }
        var t = o.GetType();
        if (depth > 6 || t.IsPrimitive || t.IsEnum || o is string || t.Name.StartsWith("FormLink") || t.Name == "FormKey"
            || t.Name.StartsWith("TranslatedString") || t.Name == "P3Float")
        {
            Console.WriteLine(indent + o);
            return;
        }
        if (o is System.Collections.IDictionary dict)
        {
            foreach (System.Collections.DictionaryEntry e in dict)
            { Console.WriteLine($"{indent}[{e.Key}] ({e.Value?.GetType().Name})"); DumpObj(e.Value, indent + "    ", depth + 1); }
            return;
        }
        if (o is System.Collections.IEnumerable en && o is not string)
        {
            int i = 0;
            foreach (var item in en)
            { Console.WriteLine($"{indent}#{i++} ({item?.GetType().Name})"); DumpObj(item, indent + "    ", depth + 1); }
            return;
        }
        foreach (var prop in t.GetProperties())
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.Name is "StaticRegistration" or "Registration" or "Type" or "ContainedFormLinks" or "EnumerateFormLinks"
                or "MajorRecordFlagsRaw" or "FormVersion" or "Version2" or "VersionControl" or "IsCompressed" or "IsDeleted"
                or "RecordStats" or "IsReadOnly") continue;
            object? v;
            try { v = prop.GetValue(o); } catch { continue; }
            var vt = v?.GetType();
            if (v == null || vt!.IsPrimitive || vt.IsEnum || v is string || vt.Name.StartsWith("FormLink") || vt.Name == "FormKey"
                || vt.Name.StartsWith("TranslatedString") || vt.Name == "P3Float")
                Console.WriteLine($"{indent}{prop.Name} = {v}");
            else
            { Console.WriteLine($"{indent}{prop.Name}: ({vt.Name})"); DumpObj(v, indent + "    ", depth + 1); }
        }
    }

    /// <summary>Cherche des répliques vanilla (texte FR ou EN) contenant des mots-clés, avec leur sujet et conditions de voix.</summary>
    public static void RunLines(string skyrimEsmPath, string[] keywords)
    {
        foreach (var lang in new[] { Mutagen.Bethesda.Strings.Language.French, Mutagen.Bethesda.Strings.Language.English })
        {
            try
            {
                using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsmPath, SkyrimRelease.SkyrimSE,
                    new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters
                    { StringsParam = new Mutagen.Bethesda.Strings.StringsReadParameters { TargetLanguage = lang } });
                Console.WriteLine($"=== langue {lang}");
                int n = 0;
                foreach (var topic in esm.DialogTopics)
                    foreach (var info in topic.Responses)
                        foreach (var r in info.Responses)
                        {
                            var text = r.Text?.String ?? "";
                            if (!keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
                            var voiceConds = string.Join(" ", info.Conditions.Select(c => c.Data.GetType().Name.Replace("ConditionData", "") + ":" +
                                string.Join("/", c.Data.EnumerateFormLinks().Select(l => l.FormKey.ID.ToString("X6")))));
                            Console.WriteLine($"{topic.EditorID} [{topic.Subtype}] INFO {info.FormKey.ID:X6} \"{text}\"  conds={voiceConds}");
                            if (++n > 300) return;
                        }
                return;
            }
            catch (Exception e) { Console.WriteLine($"langue {lang} : {e.Message}"); }
        }
    }

    /// <summary>Répliques jouables par une voix donnée (GetIsVoiceType direct ou via FormList), hors scènes de quêtes.</summary>
    public static void RunVoiceLines(string skyrimEsmPath, uint voiceId, string[] subtypeFilter)
    {
        using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsmPath, SkyrimRelease.SkyrimSE,
            new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters
            { StringsParam = new Mutagen.Bethesda.Strings.StringsReadParameters { TargetLanguage = Mutagen.Bethesda.Strings.Language.French } });
        var lists = esm.FormLists.Where(fl => fl.Items.Any(i => i.FormKey.ID == voiceId)).Select(fl => fl.FormKey.ID).ToHashSet();
        Console.WriteLine($"Listes de voix contenant {voiceId:X6} : {lists.Count}");
        foreach (var topic in esm.DialogTopics)
        {
            if (subtypeFilter.Length > 0 && !subtypeFilter.Contains(topic.Subtype.ToString())) continue;
            foreach (var info in topic.Responses)
            {
                var vt = info.Conditions.Select(c => c.Data).OfType<IGetIsVoiceTypeConditionDataGetter>().ToList();
                if (vt.Count == 0) continue;
                bool ok = vt.Any(v => v.VoiceTypeOrList.Link.FormKey.ID == voiceId || lists.Contains(v.VoiceTypeOrList.Link.FormKey.ID));
                if (!ok) continue;
                foreach (var r in info.Responses)
                    Console.WriteLine($"{topic.Subtype,-18} {topic.EditorID,-40} INFO {info.FormKey.ID:X6} \"{r.Text?.String}\"");
            }
        }
    }

    /// <summary>Répliques de Skyrim.esm ayant un fichier audio pour la voix donnée dans le BSA de voix (texte FR).</summary>
    public static void RunVoiceFiles(string skyrimEsmPath, string voiceBsa, string voiceFolder, string outFile)
    {
        var reader = Mutagen.Bethesda.Archives.Archive.CreateReader(GameRelease.SkyrimSE, voiceBsa);
        var prefix = $@"sound\voice\skyrim.esm\{voiceFolder}\";
        var infoIds = new HashSet<uint>();
        foreach (var f in reader.Files)
        {
            var p = f.Path.ToLowerInvariant();
            if (!p.StartsWith(prefix)) continue;
            var name = Path.GetFileNameWithoutExtension(p);           // ..._<infoid>_<n>
            var parts = name.Split('_');
            if (parts.Length >= 2 && uint.TryParse(parts[^2], System.Globalization.NumberStyles.HexNumber, null, out var id))
                infoIds.Add(id & 0xFFFFFF);
        }
        Console.WriteLine($"Fichiers audio {voiceFolder} : {infoIds.Count} répliques");

        using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsmPath, SkyrimRelease.SkyrimSE,
            new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters
            { StringsParam = new Mutagen.Bethesda.Strings.StringsReadParameters { TargetLanguage = Mutagen.Bethesda.Strings.Language.French } });
        using var w = new StreamWriter(outFile, false, new System.Text.UTF8Encoding(false));
        foreach (var topic in esm.DialogTopics)
            foreach (var info in topic.Responses)
            {
                if (!infoIds.Contains(info.FormKey.ID)) continue;
                var conds = string.Join(" ", info.Conditions.Select(c => c.Data.GetType().Name.Replace("ConditionData", "").Replace("BinaryOverlay", "")));
                foreach (var r in info.Responses)
                    w.WriteLine($"{topic.Subtype}\t{topic.EditorID}\t{info.FormKey.ID:X6}\t{r.Text?.String}\t{conds}");
            }
    }

    public static void RunTravel(string skyrimEsmPath)
    {
        using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsmPath, SkyrimRelease.SkyrimSE);
        var travelTpl = esm.Packages.First(p => p.EditorID == "Travel");
        Console.WriteLine($"Travel template = {travelTpl.FormKey}");
        DumpObj(travelTpl.Data, "  ", 0);
        var stats = new Dictionary<string, int>();
        IPackageGetter? example = null;
        foreach (var p in esm.Packages.Where(p => p.PackageTemplate.FormKey == travelTpl.FormKey))
        {
            foreach (var d in p.Data.Values.OfType<IPackageDataLocationGetter>())
            {
                var tn = d.Location.Target.GetType().Name;
                stats[tn] = stats.GetValueOrDefault(tn) + 1;
                if (tn.Contains("Alias") && example == null) example = p;
            }
        }
        foreach (var kv in stats) Console.WriteLine($"  {kv.Key}: {kv.Value}");
        var lt = typeof(LocationTargetRadius).Assembly.GetTypes().First(t => t.IsEnum && t.Name == "LocationType");
        Console.WriteLine($"  LocationType ({lt.FullName}): " + string.Join(", ", Enum.GetNames(lt)));
        var fb = esm.Packages.Where(p => p.PackageTemplate.FormKey == travelTpl.FormKey)
            .SelectMany(p => p.Data.Values.OfType<IPackageDataLocationGetter>().Select(d => (p, d)))
            .Where(x => x.d.Location.Target is ILocationFallbackGetter)
            .GroupBy(x => ((ILocationFallbackGetter)x.d.Location.Target).Type);
        foreach (var g in fb) Console.WriteLine($"  fallback {g.Key}: {g.Count()} ex={g.First().p.EditorID} radius={g.First().d.Location.Radius} data={((ILocationFallbackGetter)g.First().d.Location.Target).Data}");
        if (example != null)
        {
            Console.WriteLine($"=== EXEMPLE {example.EditorID} {example.FormKey}");
            DumpObj(example.Data, "  ", 0);
            Console.WriteLine($"  Flags={example.Flags} Speed={example.PreferredSpeed} Interrupt={example.InterruptFlags} Unknown={example.Unknown}");
        }
        var condTypes = typeof(Condition).Assembly.GetTypes().Where(t => t.Name.Contains("GetGlobalValue") || t.Name == "ConditionFloat").Select(t => t.FullName);
        Console.WriteLine("Conditions: " + string.Join(", ", condTypes));
        var asm = typeof(Condition).Assembly;
        foreach (var t in asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.BaseType?.Name == "ALocationTargetBase"
                     || t.Name is "LocationTarget" or "LocationFallback" or "LocationTargetRadius"))
            Console.WriteLine($"  {t.Name} : {t.BaseType?.Name} props=" + string.Join(", ", t.GetProperties().Where(p => p.DeclaringType == t).Select(p => $"{p.Name}:{p.PropertyType.Name}")));
        foreach (var e in asm.GetTypes().Where(t => t.IsEnum && (t.FullName!.Contains("LocationFallback") || t.Name.Contains("PackageTarget"))))
            Console.WriteLine($"  enum {e.FullName}: {string.Join(", ", Enum.GetNames(e))}");
        foreach (var t in asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Name.StartsWith("PackageTarget") && !t.Name.Contains("Binary") && !t.Name.Contains("Common") && !t.Name.Contains("Translation") && !t.Name.Contains("Registration") && !t.Name.Contains("MixIn")))
            Console.WriteLine($"  {t.Name} props=" + string.Join(", ", t.GetProperties().Where(p => p.DeclaringType == t).Select(p => $"{p.Name}:{p.PropertyType.Name}")));
        var gg = asm.GetTypes().First(t => t.Name == "GetGlobalValueConditionData");
        Console.WriteLine("  props: " + string.Join(", ", gg.GetProperties().Select(p => $"{p.Name}:{p.PropertyType.Name}")));
    }

    public static void Run(string skyrimEsmPath)
    {
        using var esm = SkyrimMod.CreateFromBinaryOverlay(skyrimEsmPath, SkyrimRelease.SkyrimSE);

        void Dump<T>(string title, IEnumerable<T> recs, Func<T, bool> pred, Func<T, string>? extra = null)
            where T : ISkyrimMajorRecordGetter
        {
            Console.WriteLine($"--- {title}");
            foreach (var r in recs.Where(pred).Take(40))
                Console.WriteLine($"  {r.FormKey} {r.EditorID} {extra?.Invoke(r)}");
        }

        bool Ed(ISkyrimMajorRecordGetter r, params string[] parts) =>
            r.EditorID != null && parts.Any(p => r.EditorID.Contains(p, StringComparison.OrdinalIgnoreCase));

        Dump("Races", esm.Races, r => Ed(r, "WoodElf", "Bosmer"));
        Dump("Voices", esm.VoiceTypes, r => Ed(r, "Eager"));
        Dump("Factions", esm.Factions, r => Ed(r, "PlayerFaction", "CurrentFollower"));
        Dump("Classes", esm.Classes, r => Ed(r, "Citizen", "Thief", "Pathfinder"));
        Dump("Outfits", esm.Outfits, r => Ed(r, "Farm", "Hunter", "Ranger", "Alchem", "Forsworn"));
        Dump("Containers", esm.Containers, r => Ed(r, "Satchel", "Sack", "Knapsack", "NoRespawn", "Barrel"),
            r => $"items={r.Items?.Count ?? 0} flags={r.Flags} model={r.Model?.File}");
        Dump("Idles", esm.IdleAnimations, r => Ed(r, "Pick", "Harvest", "Search", "Kneel", "Crouch", "Ritual"),
            r => $"event={r.AnimationEvent}");
        Dump("Packages (Follow)", esm.Packages, r => Ed(r, "Follow"),
            r => $"template={r.PackageTemplate.FormKey} procs={r.ProcedureTree.Count} inputs={r.Data.Count}");
        foreach (var id in new uint[] { 0x10ACD1, 0x019B2C })
        {
            var p = esm.Packages.First(x => x.FormKey.ID == id);
            Console.WriteLine($"=== PACKAGE {p.EditorID}");
            DumpObj(p, "  ", 0);
        }
        Dump("Female Bosmer NPCs", esm.Npcs,
            n => n.Race.FormKey.ID == 0x013749 && n.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female)
                 && n.HeadParts.Count > 0 && n.Template.IsNull,
            n => $"name={n.Name?.String} heads={n.HeadParts.Count} tints={n.TintLayers.Count}");
    }
}
