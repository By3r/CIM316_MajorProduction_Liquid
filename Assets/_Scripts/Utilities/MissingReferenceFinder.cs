using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace _Scripts.Editor.Tools
{
    /// <summary>
    /// Editor tool to find GameObjects and assets with missing script or serialized field references.
    /// Helps identify broken prefabs, deleted scripts, and null references across the project.
    /// </summary>
    public class MissingReferenceFinder : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<MissingReferenceInfo> _missingReferences = new List<MissingReferenceInfo>();
        private bool _includeSceneObjects = true;
        private bool _includePrefabs = true;
        private bool _includeScriptableObjects = true;
        private bool _showOnlyMissingScripts = false;
        private string _searchFilter = "";
        private int _lastScanCount = 0;

        private class MissingReferenceInfo
        {
            public Object targetObject;
            public string objectPath;
            public string issueType; // "Missing Script" or "Missing Reference"
            public string propertyPath;
            public Component component;

            public MissingReferenceInfo(Object obj, string path, string type, string propPath = "", Component comp = null)
            {
                targetObject = obj;
                objectPath = path;
                issueType = type;
                propertyPath = propPath;
                component = comp;
            }
        }

        [MenuItem("Tools/LIQUID/Missing Reference Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<MissingReferenceFinder>("Missing References");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Missing Reference Finder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans for missing scripts and null serialized field references.\n" +
                "• Click 'Scan' to find all issues\n" +
                "• Click results to select objects\n" +
                "• Use filters to narrow results",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // === SCAN OPTIONS ===
            EditorGUILayout.LabelField("Scan Options", EditorStyles.boldLabel);
            
            _includeSceneObjects = EditorGUILayout.Toggle("Scan Scene Objects", _includeSceneObjects);
            _includePrefabs = EditorGUILayout.Toggle("Scan Prefabs", _includePrefabs);
            _includeScriptableObjects = EditorGUILayout.Toggle("Scan ScriptableObjects", _includeScriptableObjects);
            _showOnlyMissingScripts = EditorGUILayout.Toggle("Show Only Missing Scripts", _showOnlyMissingScripts);

            EditorGUILayout.Space(5);

            // === SCAN BUTTON ===
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Scan for Missing References", GUILayout.Height(40)))
            {
                ScanForMissingReferences();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // === RESULTS HEADER ===
            if (_missingReferences.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Results ({_missingReferences.Count} issues found)", EditorStyles.boldLabel);
                
                // Search filter
                GUILayout.FlexibleSpace();
                GUILayout.Label("Filter:", GUILayout.Width(40));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Width(200));
                
                // Clear button
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    _missingReferences.Clear();
                    _searchFilter = "";
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // === RESULTS LIST ===
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                var filteredResults = _missingReferences;
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    filteredResults = _missingReferences
                        .Where(r => r.objectPath.ToLower().Contains(_searchFilter.ToLower()))
                        .ToList();
                }

                foreach (var missingRef in filteredResults)
                {
                    DrawMissingReferenceItem(missingRef);
                }

                EditorGUILayout.EndScrollView();
            }
            else if (_lastScanCount > 0)
            {
                EditorGUILayout.HelpBox("✓ No missing references found!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Click 'Scan' to search for missing references.", MessageType.None);
            }
        }

        private void DrawMissingReferenceItem(MissingReferenceInfo info)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Issue type color coding
            Color issueColor = info.issueType == "Missing Script" ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.8f, 0.4f);
            GUI.backgroundColor = issueColor;

            EditorGUILayout.BeginHorizontal();

            // Icon and issue type
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            string icon = info.issueType == "Missing Script" ? "✗" : "⚠";
            EditorGUILayout.LabelField($"{icon} {info.issueType}", labelStyle, GUILayout.Width(150));

            // Object path
            EditorGUILayout.LabelField(info.objectPath, EditorStyles.label);

            // Select button
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Select", GUILayout.Width(70)))
            {
                if (info.targetObject != null)
                {
                    Selection.activeObject = info.targetObject;
                    EditorGUIUtility.PingObject(info.targetObject);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Property path for missing serialized fields
            if (!string.IsNullOrEmpty(info.propertyPath) && info.issueType != "Missing Script")
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Property: {info.propertyPath}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            // Component info for missing scripts
            if (info.component != null && info.issueType == "Missing Script")
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Component: {info.component.GetType().Name}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void ScanForMissingReferences()
        {
            _missingReferences.Clear();
            _lastScanCount = 0;

            EditorUtility.DisplayProgressBar("Scanning", "Searching for missing references...", 0f);

            try
            {
                if (_includeSceneObjects)
                {
                    ScanSceneObjects();
                }

                if (_includePrefabs)
                {
                    ScanPrefabs();
                }

                if (_includeScriptableObjects)
                {
                    ScanScriptableObjects();
                }

                _lastScanCount = _missingReferences.Count;

                Debug.Log($"[MissingReferenceFinder] Scan complete. Found {_missingReferences.Count} missing references.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ScanSceneObjects()
        {
            var allObjects = FindObjectsOfType<GameObject>(true); // Include inactive
            int total = allObjects.Length;

            for (int i = 0; i < total; i++)
            {
                if (i % 10 == 0)
                {
                    EditorUtility.DisplayProgressBar("Scanning Scene Objects", 
                        $"Checking {i}/{total}...", (float)i / total);
                }

                CheckGameObjectForMissingReferences(allObjects[i], GetGameObjectPath(allObjects[i]));
            }
        }

        private void ScanPrefabs()
        {
            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
            int total = prefabGUIDs.Length;

            for (int i = 0; i < total; i++)
            {
                if (i % 10 == 0)
                {
                    EditorUtility.DisplayProgressBar("Scanning Prefabs", 
                        $"Checking {i}/{total}...", (float)i / total);
                }

                string path = AssetDatabase.GUIDToAssetPath(prefabGUIDs[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    CheckGameObjectForMissingReferences(prefab, path);
                }
            }
        }

        private void ScanScriptableObjects()
        {
            string[] soGUIDs = AssetDatabase.FindAssets("t:ScriptableObject");
            int total = soGUIDs.Length;

            for (int i = 0; i < total; i++)
            {
                if (i % 10 == 0)
                {
                    EditorUtility.DisplayProgressBar("Scanning ScriptableObjects", 
                        $"Checking {i}/{total}...", (float)i / total);
                }

                string path = AssetDatabase.GUIDToAssetPath(soGUIDs[i]);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (so != null)
                {
                    CheckScriptableObjectForMissingReferences(so, path);
                }
            }
        }

        private void CheckGameObjectForMissingReferences(GameObject obj, string path)
        {
            // Check for missing scripts
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                {
                    _missingReferences.Add(new MissingReferenceInfo(
                        obj, 
                        path, 
                        "Missing Script"
                    ));
                }
                else if (!_showOnlyMissingScripts)
                {
                    // Check serialized fields for null references
                    CheckComponentForNullReferences(component, obj, path);
                }
            }

            // Recursively check children
            foreach (Transform child in obj.transform)
            {
                CheckGameObjectForMissingReferences(child.gameObject, path + "/" + child.name);
            }
        }

        private void CheckComponentForNullReferences(Component component, GameObject obj, string path)
        {
            SerializedObject so = new SerializedObject(component);
            SerializedProperty property = so.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (property.objectReferenceValue == null && 
                        property.objectReferenceInstanceIDValue != 0)
                    {
                        _missingReferences.Add(new MissingReferenceInfo(
                            obj,
                            path,
                            "Missing Reference",
                            $"{component.GetType().Name}.{property.propertyPath}",
                            component
                        ));
                    }
                }
            }
        }

        private void CheckScriptableObjectForMissingReferences(ScriptableObject so, string path)
        {
            SerializedObject serializedSO = new SerializedObject(so);
            SerializedProperty property = serializedSO.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (property.objectReferenceValue == null && 
                        property.objectReferenceInstanceIDValue != 0)
                    {
                        _missingReferences.Add(new MissingReferenceInfo(
                            so,
                            path,
                            "Missing Reference",
                            property.propertyPath
                        ));
                    }
                }
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}