using NLua;
using System;
using System.Collections.Generic;
using System.IO;

namespace MajdataEdit_NeoX.Models.Plugin;

/// <summary>Loads ordered 1-based action entries from a Lua file.</summary>
public sealed class LuaPlugin : IDisposable
{
    private readonly Lua _lua;
    private readonly List<PluginAction> _actions = new();

    public string Name { get; }
    public IReadOnlyList<PluginAction> Actions => _actions;

    public LuaPlugin(string file)
    {
        Name = Path.GetFileNameWithoutExtension(file);
        _lua = new Lua();
        try
        {
            var values = _lua.DoFile(file);
            if (values.Length == 0 || values[0] is not LuaTable result)
                throw new InvalidDataException($"Plugin {Name} must return an array of actions.");

            _lua["__plugin_table__"] = result;
            try
            {
                var length = Convert.ToInt32(_lua.DoString("return #__plugin_table__")[0]);
                for (var i = 1; i <= length; i++)
                {
                    if (result[i] is not LuaTable entry || entry["Process"] is not LuaFunction process)
                        continue;

                    var name = entry["Name"]?.ToString() ?? process.ToString() ?? $"action_{i}";
                    var nameKey = entry["NameKey"]?.ToString();
                    var iconKey = entry["Icon"]?.ToString();
                    _actions.Add(new PluginAction(name,
                        input => process.Call(input)[0] as string
                            ?? throw new InvalidDataException($"Plugin action {name} must return a string."),
                        nameKey, iconKey));
                }
            }
            finally
            {
                _lua["__plugin_table__"] = null;
            }
        }
        catch
        {
            _lua.Dispose();
            throw;
        }
    }

    public void Dispose() => _lua.Dispose();
}
