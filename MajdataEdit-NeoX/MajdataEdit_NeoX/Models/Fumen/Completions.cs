using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Types;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MajdataEdit_NeoX.Models.Fumen;

/// <summary>
/// Fumen snippet completions keyed by trigger character.
///
/// On-disk format (completions.json) — each row under a trigger is a 3-tuple:
///   [ text, content, description ]
/// Any of content / description can be <c>null</c>; text is required.
/// </summary>
public static class Completions
{
    public static readonly IReadOnlyDictionary<string, FumenCompletionItem[]> Default =
        new Dictionary<string, FumenCompletionItem[]>
        {
            ["["] =
            [
                new FumenCompletionItem(("4:1]", null, null)),
                new FumenCompletionItem(("8:1]", null, null)),
                new FumenCompletionItem(("1:0]", null, null)),
                new FumenCompletionItem(("384:1]", null, null)),
            ],
            ["{"] =
            [
                new FumenCompletionItem(("4}", null, null)),
                new FumenCompletionItem(("3}", null, null)),
                new FumenCompletionItem(("8}", null, null)),
                new FumenCompletionItem(("16}", null, null)),
                new FumenCompletionItem(("12}", null, null)),
                new FumenCompletionItem(("384}", null, null)),
            ],
        };

    public static FumenCompletionItem[] Get(string trigger)
    {
        var path = MajEnv.CompletionFile;
        IReadOnlyDictionary<string, FumenCompletionItem[]>? source = null;

        try
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                    source = Parse(text);
            }
        }
        catch { }

        if (source is null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(Default, JsonOptions));
            }
            catch { }
            source = Default;
        }

        return source.TryGetValue(trigger, out var items) ? items : [];
    }

    static IReadOnlyDictionary<string, FumenCompletionItem[]> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, FumenCompletionItem[]>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            var entries = new List<FumenCompletionItem>();
            foreach (var node in prop.Value.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() < 1) continue;
                var t = node[0].GetString();
                if (t is null) continue;
                object? c = node.GetArrayLength() > 1 && node[1].ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<object>(node[1].GetRawText())
                    : null;
                object? d = node.GetArrayLength() > 2 && node[2].ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<object>(node[2].GetRawText())
                    : null;
                entries.Add(new FumenCompletionItem((t, c, d)));
            }
            if (entries.Count > 0)
                dict[prop.Name] = entries.ToArray();
        }
        return dict;
    }

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}