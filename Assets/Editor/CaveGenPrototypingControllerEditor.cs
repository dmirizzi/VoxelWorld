using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrototypingControllerBase), editorForChildClasses: true)]
public class PrototypingControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Randomize Seed"))
        {
            serializedObject.Update();
            serializedObject.FindProperty("Seed").intValue = Random.Range(int.MinValue, int.MaxValue);
            serializedObject.ApplyModifiedProperties(); // triggers OnValidate just like a manual field edit
        }
    }
}
