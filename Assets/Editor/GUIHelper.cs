using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

static class GUIHelper
{
    private static  GUIStyle _borderedLabelStyle;

    private static bool _initialized = false;    

    public static void Initialize()
    {
        // Create a custom GUIStyle for the label
        _borderedLabelStyle = new GUIStyle(EditorStyles.label);

        // Create a background texture with a border
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, Color.gray);
        backgroundTexture.Apply();
        _borderedLabelStyle.normal.background = backgroundTexture;

        // Set padding to make the text not touch the border
        _borderedLabelStyle.padding = new RectOffset(10, 10, 5, 5);

        // Set text color
        _borderedLabelStyle.normal.textColor = Color.white;

        _initialized = true;
    }        


    public static Vector2 Table<T>(
        int tableHeight,
        string[] columnNames,
        int[] columnWidths,
        Func<T, string>[] columnValueSelectors,
        IEnumerable<T> tableData,
        Vector2 scrollPosition
    )
    {
        if(!_initialized)
        {
            Initialize();
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(tableHeight));

        // Draw table headers
        GUILayout.BeginHorizontal();
        for(int i = 0; i < columnNames.Length; ++i)
        {
            GUILayout.Label(columnNames[i], _borderedLabelStyle, GUILayout.Width(columnWidths[i]));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Draw table rows
        foreach (var row in tableData)
        {
            GUILayout.BeginHorizontal();
            for(int i = 0; i < columnValueSelectors.Length; ++i)
            {
                GUILayout.Label(
                    columnValueSelectors[i](row), 
                    _borderedLabelStyle, 
                    GUILayout.Width(columnWidths[i]));
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        return scrollPosition;
    }
}