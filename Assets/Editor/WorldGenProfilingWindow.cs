using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class WorldGenProfilingWindow : EditorWindow
{
    [MenuItem("Window/WorldGen Profiling")]
    public static void ShowWindow()
    {
        GetWindow<WorldGenProfilingWindow>("WorldGen Profiling");
    }

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    void OnGUI()
    {
        GUILayout.Label("Batch Profiling Results");

        if( _jobs != null )
        {
            GUILayout.BeginHorizontal();

            _scrollPositionJobs = GUIHelper.Table(
                800,
                new[] { "Job", "Time Elapsed (ms)" },
                new[] { 300, 150 },
                new Func<(string Description, double ElapsedMs), string>[] { 
                    row => row.Description,
                    row => $"{row.ElapsedMs:F1}"
                },
                _jobs,
                _scrollPositionJobs
            );

            _scrollPositionMethods = GUIHelper.Table(
                800,
                new[] { "Method", "Time Elapsed (ms)" },
                new[] { 300, 150 },
                new Func<(string Description, double ElapsedMs), string>[] { 
                    row => row.Description,
                    row => $"{row.ElapsedMs:F1}"
                },
                _methods,
                _scrollPositionMethods
            );            


            GUILayout.EndHorizontal();

        }
        else
        {
            GUILayout.Label("N/A");
        }


        if (GUILayout.Button("Refresh"))
        {
            RefreshData();
        }
    }

    private void RefreshData()
    {
        _jobs = Profiler.GetProfilingEntries()
            .Where(x => x.Subject.StartsWith("Jobs/"))
            .GroupBy(x => x.Subject)
            .Select(x => (
                Description: x.Key.Substring("Jobs/".Length),
                ElapsedMs: x.Sum(y => y.ElapsedMs)
            ))
            .OrderByDescending(x => x.ElapsedMs);

        _methods = Profiler.GetProfilingEntries()
            .Where(x => x.Subject.StartsWith("Methods/"))
            .GroupBy(x => x.Subject)
            .Select(x => (
                Description: x.Key.Substring("Methods/".Length),
                ElapsedMs: x.Sum(y => y.ElapsedMs)
            ))
            .OrderByDescending(x => x.ElapsedMs);

        Repaint();
    }

    private void Initialize()
    {
        _worldUpdateScheduler = GameObject.FindObjectOfType<WorldUpdateScheduler>();
        _worldUpdateScheduler.BatchFinished += RefreshData;
    }

    private void Cleanup()
    {
        _worldUpdateScheduler.BatchFinished -= RefreshData;
        _worldUpdateScheduler = null;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
    {
        if(stateChange == PlayModeStateChange.EnteredPlayMode)
        {
            Initialize();
        }

        if(stateChange ==  PlayModeStateChange.ExitingPlayMode)
        {
            Cleanup();
        }
    }

    private IEnumerable<(string Description, double ElapsedMs)> _jobs;

    private IEnumerable<(string Description, double ElapsedMs)> _methods;

    private Vector2 _scrollPositionJobs, _scrollPositionMethods;

    private static WorldUpdateScheduler _worldUpdateScheduler;
}