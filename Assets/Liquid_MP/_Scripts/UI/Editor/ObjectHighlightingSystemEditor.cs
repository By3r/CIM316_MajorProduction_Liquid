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
        private SerializedProperty _animationSpeed;
        private SerializedProperty _fadeSpeed;
        private SerializedProperty _minFrameSize;
        private SerializedProperty _framePadding;
        private SerializedProperty _highlightCanvas;
        private SerializedProperty _highlightFrame;
        private SerializedProperty _highlightText;
        private SerializedProperty _cornerBrackets;
        private SerializedProperty _crosshairManager;
        private SerializedProperty _fadeCrosshairOnHighlight;
        private SerializedProperty _crosshairFadeSpeed;
        private SerializedProperty _animateBracketsFromCenter;
        private SerializedProperty _showDebugLogs;

        private void OnEnable()
        {
            _raycastDistance = serializedObject.FindProperty("_raycastDistance");
            _playerCamera = serializedObject.FindProperty("_playerCamera");
            _layerConfigs = serializedObject.FindProperty("_layerConfigs");
            _animationSpeed = serializedObject.FindProperty("_animationSpeed");
            _fadeSpeed = serializedObject.FindProperty("_fadeSpeed");
            _minFrameSize = serializedObject.FindProperty("_minFrameSize");
            _framePadding = serializedObject.FindProperty("_framePadding");
            _highlightCanvas = serializedObject.FindProperty("_highlightCanvas");
            _highlightFrame = serializedObject.FindProperty("_highlightFrame");
            _highlightText = serializedObject.FindProperty("_highlightText");
            _cornerBrackets = serializedObject.FindProperty("_cornerBrackets");
            _showDebugLogs = serializedObject.FindProperty("_showDebugLogs");
            _crosshairManager = serializedObject.FindProperty("_crosshairManager");
            _fadeCrosshairOnHighlight = serializedObject.FindProperty("_fadeCrosshairOnHighlight");
            _crosshairFadeSpeed = serializedObject.FindProperty("_crosshairFadeSpeed");
            _animateBracketsFromCenter = serializedObject.FindProperty("_animateBracketsFromCenter");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer-Based Object Highlighting", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure different highlight styles for different object types using layers.\n" +
                "Add layer configs below to customize brackets, colors, and text per layer.",
                MessageType.Info);

            EditorGUILayout.Space(10);

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
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showDebugLogs, new GUIContent("Show Debug Logs"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRaycastSettings()
        {
            EditorGUILayout.LabelField("Raycast Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_raycastDistance, new GUIContent("Raycast Distance"));
            EditorGUILayout.PropertyField(_playerCamera, new GUIContent("Player Camera"));

            if (_playerCamera.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Camera will auto-find Main Camera at runtime.", MessageType.None);
            }
        }

        private void DrawLayerConfigurations()
        {
            EditorGUILayout.LabelField("Layer Configurations", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Add a config for each object type you want to highlight.\n" +
                "Each layer can have custom brackets, colors, text, and crosshair behavior.",
                MessageType.None);

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.green;
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
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            for (int i = 0; i < _layerConfigs.arraySize; i++)
            {
                DrawLayerConfig(_layerConfigs.GetArrayElementAtIndex(i), i);
                EditorGUILayout.Space(5);
            }

            if (_layerConfigs.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No layer configs. Click '+ Add Layer Config' to get started.", MessageType.Warning);
            }
        }

        private void DrawLayerConfig(SerializedProperty config, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            SerializedProperty enabled = config.FindPropertyRelative("enabled");
            SerializedProperty configName = config.FindPropertyRelative("configName");
            
            enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(20));
            
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = enabled.boolValue ? Color.white : Color.gray;
            EditorGUILayout.LabelField($"Config {index + 1}: {configName.stringValue}", headerStyle);
            
            GUILayout.FlexibleSpace();
            
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Delete Config?", 
                    $"Remove '{configName.stringValue}' configuration?", "Delete", "Cancel"))
                {
                    _layerConfigs.DeleteArrayElementAtIndex(index);
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();

            if (!enabled.boolValue)
            {
                EditorGUILayout.HelpBox("Config disabled", MessageType.None);
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

            if (showBrackets.boolValue)
            {
                EditorGUILayout.LabelField("Bracket Settings", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(config.FindPropertyRelative("bracketColor"), new GUIContent("Color"));
                EditorGUILayout.PropertyField(config.FindPropertyRelative("bracketSize"), new GUIContent("Size"));
                EditorGUILayout.PropertyField(config.FindPropertyRelative("bracketSprite"), new GUIContent("Custom Sprite (Optional)"));
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            if (showText.boolValue)
            {
                EditorGUILayout.LabelField("Text Settings", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(config.FindPropertyRelative("displayText"), new GUIContent("Display Text"));
                EditorGUILayout.PropertyField(config.FindPropertyRelative("textColor"), new GUIContent("Color"));
                EditorGUILayout.PropertyField(config.FindPropertyRelative("textFontSize"), new GUIContent("Font Size"));
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            EditorGUILayout.LabelField("Crosshair Behavior", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            
            SerializedProperty fadeCrosshair = config.FindPropertyRelative("fadeCrosshair");
            EditorGUILayout.PropertyField(fadeCrosshair, new GUIContent("Fade Crosshair"));
            
            if (!_fadeCrosshairOnHighlight.boolValue)
            {
                EditorGUILayout.HelpBox("Global crosshair fading is disabled. Enable it in Crosshair Integration section.", MessageType.Info);
            }
            else if (!fadeCrosshair.boolValue)
            {
                EditorGUILayout.HelpBox("Crosshair will NOT fade when highlighting this layer.", MessageType.Info);
            }
            
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationSettings()
        {
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_animationSpeed, new GUIContent("Animation Speed"));
            EditorGUILayout.PropertyField(_fadeSpeed, new GUIContent("Fade Speed"));
            
            EditorGUILayout.HelpBox(
                "Higher values = snappier, less floaty.\n" +
                "Animation Speed: 12+ recommended for responsive feel\n" +
                "Fade Speed: 15+ recommended for quick transitions",
                MessageType.None);
        }

        private void DrawFrameSettings()
        {
            EditorGUILayout.LabelField("Frame Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_minFrameSize, new GUIContent("Min Frame Size"));
            EditorGUILayout.PropertyField(_framePadding, new GUIContent("Frame Padding"));
        }

        private void DrawUIReferences()
        {
            EditorGUILayout.LabelField("UI References (Auto-created)", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_highlightCanvas, new GUIContent("Canvas"));
            EditorGUILayout.PropertyField(_highlightFrame, new GUIContent("Frame"));
            EditorGUILayout.PropertyField(_highlightText, new GUIContent("Text"));
            EditorGUILayout.PropertyField(_cornerBrackets, new GUIContent("Brackets"), true);

            if (_highlightCanvas.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("UI will be auto-created at runtime.", MessageType.None);
            }
        }

        private void DrawCrosshairIntegration()
        {
            EditorGUILayout.LabelField("Crosshair Integration", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_crosshairManager, new GUIContent("Crosshair Manager"));
            
            if (_crosshairManager.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign CrosshairManager for morphing effect.", MessageType.Warning);
            }
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.PropertyField(_fadeCrosshairOnHighlight, new GUIContent("Fade Crosshair (Global)"));
            
            if (_fadeCrosshairOnHighlight.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_crosshairFadeSpeed, new GUIContent("Fade Speed"));
                EditorGUILayout.HelpBox(
                    "100+ recommended for instant fade.\n" +
                    "Per-layer fade control available in each Layer Config.",
                    MessageType.None);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.PropertyField(_animateBracketsFromCenter, new GUIContent("Animate from Center"));
            
            if (_animateBracketsFromCenter.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Crosshair Morphing Effect:\n" +
                    "• Crosshair fades out when highlighting\n" +
                    "• Brackets start at screen center\n" +
                    "• Brackets animate to corners\n" +
                    "• Brackets return to center when fading out\n" +
                    "• Crosshair fades back in when highlight ends",
                    MessageType.Info);
            }
        }
    }
}