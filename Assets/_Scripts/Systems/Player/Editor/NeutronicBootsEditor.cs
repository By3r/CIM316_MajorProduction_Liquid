using UnityEngine;
using UnityEditor;

namespace Liquid.Player.Equipment.Editor
{
    /// <summary>
    /// Custom inspector for NeutronicBoots component.
    /// Provides quick testing buttons and runtime state visualization.
    /// Future enhancement: Add buttons to test activation/deactivation in editor.
    /// </summary>
    [CustomEditor(typeof(NeutronicBoots))]
    public class NeutronicBootsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            NeutronicBoots boots = (NeutronicBoots)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Is On Ceiling", boots.IsOnCeiling);
                EditorGUILayout.Toggle("Is Transitioning", boots.IsTransitioning);
                EditorGUILayout.Slider("Activation Progress", boots.ActivationProgress, 0f, 1f);
            }

            // TODO: Add manual test buttons here
            // if (Application.isPlaying)
            // {
            //     EditorGUILayout.Space(10);
            //     if (GUILayout.Button("Force Activate"))
            //     {
            //         // Trigger activation
            //     }
            //     if (GUILayout.Button("Force Dismount"))
            //     {
            //         // Trigger dismount
            //     }
            // }
        }
    }
}