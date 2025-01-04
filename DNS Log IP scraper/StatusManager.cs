using DNS_Log_IP_scraper.Model;
using System.Collections.Concurrent;

namespace DNS_Log_IP_scraper
{
    public static class StatusManager
    {
        private static readonly ConcurrentDictionary<string, FileStatus> FileStatuses = new();
        private static readonly ConcurrentDictionary<string, int> FileRows = new();
        private static readonly object ConsoleLock = new();
        private static int startRow = 0;
        private static int bufferSize = 0;
        private static int currentRowCount = 0;
        private static int completedFiles = 0;
        private static DateTime lastConsoleUpdate = DateTime.MinValue;
        private static Timer updateTimer;
        private static readonly HashSet<string> ActiveFiles = new();

        public static void InitializeStatus(string fileName)
        {
            lock (ConsoleLock)
            {
                FileStatuses[fileName] = new FileStatus(fileName);
                FileRows[fileName] = currentRowCount++;
                ActiveFiles.Add(fileName);

                Console.SetCursorPosition(0, startRow + FileRows[fileName]);
                Console.WriteLine($"Pending - {fileName}");

                updateTimer ??= new Timer(CheckAndUpdate, null, 0, 100);
            }
        }

        private static void CheckAndUpdate(object state)
        {
            if ((DateTime.Now - lastConsoleUpdate).TotalMilliseconds < 100)
            {
                return;
            }

            lock (ConsoleLock)
            {
                foreach (var fileName in ActiveFiles.ToList()) 
                {
                    if (FileStatuses.TryGetValue(fileName, out var status) && status.NeedsUpdate)
                    {
                        UpdateSingleRow(status);
                        status.NeedsUpdate = false;

                        if (status.Status == "Ready" || status.Status.StartsWith("Error"))
                        {
                            ActiveFiles.Remove(fileName);
                        }
                    }
                }
                lastConsoleUpdate = DateTime.Now;
            }
        }

        public static void UpdateStatus(string fileName, string status, double rate = 0, int uniqueIps = 0)
        {
            if (FileStatuses.TryGetValue(fileName, out var fileStatus))
            {
                var wasNotCompleted = fileStatus.Status == "Pending" || fileStatus.Status == "Processing";
                var isNowCompleted = status == "Ready" || status.StartsWith("Error");

                if (wasNotCompleted && isNowCompleted)
                {
                    Interlocked.Increment(ref completedFiles);
                }

                if (fileStatus.Status != status ||
                    Math.Abs(fileStatus.Rate - rate) > 0.1 ||
                    fileStatus.UniqueIps != uniqueIps)
                {
                    fileStatus.Status = status;
                    fileStatus.Rate = rate;
                    fileStatus.UniqueIps = uniqueIps;
                    fileStatus.LastUpdate = DateTime.Now;
                    fileStatus.NeedsUpdate = true;

                    if (!ActiveFiles.Contains(fileName))
                    {
                        ActiveFiles.Add(fileName);
                    }
                }
            }
        }

        private static void UpdateSingleRow(FileStatus status)
        {
            if (!FileRows.TryGetValue(status.FileName, out int rowIndex))
            {
                return;
            }

            var originalRow = Console.CursorTop;
            var originalCol = Console.CursorLeft;

            try
            {
                Console.SetCursorPosition(0, startRow + rowIndex);

                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.SetCursorPosition(0, startRow + rowIndex);

                string statusLine = $"{status.Status} - {status.FileName}";
                if (status.Rate > 0)
                {
                    statusLine += $" - Rate: {status.Rate:N0} lines/sec";
                }
                if (status.UniqueIps > 0)
                {
                    statusLine += $" - Unique IPs: {status.UniqueIps:N0}";
                }
                Console.Write(statusLine);
            }
            finally
            {
                Console.SetCursorPosition(originalCol, originalRow);
            }
        }

        public static void Initialize(int fileCount)
        {
            startRow = Console.CursorTop;
            bufferSize = fileCount;

            int consoleBufferHeight = Console.BufferHeight;
            int minRequiredBufferSize = bufferSize + startRow + 8;
            if (minRequiredBufferSize > consoleBufferHeight)
            {
                Console.BufferHeight = minRequiredBufferSize;
            }

        }

        public static void Cleanup()
        {
            updateTimer?.Dispose();
        }
    }
}
