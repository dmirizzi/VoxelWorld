using System.Collections.Generic;
using System.Diagnostics;

public static class Profiler
{
    public static void StartProfiling(string subject)
    {
        _currentSubject = subject;
        _stopWatch.Restart();
    }

    public static void StopProfiling()
    {
        _stopWatch.Stop();
        if(_totalMsPerSubject.ContainsKey(_currentSubject))
        {
            _totalMsPerSubject[_currentSubject] += _stopWatch.Elapsed.TotalMilliseconds;
        }
        else
        {
            _totalMsPerSubject[_currentSubject] = _stopWatch.Elapsed.TotalMilliseconds;
        }
        _currentSubject = string.Empty;
    }

    public static void Clear() =>_totalMsPerSubject.Clear();

    public static IReadOnlyDictionary<string, double> GetProfilingResults() => _totalMsPerSubject;

    private static string _currentSubject;

    private static Stopwatch _stopWatch = new Stopwatch();

    private static Dictionary<string, double> _totalMsPerSubject = new Dictionary<string, double>();
}