using UnityEngine;
using UnityEditor;

namespace _Scripts.UI.Interaction.Editor
{
    [CustomEditor(typeof(ObjectHighlightingSystem))]
    public class ObjectHighlightingSystemEditor : UnityEditor.Editor
    {
        private SerializedProperty _raycastDistance;
        private SerializedProperty _playerCamera;
        private SerializedProperty _layerConfigs;
        private SerializedProperty _bracketExpandSpeed;
        private SerializedProperty _frameTrackingSpeed;
        private SerializedProperty _fadeSpeed;
        private SerializedProperty _minFrameSize;
        private SerializedProperty _framePadding;
        private SerializedProperty _highlightCanvas;
        private SerializedProperty _highlightText;
        private SerializedProperty _cornerBrackets;
        private SerializedProperty _crosshairManager;
        private SerializedProperty _fadeCrosshairOnHighlight;
        private SerializedProperty _crosshairFadeSpeed;
        private SerializedProperty _animateBracketsFromCenter;

        private void OnEnable()
        {
            _raycastDistance = serializedObject.FindProperty("_raycastDistance");
            _playerCamera = serializedObject.FindProperty("_playerCamera");
            _layerConfigs = serializedObject.FindProperty("_layerConfigs");
            _bracketExpandSpeed = serializedObject.FindProperty("_bracketExpandSpeed");
            _frameTrackingSpeed = serializedObject.FindProperty("_frameTrackingSpeed");
            _fadeSpeed = serializedObject.FindProperty("_fadeSpeed");
            _minFrameSize = serializedObject.FindProperty("_minFrameSize");
            _framePadding = serializedObject.FindProperty("_framePadding");
            _highlightCanvas = serializedObject.FindProperty("_highlightCanvas");
            _highlightText = serializedObject.FindProperty("_highlightText");
            _cornerBrackets = serializedObject.FindProperty("_cornerBrackets");
            _crosshairManager = serializedObject.FindProperty("_crosshairManager");
            _fadeCrosshairOnHighlight = serializedObject.FindProperty("_fadeCrosshairOnHighlight");
            _crosshairFadeSpeed = serializedObject.FindProperty("_crosshairFadeSpeed");
            _animateBracketsFromCenter = serializedObject.FindProperty("_animateBracketsFromCenter");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            DrawRaycastSettings();
            EditorGUILayout.Space(10);
            DrawLayerConfigurations();
            EditorGUILayout.Space(10);
            DrawAnimationSettings();
            EditorGUILayout.Space(10);
            DrawFrameSettings();
            EditorGUILayout.Space(10);
            DrawUIReferences();
            EditorGUILayout.Space(10);
            DrawCrosshairIntegration();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRaycastSettings()
        {
            EditorGUILayout.LabelField("Raycast Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_raycastDistance, new GUIContent("Raycast Distance"));
            EditorGUILayout.PropertyField(_playerCamera, new GUIContent("Player Camera"));
        }

        private void DrawLayerConfigurations()
        {
            EditorGUILayout.LabelField("Layer Configurations", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("+ Add Layer Config", GUILayout.Height(25)))
            {
                _layerConfigs.arraySize++;
                SerializedProperty newConfig = _layerConfigs.GetArrayElementAtIndex(_layerConfigs.arraySize - 1);
                newConfig.FindPropertyRelative("configName").stringValue = "New Config";
                newConfig.FindPropertyRelative("enabled").boolValue = true;
                newConfig.FindPropertyRelative("layer").intValue = 0;
                newConfig.FindPropertyRelative("showBrackets").boolValue = true;
                newConfig.FindPropertyRelative("showText").boolValue = true;
                newConfig.FindPropertyRelative("displayText").stringValue = "Interact";
                newConfig.FindPropertyRelative("bracketColor").colorValue = new Color(0.2f, 0.8f, 1f, 1f);
                newConfig.FindPropertyRelative("bracketSize").floatValue = 20f;
                newConfig.FindPropertyRelative("textColor").colorValue = Color.white;
                newConfig.FindPropertyRelative("textFontSize").intValue = 16;
                newConfig.FindPropertyRelative("fadeCrosshair").boolValue = true;
            }

            EditorGUILayout.Space(5);

            for (int i = 0; i < _layerConfigs.arraySize; i++)
            {
                DrawLayerConfig(_layerConfigs.GetArrayElementAtIndex(i), i);
                EditorGUILayout.Space(5);
            }
        }

        private void DrawLayerConfig(SerializedProperty config, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            SerializedProperty enabled = config.FindPropertyRelative("enabled");
            SerializedProperty configName = config.FindPropertyRelative("configName");

            enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(20));
            EditorGUILayout.LabelField($"{configName.stringValue}", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Delete Config?",
                    $"Remove '{configName.stringValue}'?", "Delete", "Cancel"))
                {
                    _layerConfigs.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!enabled.boolValue)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(configName, new GUIContent("Config Name"));

            SerializedProperty layer = config.FindPropertyRelative("layer");
            layer.intValue = EditorGUILayout.LayerField("Layer", layer.intValue);

            EditorGUILayout.Space(3);

            SerializedProperty showBrackets = config.FindPropertyRelative("showBrackets");
            SerializedProperty showText = config.FindPropertyRelative("showText");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(showBrackets, new GUIContent("Show Brackets"));
            EditorGUILayout.PropertyField(showText, new GUIContent("Show Text"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            if (showText.boolValue)
            {
                EditorGUILayout.PropertyField(config.FindPropertyRelative("displayText"), new GUIContent("Display Text"));
                EditorGUILayout.Space(3);
            }

            SerializedProperty fadeCrosshair = config.FindPropertyRelative("fadeCrosshair");
            EditorGUILayout.PropertyField(fadeCrosshair, new GUIContent("Fade Crosshair"));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationSettings()
        {
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_bracketExpandSpeed, new GUIContent("Bracket Expand Speed"));
            EditorGUILayout.PropertyField(_frameTrackingSpeed, new GUIContent("Frame Tracking Speed"));
            EditorGUILayout.PropertyField(_fadeSpeed, new GUIContent("Fade Speed"));
        }

        private void DrawFrameSettings()
        {
            EditorGUILayout.LabelField("Frame Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minFrameSize, new GUIContent("Min Frame Size"));
            EditorGUILayout.PropertyField(_framePadding, new GUIContent("Frame Padding"));
        }

        private void DrawUIReferences()
        {
            EditorGUILayout.LabelField("UI References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_highlightCanvas, new GUIContent("Canvas"));
            EditorGUILayout.PropertyField(_cornerBrackets, new GUIContent("Brackets (TL, TR, BL, BR)"), true);
            EditorGUILayout.PropertyField(_highlightText, new GUIContent("Text"));
        }

        private void DrawCrosshairIntegration()
        {
            EditorGUILayout.LabelField("Crosshair Integration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_crosshairManager, new GUIContent("Crosshair Manager"));
            EditorGUILayout.PropertyField(_fadeCrosshairOnHighlight, new GUIContent("Fade Crosshair (Global)"));

            if (_fadeCrosshairOnHighlight.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_crosshairFadeSpeed, new GUIContent("Fade Speed"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_animateBracketsFromCenter, new GUIContent("Animate from Center"));
        }
    }
}
