using System;

namespace MajdataEdit_NeoX.Models.Plugin;

public sealed record PluginAction(string Name, Func<string, string> Transform, string? NameKey, string? IconKey);
