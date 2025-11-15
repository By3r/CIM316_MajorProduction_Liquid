#if UNITY_EDITOR
using _Scripts.Systems.ProceduralGeneration;
using UnityEditor;
using UnityEngine;

namespace _Scripts.ProceduralGeneration
{
    [CustomEditor(typeof(BoundsChecker))]
    public class BoundsCheckerEditor : UnityEditor.Editor
    {
        private BoundsChecker _boundsChecker;

        private SerializedProperty _autoRegisterWithRegistry;
        private SerializedProperty _registerOnStart;
        private SerializedProperty _boundsCenter;
        private SerializedProperty _boundsSize;
        private SerializedProperty _useUniformPadding;
        private SerializedProperty _uniformPadding;
        private SerializedProperty _axisBasedPadding;
        private SerializedProperty _socketOffsetDistance;
        private SerializedProperty _useTightCollisionBounds;
        private SerializedProperty _socketCollisionTolerance;
        private SerializedProperty _showGizmos;
        private SerializedProperty _boundsColor;
        private SerializedProperty _paddedBoundsColor;
        private SerializedProperty _collisionBoundsColor;

        private bool _showRegistrySettings = true;
        private bool _showBoundsSettings = true;
        private bool _showPaddingSettings = true;
        private bool _showSocketSettings = true;
        private bool _showCollisionSettings = true;
        private bool _showGizmoSettings = true;
        private bool _showActions = true;

        private void OnEnable()
        {
            _boundsChecker = (BoundsChecker)target;

            _autoRegisterWithRegistry = serializedObject.FindProperty("_autoRegisterWithRegistry");
            _registerOnStart = serializedObject.FindProperty("_registerOnStart");
            _boundsCenter = serializedObject.FindProperty("_boundsCenter");
            _boundsSize = serializedObject.FindProperty("_boundsSize");
            _useUniformPadding = serializedObject.FindProperty("_useUniformPadding");
            _uniformPadding = serializedObject.FindProperty("_uniformPadding");
            _axisBasedPadding = serializedObject.FindProperty("_axisBasedPadding");
            _socketOffsetDistance = serializedObject.FindProperty("_socketOffsetDistance");
            _useTightCollisionBounds = serializedObject.FindProperty("_useTightCollisionBounds");
            _socketCollisionTolerance = serializedObject.FindProperty("_socketCollisionTolerance");
            _showGizmos = serializedObject.FindProperty("_showGizmos");
            _boundsColor = serializedObject.FindProperty("_boundsColor");
            _paddedBoundsColor = serializedObject.FindProperty("_paddedBoundsColor");
            _collisionBoundsColor = serializedObject.FindProperty("_collisionBoundsColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("BoundsChecker - Room Bounds Management", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            DrawRegistrySettings();
            EditorGUILayout.Space(5);

            DrawBoundsSettings();
            EditorGUILayout.Space(5);

            DrawPaddingSettings();
            EditorGUILayout.Space(5);

            DrawSocketOffsetSettings();
            EditorGUILayout.Space(5);

            DrawCollisionSettings();
            EditorGUILayout.Space(5);

            DrawGizmoSettings();
            EditorGUILayout.Space(5);

            DrawActions();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRegistrySettings()
        {
            _showRegistrySettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showRegistrySettings, "Registry Settings");

            if (_showRegistrySettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_autoRegisterWithRegistry, new GUIContent("Auto Register"));
                EditorGUILayout.PropertyField(_registerOnStart, new GUIContent("Register On Start"));

                EditorGUILayout.Space(3);

                GUI.enabled = false;
                EditorGUILayout.Toggle("Is Registered", _boundsChecker.IsRegistered);
                GUI.enabled = true;

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.BeginHorizontal();

                    if (!_boundsChecker.IsRegistered)
                    {
                        if (GUILayout.Button("Register Now (Runtime)"))
                        {
                            _boundsChecker.RegisterWithRegistry();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Unregister (Runtime)"))
                        {
                            _boundsChecker.UnregisterFromRegistry();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawBoundsSettings()
        {
            _showBoundsSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showBoundsSettings, "Bounds Settings");

            if (_showBoundsSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_boundsCenter, new GUIContent("Bounds Center"));
                EditorGUILayout.PropertyField(_boundsSize, new GUIContent("Bounds Size"));

                EditorGUILayout.Space(3);

                if (GUILayout.Button("Calculate Bounds from Renderers"))
                {
                    Undo.RecordObject(_boundsChecker, "Calculate Bounds");
                    _boundsChecker.CalculateBoundsFromRenderers();
                    EditorUtility.SetDirty(_boundsChecker);
                }

                EditorGUILayout.HelpBox(
                    "Automatically calculates bounds from all child Renderers.\n" +
                    "Ignores ConnectionSockets and other non-visual objects.",
                    MessageType.None);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPaddingSettings()
        {
            _showPaddingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showPaddingSettings, "Padding Settings");

            if (_showPaddingSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_useUniformPadding, new GUIContent("Use Uniform Padding"));

                if (_useUniformPadding.boolValue)
                {
                    EditorGUILayout.PropertyField(_uniformPadding, new GUIContent("Uniform Padding"));
                    EditorGUILayout.HelpBox("Padding applied equally on all axes (X, Y, Z).", MessageType.None);
                }
                else
                {
                    EditorGUILayout.PropertyField(_axisBasedPadding, new GUIContent("Axis-Based Padding"));
                    EditorGUILayout.HelpBox("Padding configured per axis for fine control.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSocketOffsetSettings()
        {
            _showSocketSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSocketSettings, "Socket Offset Settings");

            if (_showSocketSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_socketOffsetDistance, new GUIContent("Socket Offset Distance"));
                EditorGUILayout.HelpBox(
                    "Distance (in units) to move sockets outward from the nearest bounds face.\n" +
                    "Recommended: 0.1 - 0.5 units for most rooms.",
                    MessageType.None);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawCollisionSettings()
        {
            _showCollisionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showCollisionSettings, "Collision Settings");

            if (_showCollisionSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_useTightCollisionBounds, new GUIContent("Use Tight Collision Bounds"));
                EditorGUILayout.PropertyField(_socketCollisionTolerance, new GUIContent("Socket Collision Tolerance"));

                EditorGUILayout.HelpBox(
                    "Tight bounds = no padding for precise socket alignment.\n" +
                    "Socket tolerance allows slight overlap at connection points.",
                    MessageType.None);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawGizmoSettings()
        {
            _showGizmoSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showGizmoSettings, "Gizmo Settings");

            if (_showGizmoSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_showGizmos, new GUIContent("Show Gizmos"));

                if (_showGizmos.boolValue)
                {
                    EditorGUILayout.PropertyField(_boundsColor, new GUIContent("Bounds Color"));
                    EditorGUILayout.PropertyField(_paddedBoundsColor, new GUIContent("Padded Bounds Color"));
                    EditorGUILayout.PropertyField(_collisionBoundsColor, new GUIContent("Collision Bounds Color"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActions()
        {
            _showActions = EditorGUILayout.BeginFoldoutHeaderGroup(_showActions, "Socket Tools");

            if (_showActions)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Socket Management", EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Cache Sockets"))
                {
                    Undo.RecordObject(_boundsChecker, "Cache Sockets");
                    _boundsChecker.CacheConnectionSockets();
                    EditorUtility.SetDirty(_boundsChecker);
                }

                if (GUILayout.Button("Adjust Socket Positions"))
                {
                    Undo.RecordObject(_boundsChecker, "Adjust Socket Positions");
                    _boundsChecker.AdjustSocketPositions();
                    EditorUtility.SetDirty(_boundsChecker);
                }

                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Clear Before Positions"))
                {
                    Undo.RecordObject(_boundsChecker, "Clear Before Positions");
                    _boundsChecker.ClearBeforePositions();
                    EditorUtility.SetDirty(_boundsChecker);
                }

                EditorGUILayout.HelpBox(
                    "1. Cache Sockets - Find all ConnectionSocket children\n" +
                    "2. Adjust Positions - Snap sockets to bounds faces + offset\n" +
                    "3. Clear Before - Remove adjustment visualization",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
#endif