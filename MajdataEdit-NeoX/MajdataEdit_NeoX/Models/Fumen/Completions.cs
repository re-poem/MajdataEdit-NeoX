using MajdataEdit_NeoX.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

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
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly IReadOnlyDictionary<string, FumenCompletionItem[]> _completions;
    public static readonly IReadOnlyDictionary<string, FumenCompletionItem[]> Default =
        new Dictionary<string, FumenCompletionItem[]>
        {
            ["["] =
            [
                new("4:1]"),
                new("8:1]"),
                new("1:0]"),
                new("384:1]"),
            ],
            ["{"] =
            [
                new("4}"),
                new("3}"),
                new("8}"),
                new("16}"),
                new("12}"),
                new("384}"),
            ],
        };

    static Completions()
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
                File.WriteAllText(path, Serialize(Default));
            }
            catch { }
            source = Default;
        }

        _completions = source;
    }

    private static IReadOnlyDictionary<string, FumenCompletionItem[]> Parse(string json)
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

    private static string Serialize(IReadOnlyDictionary<string, FumenCompletionItem[]> completions)
    {
        var root = new JsonObject();

        foreach (var (trigger, items) in completions)
        {
            var array = new JsonArray();
            foreach (var item in items)
            {
                var tuple = new JsonArray
                {
                    item.Text,
                    item.Content is string s && s == item.Text ? null : JsonValue.Create((string?)item.Content),
                    JsonValue.Create((string?)item.Description)
                };
                array.Add(tuple);
            }
            root[trigger] = array;
        }

        return root.ToJsonString(JsonOptions);
    }

    public static FumenCompletionItem[] Get(string trigger) =>
        _completions.TryGetValue(trigger, out var items) ? items : Array.Empty<FumenCompletionItem>();
}