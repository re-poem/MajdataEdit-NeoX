using MajdataEdit_NeoX.Base;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MajdataEdit_NeoX.Models.Fumen;

/// <summary>
/// Fumen snippet completions keyed by trigger character.
///
/// Defaults live in the embedded <c>completions.json</c> (copied into the writable data dir by
/// <see cref="MajEnv.InitializeUserFiles"/>); the on-disk format — each row under a trigger is a 3-tuple
///   [ text, content, description ]
/// — has content / description optional (<c>null</c>), text required.
/// </summary>
public static class Completions
{
    private static readonly Dictionary<string, FumenCompletionItem[]> Empty = [];
    private static readonly Dictionary<string, FumenCompletionItem[]> _completions;

    static Completions()
    {
        _completions = TryLoad(MajEnv.CompletionFile) ?? Empty;
    }

    private static Dictionary<string, FumenCompletionItem[]>? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var text = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? null : Parse(text);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, FumenCompletionItem[]> Parse(string json)
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

                var text = node[0].GetString();
                if (text is null) continue;

                var content = node.GetArrayLength() > 1 && node[1].ValueKind != JsonValueKind.Null
                    ? node[1].GetString()
                    : null;

                var description = node.GetArrayLength() > 2 && node[2].ValueKind != JsonValueKind.Null
                    ? node[2].GetString()
                    : null;

                entries.Add(new FumenCompletionItem(text, content, description));
            }

            if (entries.Count > 0)
                dict[prop.Name] = entries.ToArray();
        }

        return dict;
    }

    public static FumenCompletionItem[] Get(string trigger) =>
        _completions.TryGetValue(trigger, out var items) ? items : [];
}