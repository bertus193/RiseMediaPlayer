using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Rise.Common.Helpers
{
    public static class SettingsHelpers
    {
        private static readonly ISettingsStore _store;

        static SettingsHelpers()
        {
            try
            {
                // Intentar usar ApplicationData (packaged)
                var _ = ApplicationData.Current.LocalSettings;
                _store = new PackagedSettingsStore();
            }
            catch (InvalidOperationException)
            {
                // Fallback para unpackaged: usar JSON en %LOCALAPPDATA%
                _store = new UnpackagedSettingsStore();
            }
        }

        public static T GetLocal<T>(T defaultValue, string container, string setting)
        {
            return _store.Get<T>(defaultValue, container, setting);
        }

        public static void SetLocal<T>(T newValue, string container, string setting)
        {
            _store.Set<T>(newValue, container, setting);
        }
    }

    // Interfaz común
    public interface ISettingsStore
    {
        T Get<T>(T defaultValue, string container, string setting);
        void Set<T>(T newValue, string container, string setting);
    }

    // Implementación para apps empaquetadas (MSIX)
    public class PackagedSettingsStore : ISettingsStore
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        public T Get<T>(T defaultValue, string container, string setting)
        {
            var values = _localSettings.CreateContainer(container, ApplicationDataCreateDisposition.Always).Values;
            values[setting] ??= defaultValue;
            return (T)values[setting];
        }

        public void Set<T>(T newValue, string container, string setting)
        {
            var values = _localSettings.CreateContainer(container, ApplicationDataCreateDisposition.Always).Values;
            values[setting] = newValue;
        }
    }

    // Implementación para apps unpackaged (JSON en %LOCALAPPDATA%\Rise Media Player\Settings)
    public class UnpackagedSettingsStore : ISettingsStore
    {
        private readonly string _settingsPath;

        public UnpackagedSettingsStore()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _settingsPath = Path.Combine(localAppData, "Rise Media Player", "Settings");
            Directory.CreateDirectory(_settingsPath);
        }

        private string GetFilePath(string container) => Path.Combine(_settingsPath, $"{container}.json");

        public T Get<T>(T defaultValue, string container, string setting)
        {
            var path = GetFilePath(container);
            if (!File.Exists(path)) return defaultValue;

            try
            {
                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict != null && dict.TryGetValue(setting, out var element))
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText()) ?? defaultValue;
                }
            }
            catch { }
            return defaultValue;
        }

        public void Set<T>(T newValue, string container, string setting)
        {
            var path = GetFilePath(container);
            Dictionary<string, object> dict = new();

            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                }
                catch { }
            }

            dict[setting] = newValue;
            var output = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, output);
        }
    }
}