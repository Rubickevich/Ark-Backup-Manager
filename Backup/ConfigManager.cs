using System;
using System.IO;
using System.Text.Json;

namespace Backup
{
    public class Config
    {
        public string SavePath { get; set; }
        public string BackupPath { get; set; }
        public string MapName { get; set; }
        public bool AutoStart { get; set; }
        public string GitHubToken { get; set; }
    }

    public class ConfigManager
    {
        private const string ConfigFileName = "config.json";

        public Config LoadConfig()
        {
            if (!File.Exists(ConfigFileName))
                return new Config();

            try
            {
                string json = File.ReadAllText(ConfigFileName);
                return JsonSerializer.Deserialize<Config>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                return new Config();
            }
        }

        public void SaveConfig(Config config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
