using System;
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

        lock(_entries)
        {
            _entries.AddLast(entry);
        }

        return entry;
    }

    public static void StopProfiling(ProfilingEntry entry)
    {
        entry.Stopwatch.Stop();
        entry.ElapsedMs = entry.Stopwatch.Elapsed.TotalMilliseconds;
    }

    public static void Clear()
    {
        _entries = new LinkedList<ProfilingEntry>();
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

        var elapsedPerSubject = _entries
            .GroupBy(entry => entry.Subject)
            .Select(group => new {
                Subject = group.Key,
                TotalElapsedMs = group.Sum(entry => entry.ElapsedMs)
            })
            .OrderBy(entry => entry.Subject);

        var total = 0.0;        
        foreach(var entry in elapsedPerSubject)
        {
            sb.Append(entry.TotalElapsedMs);
            sb.Append(";");

            total += entry.TotalElapsedMs;
        }
        UnityEngine.Debug.Log($"Synchronous batch processing time: {total}ms");

        sb.Append(total);
        sb.Append(Environment.NewLine);
        File.AppendAllText(fileName, sb.ToString());
    }

    private static void InitProfilingCSV(string fileName)
    {
        if(!File.Exists(fileName))
        {
            var sb = new StringBuilder();
            sb.Append("Commit;");
            sb.Append("CommitMessage;");

            var allSubjects = _entries.Select(x => x.Subject).Distinct();
            foreach(var entry in allSubjects)
            {
                sb.Append(entry);
                sb.Append(";");
            }
            sb.Append("Total");
            sb.Append(Environment.NewLine);
            
            File.WriteAllText(fileName, sb.ToString());
        }
    }

    private static LinkedList<ProfilingEntry> _entries;

    private static int _currentIndex = 0;
}