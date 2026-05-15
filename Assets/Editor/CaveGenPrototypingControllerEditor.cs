using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CaveGenPrototypingController))]
public class CaveGenPrototypingControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var ctrl = (CaveGenPrototypingController)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Randomize Seed"))
        {
            serializedObject.Update();
            serializedObject.FindProperty("Seed").intValue = Random.Range(int.MinValue, int.MaxValue);
            serializedObject.ApplyModifiedProperties(); // triggers OnValidate just like a manual field edit
        }
    }
}
