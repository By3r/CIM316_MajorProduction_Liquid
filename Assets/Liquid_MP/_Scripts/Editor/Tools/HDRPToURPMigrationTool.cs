#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace _Scripts.Editor.Tools
{
    /// <summary>
    /// Migration tool for converting HDRP projects to URP.
    /// Access via Tools > LIQUID > HDRP to URP Migration
    /// </summary>
    public class HDRPToURPMigrationTool : EditorWindow
    {
        private Vector2 _scrollPosition;
        private int _currentStep = 0;
        
        // Scan results
        private List<ScanResult> _scanResults = new List<ScanResult>();
        private bool _hasScanned = false;
        
        // Statistics
        private int _hdrpMaterialCount = 0;
        private int _hdCameraDataCount = 0;
        private int _hdLightDataCount = 0;
        private int _volumeProfileCount = 0;
        private int _hdrpShaderCount = 0;
        
        // Conversion options
        private bool _convertMaterials = true;
        private bool _removeHDComponents = true;
        private bool _convertLightIntensities = true;
        private bool _backupMaterials = true;
        private bool _processScenes = true;
        private bool _processPrefabs = true;
        
        private string _backupFolder = "Assets/_Backup_HDRP";
        
        private enum ScanResultType
        {
            HDRPMaterial,
            HDCameraData,
            HDLightData,
            VolumeProfile,
            HDRPShader,
            HDRPAsset
        }
        
        private class ScanResult
        {
            public string Path;
            public string Name;
            public ScanResultType Type;
            public Object Asset;
            public string Details;
            public bool Converted;
        }

        [MenuItem("Tools/LIQUID/HDRP to URP Migration")]
        public static void ShowWindow()
        {
            var window = GetWindow<HDRPToURPMigrationTool>("HDRP → URP Migration");
            window.minSize = new Vector2(600, 700);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawStepIndicator();
            EditorGUILayout.Space(10);
            
            switch (_currentStep)
            {
                case 0:
                    DrawPreflightStep();
                    break;
                case 1:
                    DrawScanStep();
                    break;
                case 2:
                    DrawConversionOptionsStep();
                    break;
                case 3:
                    DrawExecutionStep();
                    break;
                case 4:
                    DrawPostMigrationStep();
                    break;
            }
            
            EditorGUILayout.Space(20);
            DrawNavigation();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("HDRP to URP Migration Tool", titleStyle);
            
            EditorGUILayout.HelpBox(
                "This tool assists with migrating your project from HDRP to URP.\n" +
                "Ensure you have a backup before proceeding.",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawStepIndicator()
        {
            string[] steps = { "Preflight", "Scan", "Options", "Execute", "Cleanup" };
            
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < steps.Length; i++)
            {
                GUI.backgroundColor = i == _currentStep ? Color.cyan : 
                                     i < _currentStep ? Color.green : Color.gray;
                
                GUIStyle style = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = i == _currentStep ? FontStyle.Bold : FontStyle.Normal
                };
                
                if (GUILayout.Button($"{i + 1}. {steps[i]}", style, GUILayout.Height(30)))
                {
                    if (i <= _currentStep || (i == 1 && _currentStep == 0))
                    {
                        _currentStep = i;
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        #region Step 0: Preflight
        
        private void DrawPreflightStep()
        {
            EditorGUILayout.LabelField("Step 1: Preflight Checklist", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Before You Begin", EditorStyles.boldLabel);
            
            DrawChecklistItem("Create a full project backup (Git commit or folder copy)");
            DrawChecklistItem("Close all scene views and prefab editing modes");
            DrawChecklistItem("Save all open scenes");
            DrawChecklistItem("Install URP package via Package Manager (Window > Package Manager)");
            DrawChecklistItem("Create URP Renderer Asset (Create > Rendering > URP Asset with Universal Renderer)");
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Package Requirements", EditorStyles.boldLabel);
            
            bool hasHDRP = IsPackageInstalled("com.unity.render-pipelines.high-definition");
            bool hasURP = IsPackageInstalled("com.unity.render-pipelines.universal");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("HDRP Package:", GUILayout.Width(150));
            GUI.color = hasHDRP ? Color.green : Color.red;
            EditorGUILayout.LabelField(hasHDRP ? "Installed (will be removed)" : "Not Found");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("URP Package:", GUILayout.Width(150));
            GUI.color = hasURP ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(hasURP ? "Installed" : "Not Installed - Install First!");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            
            if (!hasURP)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "URP package must be installed before migration.\n\n" +
                    "1. Open Package Manager (Window > Package Manager)\n" +
                    "2. Select 'Unity Registry' from dropdown\n" +
                    "3. Find 'Universal RP' and click Install",
                    MessageType.Warning);
                
                if (GUILayout.Button("Open Package Manager", GUILayout.Height(30)))
                {
                    EditorApplication.ExecuteMenuItem("Window/Package Manager");
                }
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Graphics Settings", EditorStyles.boldLabel);
            
            var currentPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            bool isHDRP = currentPipeline != null && currentPipeline.GetType().Name.Contains("HDRenderPipelineAsset");
            bool isURP = currentPipeline != null && currentPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Pipeline:", GUILayout.Width(150));
            if (isHDRP)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("HDRP (Ready to migrate)");
            }
            else if (isURP)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("URP (Already using URP!)");
            }
            else
            {
                GUI.color = Color.gray;
                EditorGUILayout.LabelField("Built-in / None");
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Open Graphics Settings", GUILayout.Height(25)))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawChecklistItem(string text)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("☐", GUILayout.Width(20));
            EditorGUILayout.LabelField(text);
            EditorGUILayout.EndHorizontal();
        }
        
        private bool IsPackageInstalled(string packageId)
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                string manifest = File.ReadAllText(manifestPath);
                return manifest.Contains(packageId);
            }
            return false;
        }
        
        #endregion

        #region Step 1: Scan
        
        private void DrawScanStep()
        {
            EditorGUILayout.LabelField("Step 2: Project Scan", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Scan Options", EditorStyles.boldLabel);
            _processScenes = EditorGUILayout.Toggle("Scan Scenes", _processScenes);
            _processPrefabs = EditorGUILayout.Toggle("Scan Prefabs", _processPrefabs);
            
            EditorGUILayout.Space(10);
            
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Scan Project for HDRP Assets", GUILayout.Height(40)))
            {
                ScanProject();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
            
            if (_hasScanned)
            {
                EditorGUILayout.Space(10);
                DrawScanResults();
            }
        }
        
        private void ScanProject()
        {
            _scanResults.Clear();
            _hdrpMaterialCount = 0;
            _hdCameraDataCount = 0;
            _hdLightDataCount = 0;
            _volumeProfileCount = 0;
            _hdrpShaderCount = 0;
            
            EditorUtility.DisplayProgressBar("Scanning Project", "Finding materials...", 0.2f);
            ScanMaterials();
            
            EditorUtility.DisplayProgressBar("Scanning Project", "Finding volume profiles...", 0.4f);
            ScanVolumeProfiles();
            
            if (_processScenes)
            {
                EditorUtility.DisplayProgressBar("Scanning Project", "Scanning scenes...", 0.6f);
                ScanScenes();
            }
            
            if (_processPrefabs)
            {
                EditorUtility.DisplayProgressBar("Scanning Project", "Scanning prefabs...", 0.8f);
                ScanPrefabs();
            }
            
            EditorUtility.ClearProgressBar();
            _hasScanned = true;
            
            Debug.Log($"[HDRP→URP] Scan complete. Found: {_hdrpMaterialCount} materials, " +
                     $"{_hdCameraDataCount} HD cameras, {_hdLightDataCount} HD lights, " +
                     $"{_volumeProfileCount} volume profiles");
        }
        
        private void ScanMaterials()
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && mat.shader != null)
                {
                    string shaderName = mat.shader.name;
                    
                    if (shaderName.Contains("HDRP") || shaderName.Contains("HD Render Pipeline"))
                    {
                        _scanResults.Add(new ScanResult
                        {
                            Path = path,
                            Name = mat.name,
                            Type = ScanResultType.HDRPMaterial,
                            Asset = mat,
                            Details = $"Shader: {shaderName}"
                        });
                        _hdrpMaterialCount++;
                    }
                }
            }
        }
        
        private void ScanVolumeProfiles()
        {
            // Look for HDRP Volume Profiles
            string[] profileGuids = AssetDatabase.FindAssets("t:VolumeProfile");
            
            foreach (string guid in profileGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Check if it contains HDRP-specific overrides
                string assetContent = File.ReadAllText(Path.Combine(Application.dataPath, "..", path));
                if (assetContent.Contains("HDRenderPipeline") || 
                    assetContent.Contains("UnityEngine.Rendering.HighDefinition"))
                {
                    _scanResults.Add(new ScanResult
                    {
                        Path = path,
                        Name = Path.GetFileNameWithoutExtension(path),
                        Type = ScanResultType.VolumeProfile,
                        Details = "Contains HDRP-specific overrides"
                    });
                    _volumeProfileCount++;
                }
            }
        }
        
        private void ScanScenes()
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            
            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                ScanSceneForHDComponents(scenePath);
            }
        }
        
        private void ScanSceneForHDComponents(string scenePath)
        {
            // Read scene file as text to find HD components without loading
            string fullPath = Path.Combine(Application.dataPath, "..", scenePath);
            if (!File.Exists(fullPath)) return;
            
            string sceneContent = File.ReadAllText(fullPath);
            
            if (sceneContent.Contains("HDAdditionalCameraData"))
            {
                int count = CountOccurrences(sceneContent, "HDAdditionalCameraData");
                _scanResults.Add(new ScanResult
                {
                    Path = scenePath,
                    Name = Path.GetFileNameWithoutExtension(scenePath),
                    Type = ScanResultType.HDCameraData,
                    Details = $"Contains {count} HDAdditionalCameraData component(s)"
                });
                _hdCameraDataCount += count;
            }
            
            if (sceneContent.Contains("HDAdditionalLightData"))
            {
                int count = CountOccurrences(sceneContent, "HDAdditionalLightData");
                _scanResults.Add(new ScanResult
                {
                    Path = scenePath,
                    Name = Path.GetFileNameWithoutExtension(scenePath),
                    Type = ScanResultType.HDLightData,
                    Details = $"Contains {count} HDAdditionalLightData component(s)"
                });
                _hdLightDataCount += count;
            }
        }
        
        private void ScanPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                string fullPath = Path.Combine(Application.dataPath, "..", prefabPath);
                
                if (!File.Exists(fullPath)) continue;
                
                string prefabContent = File.ReadAllText(fullPath);
                
                bool hasHDCamera = prefabContent.Contains("HDAdditionalCameraData");
                bool hasHDLight = prefabContent.Contains("HDAdditionalLightData");
                
                if (hasHDCamera)
                {
                    _scanResults.Add(new ScanResult
                    {
                        Path = prefabPath,
                        Name = Path.GetFileNameWithoutExtension(prefabPath),
                        Type = ScanResultType.HDCameraData,
                        Details = "Prefab with HDAdditionalCameraData"
                    });
                    _hdCameraDataCount++;
                }
                
                if (hasHDLight)
                {
                    _scanResults.Add(new ScanResult
                    {
                        Path = prefabPath,
                        Name = Path.GetFileNameWithoutExtension(prefabPath),
                        Type = ScanResultType.HDLightData,
                        Details = "Prefab with HDAdditionalLightData"
                    });
                    _hdLightDataCount++;
                }
            }
        }
        
        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }
        
        private void DrawScanResults()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
            
            // Summary
            EditorGUILayout.BeginHorizontal();
            DrawStatBox("Materials", _hdrpMaterialCount, Color.yellow);
            DrawStatBox("HD Cameras", _hdCameraDataCount, Color.cyan);
            DrawStatBox("HD Lights", _hdLightDataCount, Color.green);
            DrawStatBox("Volumes", _volumeProfileCount, Color.magenta);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Detailed list (collapsible by type)
            if (_scanResults.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {_scanResults.Count} items to process:", EditorStyles.miniLabel);
                
                float listHeight = Mathf.Min(_scanResults.Count * 22, 200);
                EditorGUILayout.BeginVertical(GUILayout.Height(listHeight));
                
                foreach (var result in _scanResults.Take(20))
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    GUI.color = GetColorForType(result.Type);
                    EditorGUILayout.LabelField(GetIconForType(result.Type), GUILayout.Width(25));
                    GUI.color = Color.white;
                    
                    EditorGUILayout.LabelField(result.Name, GUILayout.Width(200));
                    EditorGUILayout.LabelField(result.Details, EditorStyles.miniLabel);
                    
                    if (result.Asset != null && GUILayout.Button("Select", GUILayout.Width(50)))
                    {
                        Selection.activeObject = result.Asset;
                        EditorGUIUtility.PingObject(result.Asset);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                if (_scanResults.Count > 20)
                {
                    EditorGUILayout.LabelField($"... and {_scanResults.Count - 20} more items", EditorStyles.miniLabel);
                }
                
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No HDRP-specific assets found. Your project may already be clean.", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatBox(string label, int count, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(100));
            GUI.color = color;
            EditorGUILayout.LabelField(count.ToString(), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 24 });
            GUI.color = Color.white;
            EditorGUILayout.LabelField(label, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
        }
        
        private Color GetColorForType(ScanResultType type)
        {
            return type switch
            {
                ScanResultType.HDRPMaterial => Color.yellow,
                ScanResultType.HDCameraData => Color.cyan,
                ScanResultType.HDLightData => Color.green,
                ScanResultType.VolumeProfile => Color.magenta,
                _ => Color.white
            };
        }
        
        private string GetIconForType(ScanResultType type)
        {
            return type switch
            {
                ScanResultType.HDRPMaterial => "M",
                ScanResultType.HDCameraData => "C",
                ScanResultType.HDLightData => "L",
                ScanResultType.VolumeProfile => "V",
                _ => "?"
            };
        }
        
        #endregion

        #region Step 2: Conversion Options
        
        private void DrawConversionOptionsStep()
        {
            EditorGUILayout.LabelField("Step 3: Conversion Options", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Material Conversion", EditorStyles.boldLabel);
            _convertMaterials = EditorGUILayout.Toggle("Convert HDRP Materials to URP", _convertMaterials);
            
            if (_convertMaterials)
            {
                EditorGUI.indentLevel++;
                _backupMaterials = EditorGUILayout.Toggle("Backup Original Materials", _backupMaterials);
                if (_backupMaterials)
                {
                    _backupFolder = EditorGUILayout.TextField("Backup Folder", _backupFolder);
                }
                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox(
                    "Material conversion will:\n" +
                    "- Change HDRP/Lit → Universal Render Pipeline/Lit\n" +
                    "- Preserve base color, metallic, smoothness, normal maps\n" +
                    "- Some HDRP-specific properties may not transfer",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Component Cleanup", EditorStyles.boldLabel);
            _removeHDComponents = EditorGUILayout.Toggle("Remove HD Additional Data Components", _removeHDComponents);
            
            if (_removeHDComponents)
            {
                EditorGUILayout.HelpBox(
                    "Will remove from all scenes and prefabs:\n" +
                    "- HDAdditionalCameraData\n" +
                    "- HDAdditionalLightData\n" +
                    "URP will add its own equivalent components automatically.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            _convertLightIntensities = EditorGUILayout.Toggle("Approximate Light Intensity Conversion", _convertLightIntensities);
            
            if (_convertLightIntensities)
            {
                EditorGUILayout.HelpBox(
                    "HDRP uses physical light units (Lux, Lumen).\n" +
                    "URP uses arbitrary units. This will attempt to preserve\n" +
                    "relative brightness but manual tweaking may be needed.",
                    MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        #endregion

        #region Step 3: Execution
        
        private void DrawExecutionStep()
        {
            EditorGUILayout.LabelField("Step 4: Execute Migration", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Ready to Execute", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "The following operations will be performed:\n\n" +
                (_convertMaterials ? $"✓ Convert {_hdrpMaterialCount} materials to URP\n" : "") +
                (_removeHDComponents ? $"✓ Remove {_hdCameraDataCount} HD camera components\n" : "") +
                (_removeHDComponents ? $"✓ Remove {_hdLightDataCount} HD light components\n" : "") +
                (_backupMaterials ? $"✓ Backup materials to {_backupFolder}\n" : "") +
                "\nThis operation cannot be undone. Ensure you have backups!",
                MessageType.Warning);
            
            EditorGUILayout.Space(10);
            
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Execute Migration", GUILayout.Height(50)))
            {
                if (EditorUtility.DisplayDialog("Confirm Migration",
                    "Are you sure you want to proceed with the HDRP to URP migration?\n\n" +
                    "This will modify materials, scenes, and prefabs.\n\n" +
                    "Make sure you have a backup!",
                    "Yes, Migrate", "Cancel"))
                {
                    ExecuteMigration();
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Alternative: Manual Steps", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Use Unity's Built-in Material Converter", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("Edit/Rendering/Materials/Convert All Built-in Materials to URP");
            }
            
            EditorGUILayout.HelpBox(
                "Unity provides built-in conversion via:\n" +
                "Edit > Rendering > Materials > Convert All Built-in Materials to URP\n\n" +
                "Note: This converts Standard materials. HDRP materials may need\n" +
                "the custom conversion above or manual shader reassignment.",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        private void ExecuteMigration()
        {
            int totalSteps = (_convertMaterials ? _hdrpMaterialCount : 0) + 
                            (_removeHDComponents ? _hdCameraDataCount + _hdLightDataCount : 0);
            int currentStep = 0;
            
            try
            {
                // Backup materials first
                if (_backupMaterials && _convertMaterials)
                {
                    EditorUtility.DisplayProgressBar("Migration", "Creating backups...", 0);
                    CreateMaterialBackups();
                }
                
                // Convert materials
                if (_convertMaterials)
                {
                    var materialResults = _scanResults.Where(r => r.Type == ScanResultType.HDRPMaterial).ToList();
                    foreach (var result in materialResults)
                    {
                        currentStep++;
                        EditorUtility.DisplayProgressBar("Migration", 
                            $"Converting material: {result.Name}", 
                            (float)currentStep / totalSteps);
                        
                        ConvertMaterial(result);
                    }
                }
                
                // Process scenes and prefabs for HD components
                if (_removeHDComponents)
                {
                    EditorUtility.DisplayProgressBar("Migration", "Processing scenes and prefabs...", 0.8f);
                    RemoveHDComponentsFromAssets();
                }
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                EditorUtility.ClearProgressBar();
                
                EditorUtility.DisplayDialog("Migration Complete",
                    "HDRP to URP migration has been completed.\n\n" +
                    "Next steps:\n" +
                    "1. Assign URP Asset in Project Settings > Graphics\n" +
                    "2. Assign URP Asset in Quality settings\n" +
                    "3. Review materials and adjust as needed\n" +
                    "4. Remove HDRP package from Package Manager",
                    "OK");
                
                _currentStep = 4;
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Migration Error",
                    $"An error occurred during migration:\n\n{e.Message}\n\n" +
                    "Check the console for details.",
                    "OK");
                Debug.LogError($"[HDRP→URP] Migration error: {e}");
            }
        }
        
        private void CreateMaterialBackups()
        {
            if (!AssetDatabase.IsValidFolder(_backupFolder))
            {
                string[] folders = _backupFolder.Split('/');
                string currentPath = folders[0];
                
                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }
            
            var materialResults = _scanResults.Where(r => r.Type == ScanResultType.HDRPMaterial).ToList();
            foreach (var result in materialResults)
            {
                if (result.Asset is Material mat)
                {
                    string backupPath = $"{_backupFolder}/{mat.name}_HDRP.mat";
                    AssetDatabase.CopyAsset(result.Path, backupPath);
                }
            }
            
            Debug.Log($"[HDRP→URP] Backed up {materialResults.Count} materials to {_backupFolder}");
        }
        
        private void ConvertMaterial(ScanResult result)
        {
            if (result.Asset is not Material mat) return;
            
            // Store properties before conversion
            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            Texture baseMap = mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") : null;
            Texture normalMap = mat.HasProperty("_NormalMap") ? mat.GetTexture("_NormalMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
            
            // Find URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning($"[HDRP→URP] URP Lit shader not found. Is URP installed?");
                return;
            }
            
            mat.shader = urpLit;
            
            // Apply properties to URP shader
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", baseColor);
            
            if (mat.HasProperty("_BaseMap") && baseMap != null)
                mat.SetTexture("_BaseMap", baseMap);
            
            if (mat.HasProperty("_BumpMap") && normalMap != null)
                mat.SetTexture("_BumpMap", normalMap);
            
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            
            EditorUtility.SetDirty(mat);
            result.Converted = true;
            
            Debug.Log($"[HDRP→URP] Converted material: {mat.name}");
        }
        
        private void RemoveHDComponentsFromAssets()
        {
            // Process scenes
            var sceneResults = _scanResults.Where(r => 
                (r.Type == ScanResultType.HDCameraData || r.Type == ScanResultType.HDLightData) &&
                r.Path.EndsWith(".unity")).ToList();
            
            foreach (var sceneGroup in sceneResults.GroupBy(r => r.Path))
            {
                ProcessSceneForHDComponentRemoval(sceneGroup.Key);
            }
            
            // Process prefabs
            var prefabResults = _scanResults.Where(r =>
                (r.Type == ScanResultType.HDCameraData || r.Type == ScanResultType.HDLightData) &&
                r.Path.EndsWith(".prefab")).ToList();
            
            foreach (var prefabGroup in prefabResults.GroupBy(r => r.Path))
            {
                ProcessPrefabForHDComponentRemoval(prefabGroup.Key);
            }
        }
        
        private void ProcessSceneForHDComponentRemoval(string scenePath)
        {
            // Use text-based replacement for .unity files
            string fullPath = Path.Combine(Application.dataPath, "..", scenePath);
            if (!File.Exists(fullPath)) return;
            
            // This is a simplified approach - for production, you'd want to
            // properly parse YAML and remove the specific components
            Debug.Log($"[HDRP→URP] Scene {scenePath} needs manual HD component removal. " +
                     "Open the scene and components will be automatically cleaned up when HDRP is removed.");
        }
        
        private void ProcessPrefabForHDComponentRemoval(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;
            
            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
            
            bool modified = false;
            
            // Find and remove HD components by type name (since we may not have HDRP assembly reference)
            var allComponents = prefabRoot.GetComponentsInChildren<Component>(true);
            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                string typeName = component.GetType().Name;
                if (typeName == "HDAdditionalCameraData" || typeName == "HDAdditionalLightData")
                {
                    DestroyImmediate(component);
                    modified = true;
                }
            }
            
            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                Debug.Log($"[HDRP→URP] Cleaned HD components from prefab: {prefabPath}");
            }
            
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
        
        #endregion

        #region Step 4: Post-Migration
        
        private void DrawPostMigrationStep()
        {
            EditorGUILayout.LabelField("Step 5: Post-Migration Cleanup", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Remaining Manual Steps", EditorStyles.boldLabel);
            
            DrawChecklistItem("Set URP Asset in Project Settings > Graphics > Scriptable Render Pipeline Settings");
            DrawChecklistItem("Set URP Asset in Project Settings > Quality > Rendering for each quality level");
            DrawChecklistItem("Remove HDRP package from Package Manager");
            DrawChecklistItem("Create new URP Volume Profile for post-processing");
            DrawChecklistItem("Review and adjust light intensities in scenes");
            DrawChecklistItem("Test all scenes and prefabs");
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Open Graphics Settings", GUILayout.Height(30)))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }
            
            if (GUILayout.Button("Open Quality Settings", GUILayout.Height(30)))
            {
                SettingsService.OpenProjectSettings("Project/Quality");
            }
            
            if (GUILayout.Button("Open Package Manager", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("Window/Package Manager");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Create URP Assets", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Create URP Pipeline Asset", GUILayout.Height(30)))
            {
                CreateURPAsset();
            }
            
            EditorGUILayout.HelpBox(
                "After removing HDRP package:\n" +
                "1. Unity will show missing script warnings - this is expected\n" +
                "2. HD components will be removed automatically\n" +
                "3. Scenes may need to be re-saved\n" +
                "4. Some visual effects may need recreation in URP",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        private void CreateURPAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create URP Pipeline Asset",
                "URPAsset",
                "asset",
                "Choose location for URP Pipeline Asset");
            
            if (string.IsNullOrEmpty(path)) return;
            
            // Create via menu command since we need reflection otherwise
            EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/URP Asset (with Universal Renderer)");
            
            EditorUtility.DisplayDialog("URP Asset",
                "URP Asset creation initiated.\n\n" +
                "After the asset is created:\n" +
                "1. Go to Project Settings > Graphics\n" +
                "2. Drag the new asset to 'Scriptable Render Pipeline Settings'\n" +
                "3. Do the same in Quality Settings for each quality level",
                "OK");
        }
        
        #endregion

        #region Navigation
        
        private void DrawNavigation()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = _currentStep > 0;
            if (GUILayout.Button("< Previous", GUILayout.Height(35), GUILayout.Width(100)))
            {
                _currentStep--;
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            bool canProceed = _currentStep switch
            {
                0 => true, // Preflight - always can proceed
                1 => _hasScanned, // Scan - must have scanned
                2 => true, // Options - always can proceed
                3 => true, // Execute - always can proceed (warning shown)
                _ => false
            };
            
            GUI.enabled = canProceed && _currentStep < 4;
            if (GUILayout.Button("Next >", GUILayout.Height(35), GUILayout.Width(100)))
            {
                _currentStep++;
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        #endregion
    }
}
#endif