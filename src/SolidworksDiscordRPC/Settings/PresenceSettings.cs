using System;
using System.IO;

namespace SolidworksDiscordRPC
{
    /// <summary>
    /// User preferences persisted to %AppData%\SolidworksDiscordRPC\settings.json.
    /// Thread-safe for reads/writes from UI thread vs. tracker timer.
    /// Public so TaskPane (public UserControl) events can expose it.
    /// </summary>
    public sealed class PresenceSettings
    {
        public bool PresenceEnabled { get; set; } = true;
        public bool HideFileName { get; set; } = false;
        public bool ShowFeatureCount { get; set; } = true;
        public bool ShowMaterial { get; set; } = true;
        public string CustomProjectName { get; set; } = "";

        private static readonly object SyncRoot = new object();

        private static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SolidworksDiscordRPC",
            "settings.json");

        public static PresenceSettings Load()
        {
            try
            {
                var path = SettingsFilePath;
                if (!File.Exists(path)) return new PresenceSettings();

                var json = File.ReadAllText(path);
                var settings = new PresenceSettings();
                TryParseBool(json, "PresenceEnabled", v => settings.PresenceEnabled = v);
                TryParseBool(json, "HideFileName", v => settings.HideFileName = v);
                TryParseBool(json, "ShowFeatureCount", v => settings.ShowFeatureCount = v);
                TryParseBool(json, "ShowMaterial", v => settings.ShowMaterial = v);
                TryParseString(json, "CustomProjectName", v => settings.CustomProjectName = v);
                return settings;
            }
            catch { return new PresenceSettings(); }
        }

        public void Save()
        {
            try
            {
                lock (SyncRoot)
                {
                    var dir = Path.GetDirectoryName(SettingsFilePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    var json =
                        "{\r\n" +
                        $"  \"PresenceEnabled\": {PresenceEnabled.ToString().ToLowerInvariant()},\r\n" +
                        $"  \"HideFileName\": {HideFileName.ToString().ToLowerInvariant()},\r\n" +
                        $"  \"ShowFeatureCount\": {ShowFeatureCount.ToString().ToLowerInvariant()},\r\n" +
                        $"  \"ShowMaterial\": {ShowMaterial.ToString().ToLowerInvariant()},\r\n" +
                        $"  \"CustomProjectName\": \"{EscapeJson(CustomProjectName ?? "")}\"\r\n" +
                        "}\r\n";

                    File.WriteAllText(SettingsFilePath, json);
                }
            }
            catch { /* never affect SolidWorks */ }
        }

        public static void OpenSettingsFolder()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SolidworksDiscordRPC");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch { /* ignore */ }
        }

        private static void TryParseBool(string json, string key, Action<bool> setter)
        {
            var search = "\"" + key + "\"";
            var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;

            var colon = json.IndexOf(':', idx + search.Length);
            if (colon < 0) return;

            var rest = json.Substring(colon + 1).TrimStart();
            if (rest.StartsWith("true", StringComparison.OrdinalIgnoreCase)) setter(true);
            else if (rest.StartsWith("false", StringComparison.OrdinalIgnoreCase)) setter(false);
        }

        private static void TryParseString(string json, string key, Action<string> setter)
        {
            var search = "\"" + key + "\"";
            var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;

            var colon = json.IndexOf(':', idx + search.Length);
            if (colon < 0) return;

            var rest = json.Substring(colon + 1).TrimStart();
            if (rest.Length < 2 || rest[0] != '"') return;

            var endQuote = rest.IndexOf('"', 1);
            if (endQuote < 0) return;

            setter(rest.Substring(1, endQuote - 1));
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
