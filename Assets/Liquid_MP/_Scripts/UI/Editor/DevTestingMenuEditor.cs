using UnityEditor;
using UnityEngine;

namespace _Scripts.UI.Editor
{
    [CustomEditor(typeof(DevTestingMenu))]
    public class DevTestingMenuEditor : UnityEditor.Editor
    {
        private SerializedProperty _sceneNames;
        private SerializedProperty _toggleButton;
        private SerializedProperty _toggleButtonText;
        private SerializedProperty _menuPanel;
        private SerializedProperty _buttonContainer;
        private SerializedProperty _buttonPrefab;
        private SerializedProperty _closeButtonTextColor;
        private SerializedProperty _eventSystem;

        private void OnEnable()
        {
            _sceneNames = serializedObject.FindProperty("_sceneNames");
            _toggleButton = serializedObject.FindProperty("_toggleButton");
            _toggleButtonText = serializedObject.FindProperty("_toggleButtonText");
            _menuPanel = serializedObject.FindProperty("_menuPanel");
            _buttonContainer = serializedObject.FindProperty("_buttonContainer");
            _buttonPrefab = serializedObject.FindProperty("_buttonPrefab");
            _closeButtonTextColor = serializedObject.FindProperty("_closeButtonTextColor");
            _eventSystem = serializedObject.FindProperty("_eventSystem");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Persists across scene loads.\n" +
                "Scene names must match Build Settings exactly.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Scenes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sceneNames, new GUIContent("Scene Names"), true);

            if (_sceneNames.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add at least one scene.", MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Populate From Build Settings"))
            {
                PopulateFromBuildSettings();
            }
            if (GUILayout.Button("Clear All"))
            {
                _sceneNames.ClearArray();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("UI References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_toggleButton);
            EditorGUILayout.PropertyField(_toggleButtonText);
            EditorGUILayout.PropertyField(_menuPanel);
            EditorGUILayout.PropertyField(_buttonContainer);
            EditorGUILayout.PropertyField(_buttonPrefab);
            EditorGUILayout.PropertyField(_eventSystem);

            bool hasAllReferences = _toggleButton.objectReferenceValue != null &&
                                    _toggleButtonText.objectReferenceValue != null &&
                                    _menuPanel.objectReferenceValue != null &&
                                    _buttonContainer.objectReferenceValue != null &&
                                    _buttonPrefab.objectReferenceValue != null &&
                                    _eventSystem.objectReferenceValue != null;

            if (!hasAllReferences)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Missing UI references. Run Tools > LIQUID > Setup Dev Testing Menu.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Styling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_closeButtonTextColor);

            serializedObject.ApplyModifiedProperties();
        }

        private void PopulateFromBuildSettings()
        {
            _sceneNames.ClearArray();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;

                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
                _sceneNames.InsertArrayElementAtIndex(_sceneNames.arraySize);
                _sceneNames.GetArrayElementAtIndex(_sceneNames.arraySize - 1).stringValue = sceneName;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"[DevTestingMenu] Added {_sceneNames.arraySize} scenes from Build Settings.");
        }
    }
}