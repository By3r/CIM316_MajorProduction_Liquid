using _Scripts.Systems.ProceduralGeneration.Doors;
using UnityEditor;
using UnityEngine;

namespace _Scripts.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(Door))]
    public class DoorEditor : UnityEditor.Editor
    {
        private SerializedProperty _doorTypeProp;
        private SerializedProperty _animationTypeProp;
        private SerializedProperty _slideDirectionProp;
        private SerializedProperty _slideDistanceProp;
        private SerializedProperty _rotationAxisProp;
        private SerializedProperty _rotationAngleProp;
        private SerializedProperty _frontRotationAngleProp;
        private SerializedProperty _backRotationAngleProp;
        private SerializedProperty _openingDurationProp;
        private SerializedProperty _closingDurationProp;
        private SerializedProperty _autoCloseProp;
        private SerializedProperty _autoCloseDelayProp;
        private SerializedProperty _allowManualCloseProp;
        private SerializedProperty _openSoundProp;
        private SerializedProperty _closeSoundProp;
        private SerializedProperty _noiseGeneratedProp;

        private void OnEnable()
        {
            _doorTypeProp = serializedObject.FindProperty("_doorType");
            _animationTypeProp = serializedObject.FindProperty("_animationType");
            _slideDirectionProp = serializedObject.FindProperty("_slideDirection");
            _slideDistanceProp = serializedObject.FindProperty("_slideDistance");
            _rotationAxisProp = serializedObject.FindProperty("_rotationAxis");
            _rotationAngleProp = serializedObject.FindProperty("_rotationAngle");
            _frontRotationAngleProp = serializedObject.FindProperty("_frontRotationAngle");
            _backRotationAngleProp = serializedObject.FindProperty("_backRotationAngle");
            _openingDurationProp = serializedObject.FindProperty("_openingDuration");
            _closingDurationProp = serializedObject.FindProperty("_closingDuration");
            _autoCloseProp = serializedObject.FindProperty("_autoClose");
            _autoCloseDelayProp = serializedObject.FindProperty("_autoCloseDelay");
            _allowManualCloseProp = serializedObject.FindProperty("_allowManualClose");
            _openSoundProp = serializedObject.FindProperty("_openSound");
            _closeSoundProp = serializedObject.FindProperty("_closeSound");
            _noiseGeneratedProp = serializedObject.FindProperty("_noiseGenerated");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDoorTypeConfiguration();
            EditorGUILayout.Space(10);
            
            DrawAnimationType();
            EditorGUILayout.Space(10);
            
            DrawAnimationSettings();
            EditorGUILayout.Space(10);
            
            DrawInteractionSettings();
            EditorGUILayout.Space(10);
            
            DrawThreatSystemSettings();
            EditorGUILayout.Space(10);
            
            DrawAudioSettings();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                DrawRuntimeInfo((Door)target);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDoorTypeConfiguration()
        {
            EditorGUILayout.LabelField("Door Type Configuration", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_doorTypeProp, new GUIContent("Door Type", 
                "The tier/type of this door for procedural generation. Must match ConnectionSocket types."));
            
        }

        private void DrawAnimationType()
        {
            EditorGUILayout.LabelField("Door Animation Type", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_animationTypeProp, new GUIContent("Animation Type", "How should this door open?"));
            
            var animationType = (Door.DoorAnimationType)_animationTypeProp.enumValueIndex;
            
            EditorGUI.indentLevel++;
            
            if (animationType == Door.DoorAnimationType.Slide)
            {
                EditorGUILayout.PropertyField(_slideDirectionProp, new GUIContent("Slide Direction", "Local direction to slide (will be normalized)"));
                EditorGUILayout.PropertyField(_slideDistanceProp, new GUIContent("Slide Distance", "How far to slide (in units)"));
                
                if (_slideDistanceProp.floatValue <= 0)
                {
                    EditorGUILayout.HelpBox("Slide distance should be greater than 0!", MessageType.Warning);
                }
            }
            else if (animationType == Door.DoorAnimationType.Rotation)
            {
                EditorGUILayout.PropertyField(_rotationAxisProp, new GUIContent("Rotation Axis", "Local axis to rotate around (will be normalized)"));
                EditorGUILayout.PropertyField(_rotationAngleProp, new GUIContent("Rotation Angle", "Degrees to rotate when opening"));
                
                if (_rotationAngleProp.floatValue == 0)
                {
                    EditorGUILayout.HelpBox("Rotation angle is 0 - door will not move!", MessageType.Warning);
                }
            }
            else if (animationType == Door.DoorAnimationType.SmartRotation)
            {
                EditorGUILayout.PropertyField(_rotationAxisProp, new GUIContent("Rotation Axis", "Local axis to rotate around (will be normalized)"));
                EditorGUILayout.PropertyField(_frontRotationAngleProp, new GUIContent("Front Rotation", "Degrees when player approaches from front (positive)"));
                EditorGUILayout.PropertyField(_backRotationAngleProp, new GUIContent("Back Rotation", "Degrees when player approaches from back (negative)"));
                
                if (_frontRotationAngleProp.floatValue == 0 && _backRotationAngleProp.floatValue == 0)
                {
                    EditorGUILayout.HelpBox("Both rotation angles are 0 - door will not move!", MessageType.Warning);
                }
                
                if (Mathf.Approximately(_frontRotationAngleProp.floatValue, _backRotationAngleProp.floatValue))
                {
                    EditorGUILayout.HelpBox("Front and back angles are the same - consider using regular Rotation instead.", MessageType.Info);
                }
            }
            
            EditorGUI.indentLevel--;
        }

        private void DrawAnimationSettings()
        {
            EditorGUILayout.LabelField("Animation Timing", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_openingDurationProp, new GUIContent("Opening Duration (seconds)", "How long the opening animation takes"));
            EditorGUILayout.PropertyField(_closingDurationProp, new GUIContent("Closing Duration (seconds)", "How long the closing animation takes"));
            
            if (_openingDurationProp.floatValue <= 0)
            {
                EditorGUILayout.HelpBox("Opening duration should be greater than 0!", MessageType.Warning);
            }
            
            if (_closingDurationProp.floatValue <= 0)
            {
                EditorGUILayout.HelpBox("Closing duration should be greater than 0!", MessageType.Warning);
            }
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.PropertyField(_autoCloseProp, new GUIContent("Auto Close", "Should the door automatically close after opening?"));
            if (_autoCloseProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_autoCloseDelayProp, new GUIContent("Auto Close Delay", "How long to wait before closing (seconds)"));
                
                if (_autoCloseDelayProp.floatValue <= 0)
                {
                    EditorGUILayout.HelpBox("Auto close delay should be greater than 0!", MessageType.Warning);
                }
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawInteractionSettings()
        {
            EditorGUILayout.LabelField("Interaction Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_allowManualCloseProp, new GUIContent("Allow Manual Close", "Can the player manually close an open door?"));
            
        }

        private void DrawThreatSystemSettings()
        {
            EditorGUILayout.LabelField("Threat System Integration", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_noiseGeneratedProp, new GUIContent("Noise Generated", 
                "How much noise this door generates when opened (added to threat level)"));
            
            if (_noiseGeneratedProp.floatValue < 0)
            {
                EditorGUILayout.HelpBox("Noise generated should not be negative!", MessageType.Warning);
            }
            
        }

        private void DrawAudioSettings()
        {
            EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_openSoundProp, new GUIContent("Open Sound", "Sound when door opens"));
            EditorGUILayout.PropertyField(_closeSoundProp, new GUIContent("Close Sound", "Sound when door closes"));
            
        }

        private void DrawRuntimeInfo(Door door)
        {
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);
            
            GUI.enabled = false;
            
            EditorGUILayout.TextField("Door Type", door.Type.ToString());
            EditorGUILayout.TextField("Animation Type", door.AnimationType.ToString());
            EditorGUILayout.Toggle("Is Open", door.IsOpen);
            EditorGUILayout.Toggle("Is Animating", door.IsAnimating);
            EditorGUILayout.Toggle("Allow Manual Close", door.AllowManualClose);
            
            GUI.enabled = true;
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Force Open (Test)"))
            {
                door.ForceOpen();
            }
            
            if (GUILayout.Button("Force Close (Test)"))
            {
                door.ForceClose();
            }
            
        }
    }
}