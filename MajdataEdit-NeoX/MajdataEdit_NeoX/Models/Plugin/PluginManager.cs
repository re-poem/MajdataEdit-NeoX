using MajdataEdit_NeoX.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MajdataEdit_NeoX.Models.Plugin;

/// <summary>Process-wide registry of user Lua plugins.</summary>
public sealed class PluginManager : IDisposable
{
    private readonly List<LuaPlugin> _plugins = new();
    public IReadOnlyList<LuaPlugin> Plugins => _plugins;
    public Action? OnPluginsChanged;

    public PluginManager() => Reregister();

    public void Reregister()
    {
        var loaded = new List<LuaPlugin>();
        try
        {
            Directory.CreateDirectory(MajEnv.LuaPluginsPath);
            foreach (var file in Directory.EnumerateFiles(MajEnv.LuaPluginsPath, "*.lua")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try { loaded.Add(new LuaPlugin(file)); }
                catch (Exception ex) { Debug.WriteLine($"Loading Lua plugin {file} failed: {ex}"); }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scanning Lua plugins failed: {ex}");
        }

        foreach (var plugin in _plugins) plugin.Dispose();
        _plugins.Clear();
        _plugins.AddRange(loaded);
        OnPluginsChanged?.Invoke();
    }

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        _plugins.Clear();
    }
}
