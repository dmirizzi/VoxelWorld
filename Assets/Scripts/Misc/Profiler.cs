using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

public class ProfilingEntry
{
    public string Subject { get; set; }

    public int Index { get; set; }

    public Stopwatch Stopwatch { get; }

    public double ElapsedMs { get; set; }

    public ProfilingEntry()
    {
        Stopwatch = new Stopwatch();
    }

}

public class MethodProfiler : IDisposable
{
    public MethodProfiler(string subject)
    {
        _token = Profiler.StartProfiling(subject);
    }

    public void Dispose()
    {
        Profiler.StopProfiling(_token);
    }

    private ProfilingEntry _token;
}

public static class Profiler
{
    static Profiler()
    {
        Clear();
    }

    public static ProfilingEntry StartProfiling(string subject)
    {
        var entry = new ProfilingEntry();
        entry.Subject = subject;
        entry.Stopwatch.Restart();

        return entry;
    }

    public static void StopProfiling(ProfilingEntry entry)
    {
        entry.Stopwatch.Stop();
        entry.ElapsedMs = entry.Stopwatch.Elapsed.TotalMilliseconds;

        // Merge the elapsed time into the dictionary
        _cumulativeTimes.AddOrUpdate(
            entry.Subject,
            entry.ElapsedMs, // If the subject doesn't exist, initialize with this value
            (key, existingValue) => existingValue + entry.ElapsedMs // If it exists, add the elapsed time
        );
    }

    public static MethodProfiler ProfileMethod(string subject) => new MethodProfiler(subject);

    public static void Clear()
    {
        _cumulativeTimes = new ConcurrentDictionary<string, double>();
    }

    public static IEnumerable<(string Subject, double TotalElapsedMs)> GetProfilingResults()
    {
        return _cumulativeTimes
            .OrderByDescending(entry => entry.Value)
            .Select(entry => (Subject: entry.Key, TotalElapsedMs: entry.Value));
    }

    public static void WriteProfilingResultsToCSV()
    {
        var fileName = @"E:\Dev\VoxelProfilingData\Profiling.csv";
        InitProfilingCSV(fileName);

        var sb = new StringBuilder();

        sb.Append(GitHelper.GetCurrentGitCommitHash());
        sb.Append(";");

        sb.Append(GitHelper.GetGitCommitMessage());
        sb.Append(";");

        var total = 0.0;
        foreach (var entry in _cumulativeTimes.OrderBy(entry => entry.Key))
        {
            sb.Append(entry.Value);
            sb.Append(";");

            total += entry.Value;
        }
        UnityEngine.Debug.Log($"Synchronous batch processing time: {total}ms");

        sb.Append(total);
        sb.Append(Environment.NewLine);
        File.AppendAllText(fileName, sb.ToString());
    }

    private static void InitProfilingCSV(string fileName)
    {
        if (!File.Exists(fileName))
        {
            var sb = new StringBuilder();
            sb.Append("Commit;");
            sb.Append("CommitMessage;");

            foreach (var subject in _cumulativeTimes.Keys.OrderBy(x => x))
            {
                sb.Append(subject);
                sb.Append(";");
            }
            sb.Append("Total");
            sb.Append(Environment.NewLine);

            File.WriteAllText(fileName, sb.ToString());
        }
    }

    private static ConcurrentDictionary<string, double> _cumulativeTimes;
}