using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Octokit;
using LibGitRepository = LibGit2Sharp.Repository;
using OctokitRepository = Octokit.Repository;

namespace Backup
{
    public class GitHubManager
    {
        private readonly string sourceFolderPath;
        private readonly string localRepoPath;
        private readonly string gitHubToken;
        private string userName;
        private readonly Octokit.GitHubClient client;
        private readonly string repoName;

        private const long EmergencyThresholdBytes = 900L * 1024 * 1024; // 900 MB

        public GitHubManager(string sourceFolderPath, string gitHubToken, string mapName)
        {
            this.sourceFolderPath = sourceFolderPath;
            this.gitHubToken = gitHubToken;
            this.repoName = $"{mapName}-repo";
            this.localRepoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, repoName);

            client = new Octokit.GitHubClient(new ProductHeaderValue("BackupApp"))
            {
                Credentials = new Octokit.Credentials(gitHubToken)
            };
        }

        public async Task InitializeAsync()
        {
            Logger.Instance.Log("Initializing GitHub manager...", LogType.Success);

            try
            {
                var user = await client.User.Current();
                userName = user.Login;
                Logger.Instance.Log($"Logged in as {userName}.", LogType.Success);
            }
            catch (Octokit.AuthorizationException ex) {
                Logger.Instance.Log($"Error while trying to login: {ex}", LogType.Error);
            }

            OctokitRepository repo;
            try
            {
                repo = await client.Repository.Get(userName, repoName);
                Logger.Instance.Log($"Repository {repoName} exists.", LogType.Success);
            }
            catch (Octokit.NotFoundException)
            {
                Logger.Instance.Log($"Repository {repoName} not found. Creating new repository.", LogType.Warning);
                var newRepo = new NewRepository(repoName)
                {
                    AutoInit = true,
                    Private = false
                };
                repo = await client.Repository.Create(newRepo);
                Logger.Instance.Log($"Repository {repoName} created successfully.", LogType.Success);
            }

            if (!Directory.Exists(localRepoPath))
            {
                LibGitRepository.Clone(repo.CloneUrl, localRepoPath);
                Logger.Instance.Log($"Repository {repoName} cloned locally to {localRepoPath}.", LogType.Success);
            }
        }

        public async Task PushFilesAsync()
        {
            Logger.Instance.Log($"Pushing important files from {sourceFolderPath} to GitHub.", LogType.Success);

            long repoSize = GetRepoSize(localRepoPath);
            if (repoSize > EmergencyThresholdBytes)
            {
                Logger.Instance.Log($"Repository size exceeded {EmergencyThresholdBytes / (1024 * 1024)} MB. Triggering emergency override.", LogType.Error);
                await NukeRepoFromTheOrbit();
                await InitializeAsync();
            }
            else
            {
                Logger.Instance.Log($"Current repo Size: {repoSize / (1024 * 1024)} / {EmergencyThresholdBytes / (1024 * 1024)} MB.", LogType.Success);
            }

            // Get all relevant files (skipping .bak extensions)
            var importantFiles = Directory.GetFiles(sourceFolderPath)
                                          .Where(file => !file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
                                                         !file.EndsWith(".profilebak", StringComparison.OrdinalIgnoreCase))
                                          .ToList();
            if (!importantFiles.Any())
            {
                Logger.Instance.Log("No important files found to push.", LogType.Warning);
                return;
            }

            using (var repo = new LibGitRepository(localRepoPath))
            {
                var pullOptions = new LibGit2Sharp.PullOptions
                {
                    FetchOptions = new LibGit2Sharp.FetchOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) =>
                            new LibGit2Sharp.UsernamePasswordCredentials { Username = gitHubToken, Password = "" }
                    }
                };

                var signature = new LibGit2Sharp.Signature(userName, $"{userName}@users.noreply.github.com", DateTimeOffset.Now);

                try
                {
                    LibGit2Sharp.Commands.Pull(repo, signature, pullOptions);
                    Logger.Instance.Log("Pulled latest changes from GitHub.", LogType.Success);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Pull failed: {ex.Message}", LogType.Warning);
                }

                foreach (var filePath in importantFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string destPath = Path.Combine(localRepoPath, fileName);

                    File.Copy(filePath, destPath, true);

                    LibGit2Sharp.Commands.Stage(repo, fileName);
                    Logger.Instance.Log($"Staged file: {fileName}", LogType.Success);
                }

                repo.Commit("Update important files from source folder.", signature, signature);
                Logger.Instance.Log("Committed changes to the local repository.", LogType.Success);

                var pushOptions = new LibGit2Sharp.PushOptions
                {
                    CredentialsProvider = (_url, _user, _cred) =>
                        new LibGit2Sharp.UsernamePasswordCredentials { Username = gitHubToken, Password = "" }
                };

                try
                {
                    repo.Network.Push(repo.Head, pushOptions);
                    Logger.Instance.Log("Files pushed to GitHub successfully.", LogType.Success);
                }
                catch (LibGit2Sharp.NonFastForwardException ex)
                {
                    Logger.Instance.Log($"Push failed: {ex.Message}", LogType.Error);
                    throw;
                }
            }
        }

        private long GetRepoSize(string path)
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                            .Sum(file => new FileInfo(file).Length);
        }

        private async Task NukeRepoFromTheOrbit()
        {
            Logger.Instance.Log($"Nuking repository {repoName} from orbit. This operation is irreversible.", LogType.Warning);

            // Delete remote repository
            try
            {
                await client.Repository.Delete(userName, repoName);
                Logger.Instance.Log($"Remote repository {repoName} deleted.", LogType.Success);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to delete remote repository: {ex.Message}", LogType.Error);
                throw;
            }

            // Delete local repository
            try
            {
                if (Directory.Exists(localRepoPath))
                {
                    Directory.Delete(localRepoPath, true);
                    Logger.Instance.Log($"Local repository {localRepoPath} deleted.", LogType.Success);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to delete local repository: {ex.Message}", LogType.Error);
                throw;
            }
        }
    }
}