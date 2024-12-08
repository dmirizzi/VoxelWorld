using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class WorldGenProfilingWindow : EditorWindow
{
    private IEnumerable<(string Description, double ElapsedMs)> _profilingEntries;

    private Vector2 _scrollPosition;

    [MenuItem("Window/WorldGen Profiling")]
    public static void ShowWindow()
    {
        GetWindow<WorldGenProfilingWindow>("WorldGen Profiling");
    }

    void OnGUI()
    {
        GUILayout.Label("Batch Profiling Results");

        if( _profilingEntries != null )
        {
            _scrollPosition = GUIHelper.Table(
                800,
                new[] { "Job", "Time Elapsed (ms)" },
                new[] { 300, 150 },
                new Func<(string Description, double ElapsedMs), string>[] { 
                    row => row.Description,
                    row => $"{row.ElapsedMs:F1}"
                },
                _profilingEntries,
                _scrollPosition
            );

/*
            // Create a scrollable area
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(800));

            // Draw table headers
            GUILayout.BeginHorizontal();
            GUILayout.Label("Job", _borderedLabelStyle, GUILayout.Width(ProfilingTableDescriptionWidth));
            GUILayout.Label("Time Elapsed (ms)", _borderedLabelStyle, GUILayout.Width(ProfilingTableElapsedWidth));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Draw table rows
            foreach (var entry in _profilingEntries)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.Description, _borderedLabelStyle, GUILayout.Width(ProfilingTableDescriptionWidth));
                GUILayout.Label($"{entry.ElapsedMs:F1}", _borderedLabelStyle, GUILayout.Width(ProfilingTableElapsedWidth));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            */
        }
        else
        {
            GUILayout.Label("N/A");
        }


        if (GUILayout.Button("Refresh"))
        {
            _profilingEntries = Profiler.GetProfilingEntries()
                .Where(x => x.Subject.StartsWith("Jobs/"))
                .GroupBy(x => x.Subject)
                .Select(x => (
                    Description: x.Key.Substring("Jobs/".Length),
                    ElapsedMs: x.Sum(y => y.ElapsedMs)
                ))
                .OrderByDescending(x => x.ElapsedMs);
        }
    }
}