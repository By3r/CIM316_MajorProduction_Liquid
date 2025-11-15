using UnityEditor;
using UnityEngine;

namespace _Scripts.UI.Editor
{
    [CustomEditor(typeof(CrosshairManager))]
    public class CrosshairManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _normalCrosshairProp;
        private SerializedProperty _interactionCrosshairProp;
        private SerializedProperty _crosshairImageProp;
        private SerializedProperty _crosshairCanvasGroupProp;
        private SerializedProperty _hideNormalCrosshairProp;
        private SerializedProperty _alwaysShowCrosshairProp;
        private SerializedProperty _transitionSpeedProp;
        private SerializedProperty _interactionScaleProp;
        private SerializedProperty _enablePulseEffectProp;
        private SerializedProperty _pulseSpeedProp;
        private SerializedProperty _pulseIntensityProp;
        private SerializedProperty _useColorChangeProp;
        private SerializedProperty _interactionColorProp;
        private SerializedProperty _normalColorProp;
        
        private SerializedProperty _enableInteractionRotationProp;
        private SerializedProperty _interactionRotationAnglesProp;
        private SerializedProperty _interactionRotationDurationProp;
        private SerializedProperty _interactionRotationCurveProp;

        private bool _showSpriteSettings = true;
        private bool _showUIReferences = true;
        private bool _showVisibilitySettings = true;
        private bool _showAnimationSettings = true;
        private bool _showColorSettings = true;
        private bool _showRotationSettings = true;

        private void OnEnable()
        {
            _normalCrosshairProp = serializedObject.FindProperty("_normalCrosshair");
            _interactionCrosshairProp = serializedObject.FindProperty("_interactionCrosshair");
            _crosshairImageProp = serializedObject.FindProperty("_crosshairImage");
            _crosshairCanvasGroupProp = serializedObject.FindProperty("_crosshairCanvasGroup");
            _hideNormalCrosshairProp = serializedObject.FindProperty("_hideNormalCrosshair");
            _alwaysShowCrosshairProp = serializedObject.FindProperty("_alwaysShowCrosshair");
            _transitionSpeedProp = serializedObject.FindProperty("_transitionSpeed");
            _interactionScaleProp = serializedObject.FindProperty("_interactionScale");
            _enablePulseEffectProp = serializedObject.FindProperty("_enablePulseEffect");
            _pulseSpeedProp = serializedObject.FindProperty("_pulseSpeed");
            _pulseIntensityProp = serializedObject.FindProperty("_pulseIntensity");
            _useColorChangeProp = serializedObject.FindProperty("_useColorChange");
            _interactionColorProp = serializedObject.FindProperty("_interactionColor");
            _normalColorProp = serializedObject.FindProperty("_normalColor");
            
            _enableInteractionRotationProp = serializedObject.FindProperty("_enableInteractionRotation");
            _interactionRotationAnglesProp = serializedObject.FindProperty("_interactionRotationAngles");
            _interactionRotationDurationProp = serializedObject.FindProperty("_interactionRotationDuration");
            _interactionRotationCurveProp = serializedObject.FindProperty("_interactionRotationCurve");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var crosshairManager = target as CrosshairManager;

            EditorGUILayout.LabelField("Crosshair Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manages crosshair appearance. Shows normal crosshair by default, " +
                "switches to interaction crosshair when looking at interactables. " +
                "Integrates with InteractionController for detection.",
                MessageType.Info);
            EditorGUILayout.Space();

            _showSpriteSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSpriteSettings, "Crosshair Sprites");
            if (_showSpriteSettings)
            {
                EditorGUI.indentLevel++;
                DrawSpriteSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            _showUIReferences = EditorGUILayout.BeginFoldoutHeaderGroup(_showUIReferences, "UI References");
            if (_showUIReferences)
            {
                EditorGUI.indentLevel++;
                DrawUIReferences();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            _showVisibilitySettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showVisibilitySettings, "Visibility Settings");
            if (_showVisibilitySettings)
            {
                EditorGUI.indentLevel++;
                DrawVisibilitySettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            _showAnimationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showAnimationSettings, "Animation Settings");
            if (_showAnimationSettings)
            {
                EditorGUI.indentLevel++;
                DrawAnimationSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            _showColorSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showColorSettings, "Color Settings (Optional)");
            if (_showColorSettings)
            {
                EditorGUI.indentLevel++;
                DrawColorSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            _showRotationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showRotationSettings, "Interaction Rotation Animation");
            if (_showRotationSettings)
            {
                EditorGUI.indentLevel++;
                DrawRotationSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            if (Application.isPlaying)
            {
                DrawRuntimeInfo(crosshairManager);
                EditorGUILayout.Space();
            }

            DrawUtilityButtons(crosshairManager);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSpriteSettings()
        {
            EditorGUILayout.PropertyField(_normalCrosshairProp, new GUIContent("Normal Crosshair", 
                "Crosshair sprite shown by default (e.g., simple dot)"));
            EditorGUILayout.PropertyField(_interactionCrosshairProp, new GUIContent("Interaction Crosshair", 
                "Crosshair sprite when looking at interactables"));

            if (_normalCrosshairProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Normal crosshair sprite is not assigned!", MessageType.Warning);
            }

            if (_interactionCrosshairProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Interaction crosshair sprite is not assigned!", MessageType.Warning);
            }

            if (_normalCrosshairProp.objectReferenceValue != null || _interactionCrosshairProp.objectReferenceValue != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Sprite Preview", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSpritePreview("Normal", _normalCrosshairProp.objectReferenceValue as Sprite);
                    DrawSpritePreview("Interaction", _interactionCrosshairProp.objectReferenceValue as Sprite);
                }
            }
        }

        private void DrawSpritePreview(string label, Sprite sprite)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(80)))
            {
                if (sprite != null)
                {
                    var rect = GUILayoutUtility.GetRect(60, 60);
                    GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
                    EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(60));
                }
                else
                {
                    var rect = GUILayoutUtility.GetRect(60, 60);
                    EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                    EditorGUILayout.LabelField($"{label}\n(None)", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(60));
                }
            }
        }

        private void DrawUIReferences()
        {
            EditorGUILayout.PropertyField(_crosshairImageProp, new GUIContent("Crosshair Image", 
                "Image component that displays the crosshair sprite"));
            EditorGUILayout.PropertyField(_crosshairCanvasGroupProp, new GUIContent("Canvas Group (Optional)", 
                "Canvas group for smooth fading (optional)"));

            if (_crosshairImageProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Crosshair Image is required! Assign the Image component.", MessageType.Error);
            }

            if (_crosshairCanvasGroupProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No Canvas Group assigned. Visibility will toggle instantly instead of fading.",
                    MessageType.Info);
            }
        }

        private void DrawVisibilitySettings()
        {
            EditorGUILayout.PropertyField(_hideNormalCrosshairProp, new GUIContent("Hide Normal Crosshair", 
                "Hide crosshair when not looking at interactables (immersion mode)"));
            EditorGUILayout.PropertyField(_alwaysShowCrosshairProp, new GUIContent("Always Show Crosshair", 
                "Always show crosshair regardless of interaction state"));

            if (_hideNormalCrosshairProp.boolValue && _alwaysShowCrosshairProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Conflicting settings: 'Always Show' overrides 'Hide Normal'.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(3);

            if (_hideNormalCrosshairProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Immersion Mode: Crosshair only visible when looking at interactables.",
                    MessageType.Info);
            }
            else if (_alwaysShowCrosshairProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Normal Mode: Crosshair always visible, changes appearance when looking at interactables.",
                    MessageType.Info);
            }
        }

        private void DrawAnimationSettings()
        {
            EditorGUILayout.PropertyField(_transitionSpeedProp, new GUIContent("Transition Speed", 
                "How fast crosshair changes between states"));
            EditorGUILayout.PropertyField(_interactionScaleProp, new GUIContent("Interaction Scale", 
                "Scale multiplier when showing interaction crosshair"));

            if (_transitionSpeedProp.floatValue <= 0)
            {
                EditorGUILayout.HelpBox("Transition speed should be greater than 0!", MessageType.Warning);
            }

            if (_interactionScaleProp.floatValue <= 0)
            {
                EditorGUILayout.HelpBox("Interaction scale should be greater than 0!", MessageType.Warning);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Pulse Effect", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_enablePulseEffectProp, new GUIContent("Enable Pulse", 
                "Make the crosshair pulse when looking at interactables"));

            if (_enablePulseEffectProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_pulseSpeedProp, new GUIContent("Pulse Speed", 
                    "How fast the pulse oscillates"));
                EditorGUILayout.PropertyField(_pulseIntensityProp, new GUIContent("Pulse Intensity", 
                    "Strength of the pulse effect (0-1)"));

                if (_pulseSpeedProp.floatValue <= 0)
                {
                    EditorGUILayout.HelpBox("Pulse speed should be greater than 0!", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawColorSettings()
        {
            EditorGUILayout.PropertyField(_useColorChangeProp, new GUIContent("Use Color Change", 
                "Change crosshair color when looking at interactables"));

            if (_useColorChangeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_normalColorProp, new GUIContent("Normal Color", 
                    "Color when not looking at interactables"));
                EditorGUILayout.PropertyField(_interactionColorProp, new GUIContent("Interaction Color", 
                    "Color when looking at interactables"));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Color Preview", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawColorPreview("Normal", _normalColorProp.colorValue);
                    DrawColorPreview("Interaction", _interactionColorProp.colorValue);
                }
            }
        }

        private void DrawColorPreview(string label, Color color)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(70)))
            {
                var rect = GUILayoutUtility.GetRect(60, 20);
                EditorGUI.DrawRect(rect, color);
                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(60));
            }
        }

        private void DrawRotationSettings()
        {
            EditorGUILayout.PropertyField(_enableInteractionRotationProp, new GUIContent("Enable Rotation", 
                "Play a quick rotation animation when interacting"));

            if (_enableInteractionRotationProp.boolValue)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.LabelField("Rotation Angles (degrees)", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(_interactionRotationAnglesProp, new GUIContent("Rotation (X, Y, Z)", 
                    "Rotation angles on each axis in degrees"));
                
                Vector3 angles = _interactionRotationAnglesProp.vector3Value;
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"X: {angles.x:F1} degrees (pitch)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Y: {angles.y:F1} degrees (yaw)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Z: {angles.z:F1} degrees (roll)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(_interactionRotationDurationProp, new GUIContent("Animation Duration", 
                    "How long the rotation takes (in seconds)"));
                
                EditorGUILayout.PropertyField(_interactionRotationCurveProp, new GUIContent("Animation Curve", 
                    "Curve for the rotation animation (makes it feel more dynamic)"));

                EditorGUI.indentLevel--;

                if (_interactionRotationDurationProp.floatValue <= 0)
                {
                    EditorGUILayout.HelpBox("Animation duration should be greater than 0!", MessageType.Warning);
                }

                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "The rotation animation plays when you interact with something while looking at it. " +
                    "It rotates on all three axes (X=pitch, Y=yaw, Z=roll) simultaneously. " +
                    "When the sprite changes back to normal, the rotation resets automatically.",
                    MessageType.Info);

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.HelpBox(
                        "Tip: Look at a door and press E to see the 3D rotation animation in action!",
                        MessageType.None);
                }
            }
        }

        private void DrawRuntimeInfo(CrosshairManager manager)
        {
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);

            GUI.enabled = false;
            EditorGUILayout.Toggle("Is Showing Interaction", manager.IsShowingInteraction);
            EditorGUILayout.Toggle("Is Visible", manager.IsCrosshairVisible());
            EditorGUILayout.Toggle("Hide Normal Crosshair", manager.HideNormalCrosshair);
            GUI.enabled = true;

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(manager.GetDebugInfo(), MessageType.None);

            if (manager.GetDebugInfo().Contains("Rotating: True"))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox("Rotation animation is playing!", MessageType.Info);
            }

            Repaint();
        }

        private void DrawUtilityButtons(CrosshairManager manager)
        {
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Force Show"))
                    {
                        manager.SetCrosshairVisible(true);
                    }

                    if (GUILayout.Button("Force Hide"))
                    {
                        manager.SetCrosshairVisible(false);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Toggle Hide Normal"))
                    {
                        manager.SetHideNormalCrosshair(!manager.HideNormalCrosshair);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use runtime utilities.", MessageType.Info);
            }
        }
    }
}