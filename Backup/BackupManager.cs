using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Backup
{
    public class BackupManager
    {
        private readonly string backupPath;
        private readonly string mapName;

        public BackupManager(string backupPath, string mapName)
        {
            this.backupPath = backupPath;
            this.mapName = mapName;
        }

        public List<(string DisplayText, string Timestamp)> GetAvailableBackups()
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
                return new List<(string, string)>();

            var currentTime = DateTime.Now;

            return Directory.GetFiles(backupPath, $"{mapName}_*.ark")
                            .Select(file =>
                            {
                                string fileName = Path.GetFileNameWithoutExtension(file);
                                string timestamp = fileName.Replace($"{mapName}_", "");

                                if (DateTime.TryParseExact(timestamp, "yyyy.MM.dd_HH.mm", null, System.Globalization.DateTimeStyles.None, out DateTime backupTime))
                                {
                                    TimeSpan difference = currentTime - backupTime;

                                    string displayText = FormatTimeDifference(difference);

                                    return (DisplayText: displayText, Timestamp: timestamp);
                                }

                                return (DisplayText: "Backup (unknown time)", Timestamp: timestamp);
                            })
                            .OrderByDescending(entry => entry.Timestamp)
                            .ToList();
        }

        private string FormatTimeDifference(TimeSpan difference)
        {
            if (difference.TotalMinutes < 1)
                return "Backup just now";

            var days = (int)difference.TotalDays;
            var hours = difference.Hours;
            var minutes = difference.Minutes;

            var parts = new List<string>();
            if (days > 0)
                parts.Add($"{days} day{(days > 1 ? "s" : "")}");
            if (hours > 0)
                parts.Add($"{hours} hour{(hours > 1 ? "s" : "")}");
            if (minutes > 0)
                parts.Add($"{minutes} minute{(minutes > 1 ? "s" : "")}");

            return "Backup " + string.Join(" ", parts) + " ago";
        }

        public void LoadBackup(string timestamp, string savePath)
        {
            if (string.IsNullOrEmpty(timestamp))
                throw new ArgumentException("Invalid timestamp");

            string backupFileName = $"{mapName}_{timestamp}.ark";
            string backupFilePath = Path.Combine(backupPath, backupFileName);

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found", backupFilePath);

            string destinationFilePath = Path.Combine(savePath, $"{mapName}.ark");

            // Replace the .ark file in the source folder
            File.Copy(backupFilePath, destinationFilePath, true);
        }
    }
}