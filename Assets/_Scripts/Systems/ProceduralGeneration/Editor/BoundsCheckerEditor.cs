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
        private SerializedProperty _subBounds;
        private SerializedProperty _showGizmos;
        private SerializedProperty _boundsColor;

        private bool _showRegistrySettings = true;
        private bool _showBoundsSettings = true;
        private bool _showSubBoundsSettings = true;
        private bool _showGizmoSettings = true;
        private bool _showActions = true;

        private void OnEnable()
        {
            _boundsChecker = (BoundsChecker)target;

            _autoRegisterWithRegistry = serializedObject.FindProperty("_autoRegisterWithRegistry");
            _registerOnStart = serializedObject.FindProperty("_registerOnStart");
            _boundsCenter = serializedObject.FindProperty("_boundsCenter");
            _boundsSize = serializedObject.FindProperty("_boundsSize");
            _subBounds = serializedObject.FindProperty("_subBounds");
            _showGizmos = serializedObject.FindProperty("_showGizmos");
            _boundsColor = serializedObject.FindProperty("_boundsColor");
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

            DrawSubBoundsSettings();
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

        private void DrawSubBoundsSettings()
        {
            int subCount = _subBounds.arraySize;
            string headerLabel = subCount > 0
                ? $"Sub-Bounds ({subCount})"
                : "Sub-Bounds";

            _showSubBoundsSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSubBoundsSettings, headerLabel);

            if (_showSubBoundsSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Define multiple sub-bounds for L-shaped or cross-shaped rooms.\n" +
                    "When 2+ sub-bounds are defined, the main Bounds above becomes the " +
                    "auto-calculated encapsulating AABB. Leave empty for simple rectangular rooms.",
                    MessageType.None);

                EditorGUILayout.Space(3);

                // Draw each sub-bound entry
                for (int i = 0; i < _subBounds.arraySize; i++)
                {
                    SerializedProperty entry = _subBounds.GetArrayElementAtIndex(i);
                    SerializedProperty centerProp = entry.FindPropertyRelative("center");
                    SerializedProperty sizeProp = entry.FindPropertyRelative("size");
                    SerializedProperty labelProp = entry.FindPropertyRelative("label");

                    string entryLabel = string.IsNullOrEmpty(labelProp.stringValue)
                        ? $"Sub-Bound {i}"
                        : labelProp.stringValue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entryLabel, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(18)))
                    {
                        _subBounds.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        break;
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.PropertyField(labelProp, new GUIContent("Label"));
                    EditorGUILayout.PropertyField(centerProp, new GUIContent("Center"));
                    EditorGUILayout.PropertyField(sizeProp, new GUIContent("Size"));

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.Space(3);

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("+ Add Sub-Bound", GUILayout.Height(25)))
                {
                    _subBounds.InsertArrayElementAtIndex(_subBounds.arraySize);
                    serializedObject.ApplyModifiedProperties();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Recalculate Encapsulating", GUILayout.Height(25)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(_boundsChecker, "Recalculate Encapsulating Bounds");
                    _boundsChecker.RecalculateEncapsulatingBounds();
                    EditorUtility.SetDirty(_boundsChecker);
                    serializedObject.Update();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // Auto-detect button
                GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button("Auto-Detect Sub-Bounds from Renderers", GUILayout.Height(28)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(_boundsChecker, "Auto-Detect Sub-Bounds");
                    _boundsChecker.AutoDetectSubBounds();
                    EditorUtility.SetDirty(_boundsChecker);
                    serializedObject.Update();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    "Auto-detect scans all renderers and creates sub-bounds for non-rectangular rooms. " +
                    "Rectangular rooms get no sub-bounds (single AABB is sufficient).",
                    MessageType.None);

                if (_subBounds.arraySize == 1)
                {
                    EditorGUILayout.HelpBox(
                        "A single sub-bound has no effect. Add 2+ sub-bounds for compound collision, " +
                        "or remove all for simple rectangular bounds.",
                        MessageType.Warning);
                }

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
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActions()
        {
            _showActions = EditorGUILayout.BeginFoldoutHeaderGroup(_showActions, "Tools");

            if (_showActions)
            {
                EditorGUI.indentLevel++;

                if (GUILayout.Button("Cache Connection Sockets"))
                {
                    Undo.RecordObject(_boundsChecker, "Cache Sockets");
                    _boundsChecker.CacheConnectionSockets();
                    EditorUtility.SetDirty(_boundsChecker);
                }

                EditorGUILayout.HelpBox(
                    "Cache Sockets - Finds all ConnectionSocket children.\n" +
                    "Sockets now live directly on door frame wall pieces.\n" +
                    "Use 'Calculate Bounds from Renderers' above to auto-fit bounds.",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
#endif
