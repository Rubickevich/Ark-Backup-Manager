using System;
using System.IO;
using System.Threading.Tasks;

namespace Backup
{
    public class FileWatcher
    {
        private FileSystemWatcher watcher;
        private DateTime lastEventTime = DateTime.MinValue;
        private readonly int debounceInterval = 1000; 

        private string backupPath;
        private string mapName;

        public bool IsRunning { get; private set; }

        public event EventHandler<string> OnFileChanged;

        public FileWatcher()
        {
            watcher = new FileSystemWatcher();
            watcher.Changed += Watcher_Changed;
        }

        public void StartWatching(string path, string mapName, string backupPath)
        {
            if (IsRunning) return;

            this.mapName = mapName;
            this.backupPath = backupPath;

            watcher.Path = path;
            watcher.Filter = $"{mapName}.ark";
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            watcher.EnableRaisingEvents = true;

            IsRunning = true;
        }

        public void StopWatching()
        {
            if (!IsRunning) return;

            watcher.EnableRaisingEvents = false;
            IsRunning = false;
        }

        public void PauseWatching()
        {
            watcher.EnableRaisingEvents = false;
        }

        public void ResumeWatching()
        {
            watcher.EnableRaisingEvents = true;
        }

        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Debounce logic: Ignore events occurring within the debounce interval
            // Important because windows IO operations aren't done in a single instance and can be misinterpreted as multiple file changes. 
            DateTime now = DateTime.Now;
            if ((now - lastEventTime).TotalMilliseconds < debounceInterval)
            {
                return;
            }
            lastEventTime = now; 
            await Task.Delay(500); //Just to be extra sure

            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HH.mm");
            string backupFileName = $"{mapName}_{timestamp}.ark";
            string backupFilePath = Path.Combine(backupPath, backupFileName);

            try
            {
                File.Copy(e.FullPath, backupFilePath, true);
                OnFileChanged?.Invoke(this, backupFilePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error copying file: {ex.Message}");
            }
        }
    }
}