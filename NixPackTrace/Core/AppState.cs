using System.IO;
using Newtonsoft.Json;
using NixPackTrace.Models;

namespace NixPackTrace.Core
{
    public static class AppState
    {
        public static AppSettings Settings    { get; set; } = new AppSettings();
        public static string      CurrentUser { get; set; } = "Unknown";

        private static string SettingsFilePath
        {
            get
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(appData, "NixPackTrace");
                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }
                return Path.Combine(appFolder, "appsettings.json");
            }
        }

        public static void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json   = File.ReadAllText(SettingsFilePath);
                var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                if (loaded != null) Settings = loaded;
            }
        }

        public static void SaveSettings()
        {
            File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }
    }
}
