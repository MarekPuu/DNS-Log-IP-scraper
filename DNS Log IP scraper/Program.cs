using DNS_Log_IP_scraper;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WindowWidth = Math.Min(160, Console.LargestWindowWidth);
        Console.WindowHeight = Math.Min(40, Console.LargestWindowHeight);
        Console.BufferHeight = Math.Min(40, Console.LargestWindowHeight);

        Console.Write("Enter root folder path: ");
        string folderPath = Console.ReadLine();
        folderPath = folderPath.Trim('\"', '\'');

        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                Console.WriteLine("Path cannot be empty");
                Console.ReadLine();
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"Directory does not exist: {folderPath}");
                Console.ReadLine();
                return;
            }

            var testAccess = Directory.GetFiles(folderPath);
            Console.WriteLine($"Found {testAccess.Length} files in directory");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accessing path: {ex.Message}");
            Console.ReadLine();
            return;
        }

        Console.WriteLine($"Using {Environment.ProcessorCount} processors");

        var files = Directory.GetFiles(folderPath);
        // Smaller and less specific regex string would result 3-4x faster read times
        var ipValidation = @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b";
        var regex = new Regex(ipValidation, RegexOptions.Compiled | RegexOptions.NonBacktracking);
        var stopwatch = Stopwatch.StartNew();
        string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"unique_ips_{DateTime.Now:dd_MM_yyyy}.txt");

        var globalUniqueIps = new HashSet<string>();
        int totalFiles = files.Length;
        int processedFiles = 0;

        StatusManager.Initialize(totalFiles);

        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            file =>
            {
                StatusManager.InitializeStatus(Path.GetFileName(file));
                var uniqueIpsInFile = ProcessFile(file, regex);
                lock (globalUniqueIps)
                {
                    foreach (var ip in uniqueIpsInFile)
                    {
                        globalUniqueIps.Add(ip);
                    }
                }
                int completed = Interlocked.Increment(ref processedFiles);
            }
        );

        Console.WriteLine("\nSaving unique IPs to file...");
        await File.WriteAllLinesAsync(outputPath, globalUniqueIps.OrderBy(ip => ip));
        stopwatch.Stop();

        Console.WriteLine($"File saved to {outputPath}");
        Console.WriteLine($"\nProcessing completed in {stopwatch.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine($"Total unique IP addresses found: {globalUniqueIps.Count}");

        Console.Write("Scan is ready...");
        Console.ReadLine();
    }

    static HashSet<string> ProcessFile(string filePath, Regex ipRegex)
    {
        var uniqueIps = new HashSet<string>();
        try
        {
            string fileName = Path.GetFileName(filePath);
            StatusManager.UpdateStatus(fileName, "Processing");
            //Console.WriteLine($"Processing file {fileName}...");
            var lineCount = 0;
            var startTime = DateTime.Now;

            using var memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var accessor = memoryMappedFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            using var reader = new StreamReader(accessor, Encoding.UTF8);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineCount++;
                var matches = ipRegex.Matches(line);
                foreach (Match match in matches)
                {
                    uniqueIps.Add(match.Value);
                }
            }

            var duration = (DateTime.Now - startTime).TotalSeconds;
            var rate = Math.Round(lineCount / duration, 2);
            StatusManager.UpdateStatus(fileName, "Ready", rate, uniqueIps.Count);
            //Console.WriteLine($"[{fileName}] Processed {lineCount:N0} lines. " +
            //              $"Rate: {rate:N0} lines/second. " +
            //              $"Unique IPs in file: {uniqueIps.Count:N0}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file {Path.GetFileName(filePath)}: {ex.Message}");
        }
        return uniqueIps;
    }
}