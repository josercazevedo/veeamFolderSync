namespace FolderSync
{
    using Microsoft.Extensions.Logging;

    class Program
    {
        private static ILogger logger;

        static async Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            logger = loggerFactory.CreateLogger("FolderSync");

            string sourcePath = args[0];
            string replicaPath = args[1];
            var syncInterval = int.Parse(args[2]);

            while(true)
            {
                var routine = new FolderSyncRoutine(sourcePath, replicaPath, logger);
                await SyncAsync(routine);

                Thread.Sleep(syncInterval);
            }           
        }

        internal static async Task SyncAsync(FolderSyncRoutine fsr)
        {
            await fsr.SyncAsync();
        }
    }
}
