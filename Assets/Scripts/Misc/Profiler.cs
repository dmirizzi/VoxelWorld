using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

public class ProfilingToken
{
    public string Subject { get; private set; }

    public ProfilingToken(string subject)
    {
        Subject = subject;
        _tokenStr = $"{Subject}{System.Guid.NewGuid().ToString()}";
    }

    public override string ToString() => _tokenStr;

    public static implicit operator string(ProfilingToken token) => token._tokenStr;

    private string _tokenStr;
}

public static class Profiler
{
    public static ProfilingToken StartProfiling(string subject)
    {
        var token = new ProfilingToken(subject);
        lock(_lockObject)
        {
            _stopwatches[token] = new Stopwatch();
            _stopwatches[token].Restart();
        }
        return token;
    }

    public static void StopProfiling(ProfilingToken token)
    {
        lock(_lockObject)
        {
            var stopwatch = _stopwatches[token];
            stopwatch.Stop();
            if(_totalMsPerSubject.ContainsKey(token.Subject))
            {
                _totalMsPerSubject[token.Subject] += stopwatch.Elapsed.TotalMilliseconds;
            }
            else
            {
                _totalMsPerSubject[token.Subject] = stopwatch.Elapsed.TotalMilliseconds;
            }
        }
    }

    public static void Clear() =>_totalMsPerSubject.Clear();

    public static IReadOnlyDictionary<string, double> GetProfilingResults() => _totalMsPerSubject;

    public static void WriteProfilingResultsToCSV() 
    {
        var fileName = "./profiling.csv";
        InitProfilingCSV(fileName);

        var sb = new StringBuilder();

        sb.Append(GitHelper.GetCurrentGitCommitHash());
        sb.Append(";");

        sb.Append(GitHelper.GetGitCommitMessage());
        sb.Append(";");

        var total = 0.0;        
        foreach(var entry in _totalMsPerSubject.OrderBy(x => x.Key))
        {
            sb.Append(entry.Value);
            sb.Append(";");

            total += entry.Value;
        }
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
            foreach(var entry in _totalMsPerSubject.OrderBy(x => x.Key))
            {
                sb.Append(entry.Key);
                sb.Append(";");
            }
            sb.Append("Total");
            sb.Append(Environment.NewLine);
            
            File.WriteAllText(fileName, sb.ToString());
        }
    }

    private static Dictionary<string, double> _totalMsPerSubject = new Dictionary<string, double>();

    private static Dictionary<string, Stopwatch> _stopwatches = new Dictionary<string, Stopwatch>();

    private static object _lockObject = new object();
}