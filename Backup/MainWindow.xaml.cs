using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Backup
{
    public partial class MainWindow : Window
    {
        private FileWatcher fileWatcher;
        private ConfigManager configManager;
        private Config userConfig;

        private string savePath;
        private string backupPath;
        private string mapName;
        
        private GitHubManager gitHubManager;

        private BackupManager backupManager;
        public MainWindow()
        {
            InitializeComponent();
            // Initialize Logger
            Logger.Instance.Initialize(LogBox);

            try
            {
                fileWatcher = new FileWatcher();
                configManager = new ConfigManager();

                userConfig = configManager.LoadConfig();

                ApplyConfig();

                fileWatcher.OnFileChanged += FileWatcher_OnFileChanged;

                backupManager = new BackupManager(backupPath, mapName);

                if (userConfig.AutoStart && !string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(backupPath) && !string.IsNullOrEmpty(mapName))
                {
                    StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Initialization failed: {ex.Message}", LogType.Error);
                MessageBox.Show("An unexpected error occurred during startup. Please check the logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ApplyConfig()
        {
            // Load paths and map name
            savePath = userConfig.SavePath;
            backupPath = userConfig.BackupPath;
            mapName = userConfig.MapName;

            // Update UI elements
            SavePathLabel.Text = savePath ?? "Not Selected";
            BackupPathLabel.Text = backupPath ?? "Not Selected";
            MapNameTextBox.Text = mapName ?? string.Empty;
            AutoStartCheckBox.IsChecked = userConfig.AutoStart;

            // Load GitHub token
            if (!string.IsNullOrEmpty(userConfig.GitHubToken))
            {
                GitHubTokenBox.Password = userConfig.GitHubToken;
            }
            else
            {
                GitHubTokenBox.Password = string.Empty;
            }
        }

        private void SaveCurrentConfig()
        {
            userConfig.SavePath = savePath;
            userConfig.BackupPath = backupPath;
            userConfig.MapName = mapName;
            userConfig.AutoStart = AutoStartCheckBox.IsChecked ?? false;

            // Save GitHub token if it's not empty
            if (!string.IsNullOrEmpty(GitHubTokenBox.Password))
            {
                userConfig.GitHubToken = GitHubTokenBox.Password;
            }

            configManager.SaveConfig(userConfig);
            Logger.Instance.Log("Configuration saved.", LogType.Success);
        }

        private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SaveCurrentConfig();
        }

        private void BrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            savePath = SelectFolder("Select the SavedArks folder");
            if (!string.IsNullOrEmpty(savePath))
            {
                SavePathLabel.Text = savePath;
                DetermineMapName();
                SaveCurrentConfig();
            }
        }

        private void BrowseBackupPath_Click(object sender, RoutedEventArgs e)
        {
            backupPath = SelectFolder("Select the Backup folder");
            if (!string.IsNullOrEmpty(backupPath))
            {
                BackupPathLabel.Text = backupPath;
                SaveCurrentConfig();
                backupManager = new BackupManager(backupPath, mapName); //Init backupManager again, since it's relying on the backupPath that could change.
            }
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (fileWatcher.IsRunning)
            {
                StopMonitoring();
            }
            else
            {
                if (string.IsNullOrEmpty(savePath) || string.IsNullOrEmpty(backupPath) || string.IsNullOrEmpty(mapName))
                {
                    MessageBox.Show("Please provide a save path and backup path, and ensure a map name is detected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                SaveCurrentConfig();
                StartMonitoring();
            }
        }

        private async void StartMonitoring()
        {
            fileWatcher.StartWatching(savePath, mapName, backupPath);
            Logger.Instance.Log("Monitoring started.", LogType.Success);
            StartStopButton.Content = "Stop Backup";

            if (!string.IsNullOrEmpty(GitHubTokenBox.Password))
            {
                try
                {
                    gitHubManager = new GitHubManager(savePath, GitHubTokenBox.Password, mapName);
                    await gitHubManager.InitializeAsync();
                }
                catch (Exception ex) {
                    Logger.Instance.Log($"Couldn't initialize GithubManager! Error: {ex}", LogType.Error);
                }
            }
        }

        private void StopMonitoring()
        {
            fileWatcher.StopWatching();
            Logger.Instance.Log("Monitoring stopped.", LogType.Warning);
            StartStopButton.Content = "Start Backup";
        }

        private async void FileWatcher_OnFileChanged(object sender, string backupFilePath)
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Instance.Log($"Backup created: {backupFilePath}", LogType.Success);
            });

            if (gitHubManager != null)
            {
                try
                {
                    await gitHubManager.PushFilesAsync();
                }
                catch (Exception ex) {
                    Logger.Instance.Log($"Unexpected githubManager Exception: {ex}", LogType.Error);
                }
            }
        }

        private string SelectFolder(string description)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = description,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select this folder"
            };

            if (dialog.ShowDialog() == true)
            {
                return Path.GetDirectoryName(dialog.FileName);
            }

            return null;
        }
        private void SetDefaultBackupPath()
        {
            // Automatically set default backup path if not provided
            if (string.IsNullOrEmpty(backupPath) && !string.IsNullOrEmpty(mapName))
            {
                string exePath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                backupPath = Path.Combine(exePath ?? AppDomain.CurrentDomain.BaseDirectory, $"{mapName}-backup");

                // Create the folder if it doesn't exist
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                    Logger.Instance.Log($"Created default backup folder: {backupPath}", LogType.Success);
                    BackupPathLabel.Text = backupPath;
                }
            }
        }

        private void DetermineMapName()
        {
            if (string.IsNullOrEmpty(savePath))
            {
                MessageBox.Show("Please select a valid save path first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var arkFiles = Directory.GetFiles(savePath, "*.ark");
                if (arkFiles.Length == 0)
                {
                    MessageBox.Show("No .ark files found in the selected directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var shortestFile = arkFiles.OrderBy(f => Path.GetFileName(f).Length).FirstOrDefault();
                mapName = Path.GetFileNameWithoutExtension(shortestFile);

                MapNameTextBox.Text = mapName;
                Logger.Instance.Log($"Detected map: {mapName}", LogType.Success);
                SetDefaultBackupPath();
                SaveCurrentConfig();
                backupManager = new BackupManager(backupPath, mapName); //Init backupManager again, since it's relying on the mapname that could change.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while detecting the map name: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var availableBackups = backupManager.GetAvailableBackups();

                if (!availableBackups.Any())
                {
                    MessageBox.Show("No backups available to load.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var selectedBackup = ShowBackupSelectionDialog(availableBackups.Select(b => b.DisplayText).ToList());

                if (string.IsNullOrEmpty(selectedBackup))
                    return;
                var timestamp = availableBackups.FirstOrDefault(b => b.DisplayText == selectedBackup).Timestamp;

                fileWatcher.PauseWatching(); //temporarily pause the monitoring, not to interpret the backupload as a file change. 

                backupManager.LoadBackup(timestamp, savePath);
                Logger.Instance.Log($"Loaded backup: {selectedBackup}", LogType.Success);

                MessageBox.Show($"Backup {selectedBackup} successfully loaded.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                fileWatcher.ResumeWatching(); // Resume monitoring
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error loading backup: {ex.Message}", LogType.Error);
                MessageBox.Show($"An error occurred while loading the backup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                fileWatcher.ResumeWatching();
            }
        }
        private string ShowBackupSelectionDialog(List<string> backups)
        {
            var dialog = new Window
            {
                Title = "Select Backup",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); 
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var listBox = new ListBox
            {
                ItemsSource = backups,
                Margin = new Thickness(10),
                SelectionMode = SelectionMode.Single
            };
            Grid.SetRow(listBox, 0);
            grid.Children.Add(listBox);

            var selectButton = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            selectButton.Click += (s, e) => dialog.DialogResult = true;
            Grid.SetRow(selectButton, 1);
            grid.Children.Add(selectButton);

            dialog.Content = grid;

            if (dialog.ShowDialog() == true)
            {
                return listBox.SelectedItem as string;
            }

            return null;
        }
    }
}
