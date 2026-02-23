#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

namespace _Scripts.Editor.Tools
{
    /// <summary>
    /// Post-migration utilities for fixing common URP conversion issues.
    /// Access via Tools > LIQUID > URP Post-Migration Fixes
    /// </summary>
    public class URPPostMigrationFixes : EditorWindow
    {
        private Vector2 _scrollPosition;
        
        // Fix options
        private bool _fixMissingShaders = true;
        private bool _fixLightIntensities = true;
        private bool _cleanMissingScripts = true;
        private bool _regenerateLightmaps = false;
        
        // Light conversion settings
        private float _directionalLightMultiplier = 0.1f;
        private float _pointLightMultiplier = 0.01f;
        private float _spotLightMultiplier = 0.01f;

        [MenuItem("Tools/LIQUID/URP Post-Migration Fixes")]
        public static void ShowWindow()
        {
            var window = GetWindow<URPPostMigrationFixes>("URP Fixes");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawMissingShaderFixes();
            EditorGUILayout.Space(10);
            
            DrawLightIntensityFixes();
            EditorGUILayout.Space(10);
            
            DrawMissingScriptCleanup();
            EditorGUILayout.Space(10);
            
            DrawMaterialDiagnostics();
            EditorGUILayout.Space(10);
            
            DrawQuickActions();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("URP Post-Migration Fixes", titleStyle);
            
            EditorGUILayout.HelpBox(
                "Use these tools to fix common issues after migrating from HDRP to URP.",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawMissingShaderFixes()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Missing/Pink Shader Fixes", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Finds materials using missing shaders (pink/magenta) and assigns URP Lit.",
                MessageType.None);
            
            if (GUILayout.Button("Find Materials with Missing Shaders", GUILayout.Height(30)))
            {
                FindMaterialsWithMissingShaders();
            }
            
            if (GUILayout.Button("Fix All Pink Materials → URP Lit", GUILayout.Height(30)))
            {
                FixMissingShaderMaterials();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void FindMaterialsWithMissingShaders()
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            List<Material> pinkMaterials = new List<Material>();
            
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader"))
                {
                    pinkMaterials.Add(mat);
                    Debug.Log($"[URP Fix] Missing shader material: {path}");
                }
            }
            
            if (pinkMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("Shader Check", "No materials with missing shaders found.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Shader Check", 
                    $"Found {pinkMaterials.Count} materials with missing shaders.\n\nCheck console for details.",
                    "OK");
            }
        }

        private void FixMissingShaderMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog("Error", "URP Lit shader not found. Is URP installed?", "OK");
                return;
            }
            
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            int fixedCount = 0;
            
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader"))
                {
                    mat.shader = urpLit;
                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                    Debug.Log($"[URP Fix] Fixed material: {path}");
                }
            }
            
            AssetDatabase.SaveAssets();
            
            EditorUtility.DisplayDialog("Fix Complete", 
                $"Fixed {fixedCount} materials with missing shaders.",
                "OK");
        }

        private void DrawLightIntensityFixes()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Light Intensity Conversion", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "HDRP uses physical light units (Lux/Lumen). URP uses arbitrary units.\n" +
                "These multipliers help approximate the conversion.",
                MessageType.None);
            
            _directionalLightMultiplier = EditorGUILayout.Slider("Directional Multiplier", _directionalLightMultiplier, 0.001f, 1f);
            _pointLightMultiplier = EditorGUILayout.Slider("Point Light Multiplier", _pointLightMultiplier, 0.001f, 0.1f);
            _spotLightMultiplier = EditorGUILayout.Slider("Spot Light Multiplier", _spotLightMultiplier, 0.001f, 0.1f);
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Convert Lights in Current Scene", GUILayout.Height(30)))
            {
                ConvertSceneLightIntensities();
            }
            
            EditorGUILayout.HelpBox(
                "Typical HDRP → URP intensity conversions:\n" +
                "• Directional: ~100,000 lux → ~1-2 intensity\n" +
                "• Point: ~10,000 lumen → ~100-200 intensity\n" +
                "• Spot: ~10,000 lumen → ~100-200 intensity",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }

        private void ConvertSceneLightIntensities()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            int converted = 0;
            
            foreach (Light light in lights)
            {
                float originalIntensity = light.intensity;
                
                switch (light.type)
                {
                    case LightType.Directional:
                        // HDRP directional uses Lux, typically 10,000-130,000
                        if (originalIntensity > 100)
                        {
                            light.intensity = originalIntensity * _directionalLightMultiplier;
                            converted++;
                        }
                        break;
                        
                    case LightType.Point:
                        // HDRP point uses Lumen, typically thousands
                        if (originalIntensity > 500)
                        {
                            light.intensity = originalIntensity * _pointLightMultiplier;
                            converted++;
                        }
                        break;
                        
                    case LightType.Spot:
                        // HDRP spot uses Lumen
                        if (originalIntensity > 500)
                        {
                            light.intensity = originalIntensity * _spotLightMultiplier;
                            converted++;
                        }
                        break;
                }
                
                if (originalIntensity != light.intensity)
                {
                    Debug.Log($"[URP Fix] Light '{light.name}': {originalIntensity} → {light.intensity}");
                    EditorUtility.SetDirty(light);
                }
            }
            
            if (converted > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            
            EditorUtility.DisplayDialog("Light Conversion", 
                $"Converted {converted} lights with high intensity values.\n\nSave the scene to preserve changes.",
                "OK");
        }

        private void DrawMissingScriptCleanup()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Missing Script Cleanup", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "After removing HDRP package, HD Additional Data components become missing scripts.\n" +
                "Use this to clean them up from the current scene.",
                MessageType.None);
            
            if (GUILayout.Button("Clean Missing Scripts in Scene", GUILayout.Height(30)))
            {
                CleanMissingScriptsInScene();
            }
            
            if (GUILayout.Button("Clean Missing Scripts in Selected Prefabs", GUILayout.Height(30)))
            {
                CleanMissingScriptsInSelectedPrefabs();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CleanMissingScriptsInScene()
        {
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int cleanedCount = 0;
            
            foreach (GameObject go in allObjects)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    cleanedCount += removed;
                    Debug.Log($"[URP Fix] Removed {removed} missing scripts from: {go.name}");
                }
            }
            
            if (cleanedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            
            EditorUtility.DisplayDialog("Cleanup Complete", 
                $"Removed {cleanedCount} missing script components.\n\nSave the scene to preserve changes.",
                "OK");
        }

        private void CleanMissingScriptsInSelectedPrefabs()
        {
            Object[] selected = Selection.objects;
            int totalCleaned = 0;
            int prefabsProcessed = 0;
            
            foreach (Object obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".prefab")) continue;
                
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                GameObject instance = PrefabUtility.LoadPrefabContents(path);
                
                Transform[] allTransforms = instance.GetComponentsInChildren<Transform>(true);
                int cleanedInPrefab = 0;
                
                foreach (Transform t in allTransforms)
                {
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    cleanedInPrefab += removed;
                }
                
                if (cleanedInPrefab > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                    totalCleaned += cleanedInPrefab;
                    Debug.Log($"[URP Fix] Cleaned {cleanedInPrefab} missing scripts from: {path}");
                }
                
                PrefabUtility.UnloadPrefabContents(instance);
                prefabsProcessed++;
            }
            
            EditorUtility.DisplayDialog("Prefab Cleanup", 
                $"Processed {prefabsProcessed} prefabs.\nRemoved {totalCleaned} missing script components.",
                "OK");
        }

        private void DrawMaterialDiagnostics()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Material Diagnostics", EditorStyles.boldLabel);
            
            if (GUILayout.Button("List All Shader Types in Project", GUILayout.Height(25)))
            {
                ListAllShaderTypes();
            }
            
            if (GUILayout.Button("Find Non-URP Materials", GUILayout.Height(25)))
            {
                FindNonURPMaterials();
            }
            
            if (GUILayout.Button("Batch Convert Selected Materials to URP Lit", GUILayout.Height(25)))
            {
                BatchConvertSelectedToURPLit();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void ListAllShaderTypes()
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            Dictionary<string, int> shaderCounts = new Dictionary<string, int>();
            
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && mat.shader != null)
                {
                    string shaderName = mat.shader.name;
                    if (!shaderCounts.ContainsKey(shaderName))
                        shaderCounts[shaderName] = 0;
                    shaderCounts[shaderName]++;
                }
            }
            
            Debug.Log("=== Shader Usage in Project ===");
            foreach (var kvp in shaderCounts.OrderByDescending(x => x.Value))
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value} materials");
            }
            Debug.Log("================================");
            
            EditorUtility.DisplayDialog("Shader Types", 
                $"Found {shaderCounts.Count} unique shaders.\nCheck console for full list.",
                "OK");
        }

        private void FindNonURPMaterials()
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            List<string> nonURPMaterials = new List<string>();
            
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && mat.shader != null)
                {
                    string shaderName = mat.shader.name.ToLower();
                    
                    // Check if NOT a URP shader
                    if (!shaderName.Contains("universal") && 
                        !shaderName.Contains("urp") &&
                        !shaderName.Contains("ui/") &&
                        !shaderName.Contains("sprites/") &&
                        !shaderName.Contains("skybox/"))
                    {
                        nonURPMaterials.Add($"{path} ({mat.shader.name})");
                    }
                }
            }
            
            if (nonURPMaterials.Count > 0)
            {
                Debug.Log("=== Non-URP Materials ===");
                foreach (string mat in nonURPMaterials)
                {
                    Debug.Log($"  {mat}");
                }
                Debug.Log("=========================");
            }
            
            EditorUtility.DisplayDialog("Non-URP Materials", 
                $"Found {nonURPMaterials.Count} materials not using URP shaders.\nCheck console for details.",
                "OK");
        }

        private void BatchConvertSelectedToURPLit()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog("Error", "URP Lit shader not found.", "OK");
                return;
            }
            
            Object[] selected = Selection.objects;
            int converted = 0;
            
            foreach (Object obj in selected)
            {
                if (obj is Material mat)
                {
                    // Store properties
                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : 
                                     mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture mainTex = mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") :
                                     mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    
                    mat.shader = urpLit;
                    
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", baseColor);
                    if (mat.HasProperty("_BaseMap") && mainTex != null)
                        mat.SetTexture("_BaseMap", mainTex);
                    
                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }
            
            AssetDatabase.SaveAssets();
            
            EditorUtility.DisplayDialog("Conversion Complete", 
                $"Converted {converted} materials to URP Lit.",
                "OK");
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Graphics Settings", GUILayout.Height(30)))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }
            
            if (GUILayout.Button("Quality Settings", GUILayout.Height(30)))
            {
                SettingsService.OpenProjectSettings("Project/Quality");
            }
            
            if (GUILayout.Button("Package Manager", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("Window/Package Manager");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Create New URP Volume Profile", GUILayout.Height(25)))
            {
                CreateURPVolumeProfile();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CreateURPVolumeProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create URP Volume Profile",
                "URP_VolumeProfile",
                "asset",
                "Choose location for Volume Profile");
            
            if (string.IsNullOrEmpty(path)) return;
            
            // Create a new Volume Profile
            var volumeProfile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(volumeProfile, path);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = volumeProfile;
            EditorGUIUtility.PingObject(volumeProfile);
            
            EditorUtility.DisplayDialog("Volume Profile Created",
                "New URP Volume Profile created.\n\n" +
                "Add it to a Volume component in your scene and add overrides for:\n" +
                "• Bloom\n" +
                "• Color Adjustments\n" +
                "• Vignette\n" +
                "• Depth of Field\n" +
                "• etc.",
                "OK");
        }
    }
}
#endif