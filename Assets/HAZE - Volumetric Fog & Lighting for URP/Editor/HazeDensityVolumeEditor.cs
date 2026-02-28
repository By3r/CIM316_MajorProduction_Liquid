using System;
using Haze.Runtime;
using UnityEditor;

namespace Haze.Editor
{
    [CustomEditor(typeof(HazeDensityVolume)), CanEditMultipleObjects]
    public class HazeDensityVolumeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var densityVolumeMode = ((HazeDensityVolume)target).DensityMode;
            var serializedProperty = serializedObject.GetIterator();
            var isSubtractive = densityVolumeMode == HazeDensityVolume.VolumeDensityMode.Subtractive;
            serializedProperty.NextVisible(true);
            
            for (var i = 0; i < 4; i++)
            {
                serializedProperty.NextVisible(false);
                EditorGUILayout.PropertyField(serializedProperty, true);
            }
            
            while (serializedProperty.NextVisible(false))
            {
                if (isSubtractive && !serializedProperty.name.Contains("Height", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                EditorGUILayout.PropertyField(serializedProperty, true);
            }

            if (isSubtractive)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_secondaryLightDensityBoost"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
