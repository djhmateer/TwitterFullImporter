using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TwitterProcessNoOLTP;

namespace TwitterProcessBootstrap
{
    class Program
    {
        static void Main()
        {
            // Clear down everything for perf testing
            Util.ClearMSSQLDatabaseAndInsertSeedLanguages();
            Util.FlushAllDatabasesFromRedis();

            Util.DeleteAllFromRabbitWorkerQueue();
            Util.PopulateRabbitWorkerQueueWithFilenames();

            Util.UpdateRedisLanguageCacheAndLargestLanguageIDFromMSSQL();
            Util.UpdateRedisHashTagCacheAndLargestHashTagIDFromMSSQL();
            // end clear down

            var process = new Dictionary<Process, int>();
            for (var i = 1; i <= 7; i++)
            {
                var p = new Process();
                p.StartInfo.FileName = @"..\..\..\TwitterProcessNoOLTP\bin\Debug\TwitterProcessNoOLTP.exe";
                p.StartInfo.Arguments = "bootstrapperCalling";
                p.Start();
                process.Add(p, i);
                //Thread.Sleep(100);
            }
        }
    }
}
