namespace FolderSync
{
    using Microsoft.Extensions.Logging;
    using System.Collections;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    public class FolderSyncRoutine
    {
        private readonly ILogger logger;
        public FolderSyncRoutine(string sourcePath, string replicaPath, ILogger logger)
        {
            this.SourcePath = sourcePath;
            this.ReplicaPath = replicaPath;
            this.logger = logger;
        }

        public string SourcePath { get; private set; }

        public string ReplicaPath { get; private set; }

        public async Task SyncAsync()
        {
            await this.SyncAsync(this.SourcePath, this.ReplicaPath); 
        }

        private async Task SyncAsync(string sourcePath, string replicaPath)
        {
            var sourceFiles = Directory.GetFiles(sourcePath).ToList();
            Directory.CreateDirectory(replicaPath);
            var replicaFiles = Directory.GetFiles(replicaPath).ToList();
            this.Sync(sourceFiles, replicaFiles);

            var subfolders = Directory.GetDirectories(sourcePath);

            var subfoldersToDelete = Directory.GetDirectories(replicaPath).
                                        Select(folder => Path.GetRelativePath(this.ReplicaPath, folder)).
                                        Except(subfolders.Select(folder => Path.GetRelativePath(this.SourcePath, folder)));
            
            foreach (var subfolder in subfoldersToDelete)
            {
                Directory.Delete(Path.Combine(this.ReplicaPath, subfolder), true);
            }

            foreach (var folder in subfolders)
            {
                await Task.Run(
                    () => this.SyncAsync(folder, this.BuildAbsoluteReplicationPath(folder)));
            };
        }

        private void Sync(IList<string> sourceFiles, IList<string> replicaFiles)
        {
            var sourceChecksums = this.GetChecksums(sourceFiles);
            var replicaChecksums = this.GetChecksums(replicaFiles);

            var filesToSync = sourceChecksums.Except(replicaChecksums, new FileEqualityComparer());
            var filesToRemove = replicaChecksums.Except(sourceChecksums, new FileEqualityComparer());

            foreach(var file in filesToRemove)
            {
                this.RemoveFile(file.Key);
            }

            foreach (var file in filesToSync)
            {
                this.SyncFile(file.Key, this.BuildAbsoluteReplicationPath(file.Key));
            }
        }

        private void SyncFile(string sourceFilePath, string destination)
        {            
            File.Copy(sourceFilePath, destination, true);
            logger.LogInformation($"Synced file {sourceFilePath}");
        }

        private void RemoveFile(string filePath)
        {
            File.Delete(filePath);
            logger.LogInformation($"Removed file {filePath}");
        }

        private IDictionary<string, byte[]> GetChecksums(IList<string> paths)
        {
            var pathsByChecksums = new Dictionary<string, byte[]>();
            paths.AsParallel().ForAll(path => pathsByChecksums.Add(path, MD5.HashData(File.ReadAllBytes(path))));

            return pathsByChecksums;
        }

        private string BuildAbsoluteReplicationPath(string sourceSubFolderPath)
        {
            return Path.Combine(this.ReplicaPath, Path.GetRelativePath(this.SourcePath, sourceSubFolderPath));
        }
    }

    public class FileEqualityComparer : IEqualityComparer<KeyValuePair<string, byte[]>>
    {
        public bool Equals(KeyValuePair<string, byte[]> file1, KeyValuePair<string, byte[]> file2)
        {
            return Path.GetFileName(file1.Key).Equals(Path.GetFileName(file2.Key)) && StructuralComparisons.StructuralEqualityComparer.Equals(file1.Value, file2.Value);
        }

        public int GetHashCode(KeyValuePair<string, byte[]> file) => file.Value.ToString().GetHashCode();
    }
}
