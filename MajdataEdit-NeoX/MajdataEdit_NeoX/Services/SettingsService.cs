using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Models.Setting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MajdataEdit_NeoX.Services;

public class SettingsService
{
    private MajSetting? _currentSettings;
    private readonly Lock _lock = new();


    public MajSetting Current => _currentSettings!;
    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        Load();
    }
    public void NotifyChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Set(Action<MajSetting> setter)
    {
        lock (_lock)
        {
            setter(Current);
        }

        NotifyChanged();
    }

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(MajEnv.SettingsFile))
                {
                    var json = File.ReadAllText(MajEnv.SettingsFile);
                    var root = JObject.Parse(json);
                    var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
                    {
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    });
                    _currentSettings = root.ToObject<MajSetting>(serializer);
                    _currentSettings ??= new MajSetting();
                }
                else
                {
                    _currentSettings = new MajSetting();
                    Save(); // Save defaults
                }
            }
            catch (Exception ex)
            {
                // If load fails, use defaults
                Console.WriteLine($"Failed to load settings: {ex.Message}");
                _currentSettings = new MajSetting();
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                if (_currentSettings == null)
                {
                    return;
                }

                var directory = Path.GetDirectoryName(MajEnv.SettingsFile);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
                File.WriteAllText(MajEnv.SettingsFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
                throw;
            }
        }
    }
}
