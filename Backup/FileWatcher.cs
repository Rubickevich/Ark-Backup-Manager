using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Backup
{
    public class FileWatcher
    {
        private string filePath;
        private string backupPath;
        private string mapName;
        private DateTime? lastModifiedTime;
        private CancellationTokenSource cancellationTokenSource;
        private readonly int pollingInterval = 30000; // Poll every 30 seconds

        public bool IsRunning { get; private set; }

        public event EventHandler<string> OnFileChanged;

        public void StartWatching(string path, string mapName, string backupPath)
        {
            if (IsRunning) return;

            this.filePath = Path.Combine(path, $"{mapName}.ark");
            this.mapName = mapName;
            this.backupPath = backupPath;

            cancellationTokenSource = new CancellationTokenSource();
            IsRunning = true;

            Task.Run(() => PollFileChanges(cancellationTokenSource.Token));
        }

        public void StopWatching()
        {
            if (!IsRunning) return;

            cancellationTokenSource.Cancel();
            IsRunning = false;
        }

        public void PauseWatching()
        {
            StopWatching();
        }

        public void ResumeWatching()
        {
            if (!IsRunning)
                StartWatching(Path.GetDirectoryName(filePath), mapName, backupPath);
        }

        private async Task PollFileChanges(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        DateTime currentModifiedTime = File.GetLastWriteTime(filePath);

                        if (lastModifiedTime.HasValue && currentModifiedTime > lastModifiedTime.Value)
                        {
                            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HH.mm");
                            string backupFileName = $"{mapName}_{timestamp}.ark";
                            string backupFilePath = Path.Combine(backupPath, backupFileName);

                            File.Copy(filePath, backupFilePath, true);
                            OnFileChanged?.Invoke(this, backupFilePath);
                        }

                        lastModifiedTime = currentModifiedTime;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring file: {ex.Message}");
                }

                await Task.Delay(pollingInterval, cancellationToken);
            }
        }
    }
}