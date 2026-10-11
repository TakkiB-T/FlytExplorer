using System.Diagnostics;
using FlytExplorer.FileSystem;

namespace FlytExplorer.Benchmarks;

// Run standalone via `dotnet run -c Release -- <folder>` after project is created.
// Results are written to benchmarks/results/<date>/baseline.csv
internal static class FolderEnumerationBenchmark
{
    internal static async Task RunAsync(string folderPath, int iterations = 20)
    {
        var provider = new LocalFolderProvider();
        var latencies = new List<long>(iterations);
        int count = 0;

        // Warm up
        await foreach (var _ in provider.EnumerateAsync(folderPath, CancellationToken.None)) { }

        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            count = 0;
            await foreach (var _ in provider.EnumerateAsync(folderPath, CancellationToken.None))
                count++;
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        latencies.Sort();
        var p50  = latencies[latencies.Count / 2];
        var p95  = latencies[(int)(latencies.Count * 0.95)];
        var p99  = latencies[(int)(latencies.Count * 0.99)];
        var max  = latencies[^1];

        Console.WriteLine($"Folder: {folderPath}  ({count} items)");
        Console.WriteLine($"  p50={p50} ms  p95={p95} ms  p99={p99} ms  max={max} ms  (n={iterations})");

        var date = DateTime.Now.ToString("yyyyMMdd");
        var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "results", date);
        Directory.CreateDirectory(outDir);
        var csv = Path.Combine(outDir, "baseline.csv");
        if (!File.Exists(csv))
            File.WriteAllText(csv, "folder,items,p50_ms,p95_ms,p99_ms,max_ms,iterations,machine,os\n");

        var machine = Environment.MachineName;
        var os = Environment.OSVersion.ToString();
        File.AppendAllText(csv,
            $"\"{folderPath}\",{count},{p50},{p95},{p99},{max},{iterations},\"{machine}\",\"{os}\"\n");

        Console.WriteLine($"  Saved to {csv}");
    }
}
