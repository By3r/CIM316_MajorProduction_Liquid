#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LiquidEnemy))]
public class LiquidEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LiquidEnemy enemy = (LiquidEnemy)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("GOAP Runtime Debug", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Current Goal", enemy.DebugCurrentGoalName);
        EditorGUILayout.LabelField("Current Action", enemy.DebugCurrentActionName);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("World State Dump", EditorStyles.boldLabel);

        GUIStyle box = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            richText = false
        };

        string dump = "";
        if (enemy.DebugWorldState != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var kvp in enemy.DebugWorldState)
            {
                sb.Append(kvp.Key);
                sb.Append(" = ");
                sb.Append(kvp.Value != null ? kvp.Value.ToString() : "null");
                sb.AppendLine();
            }

            dump = sb.ToString();
        }

        EditorGUILayout.TextArea(dump, GUILayout.MinHeight(120));
    }
}
#endif