using System.Globalization;
using System.Text.Json;

namespace NPCHerbo.Generator;

/// <summary>
/// FormIDs stables par EditorID (formids.json, versionné). Indispensable : un FormID qui change
/// casse les sauvegardes existantes. Un nouvel EditorID reçoit le plus grand ID connu + 1.
/// </summary>
public sealed class FormIdRegistry
{
    readonly string _path;
    readonly Dictionary<string, uint> _ids;
    public List<string> Added { get; } = new();

    public FormIdRegistry(string path)
    {
        _path = path;
        _ids = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!
                .ToDictionary(kv => kv.Key, kv => uint.Parse(kv.Value, NumberStyles.HexNumber))
            : new Dictionary<string, uint>();
    }

    public uint Get(string editorId)
    {
        if (_ids.TryGetValue(editorId, out var id)) return id;
        id = _ids.Count == 0 ? 0x800u : _ids.Values.Max() + 1;
        if (id > 0xFFF) throw new InvalidOperationException("Plage ESL epuisee (0x800-0xFFF).");
        _ids[editorId] = id;
        Added.Add(editorId);
        return id;
    }

    public uint Max => _ids.Count == 0 ? 0x7FFu : _ids.Values.Max();

    public void Save()
    {
        var ordered = _ids.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value.ToString("X6"));
        File.WriteAllText(_path, JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
