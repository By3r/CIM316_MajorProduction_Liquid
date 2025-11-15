using UnityEngine;
using UnityEditor;
using _Scripts.ProceduralGeneration;

namespace _Scripts.ProceduralGeneration.Editor
{
    /// <summary>
    /// Diagnostic tool to check for room configuration issues.
    /// Use this to identify why certain rooms fail narrow-phase checks.
    /// </summary>
    public class RoomDiagnosticTool : EditorWindow
    {
        private GameObject _roomPrefab;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Room Diagnostic Tool")]
        public static void ShowWindow()
        {
            GetWindow<RoomDiagnosticTool>("Room Diagnostics");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Room Configuration Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use this tool to diagnose why a room prefab fails narrow-phase collision checks.\n" +
                "Select a room prefab to analyze its configuration.",
                MessageType.Info);

            EditorGUILayout.Space();

            // Room prefab selection
            _roomPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Room Prefab to Analyze",
                _roomPrefab,
                typeof(GameObject),
                false
            );

            if (_roomPrefab == null)
            {
                EditorGUILayout.HelpBox("Select a room prefab to analyze.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();

            // Analyze button
            if (GUILayout.Button("Analyze Room Configuration", GUILayout.Height(30)))
            {
                AnalyzeRoom();
            }

            EditorGUILayout.Space();

            // Auto-fix button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Auto-Fix Bounds (Calculate from Renderers)", GUILayout.Height(30)))
            {
                AutoFixBounds();
            }
            GUI.backgroundColor = Color.white;
        }

        private void AnalyzeRoom()
        {
            Debug.Log($"=== ANALYZING ROOM: {_roomPrefab.name} ===");

            // Check BoundsChecker
            BoundsChecker boundsChecker = _roomPrefab.GetComponent<BoundsChecker>();
            if (boundsChecker == null)
            {
                Debug.LogError($"❌ CRITICAL: Room '{_roomPrefab.name}' is missing BoundsChecker component!");
                return;
            }

            Debug.Log($"✓ BoundsChecker found");

            // Get bounds
            Bounds tightBounds = boundsChecker.GetBounds();
            Bounds paddedBounds = boundsChecker.GetPaddedBounds();
            Bounds collisionBounds = boundsChecker.GetCollisionBounds(allowSocketOverlap: true);

            Debug.Log($"Bounds Center: {tightBounds.center}");
            Debug.Log($"Bounds Size: {tightBounds.size}");
            Debug.Log($"Tight Bounds: {tightBounds}");
            Debug.Log($"Padded Bounds: {paddedBounds}");
            Debug.Log($"Collision Bounds: {collisionBounds}");

            // Check renderers
            Renderer[] renderers = _roomPrefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"⚠ Warning: Room '{_roomPrefab.name}' has no renderers!");
            }
            else
            {
                Debug.Log($"✓ Found {renderers.Length} renderers");

                // Calculate actual bounds from renderers
                Bounds actualBounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
                foreach (Renderer renderer in renderers)
                {
                    // Skip sockets
                    if (renderer.GetComponentInParent<ConnectionSocket>() != null)
                        continue;

                    actualBounds.Encapsulate(renderer.bounds);
                }

                Debug.Log($"Actual Geometry Bounds: {actualBounds}");

                // Check if BoundsChecker bounds match actual geometry
                Vector3 sizeDiff = actualBounds.size - tightBounds.size;
                if (sizeDiff.magnitude > 0.5f)
                {
                    Debug.LogWarning($"⚠ WARNING: BoundsChecker bounds don't match actual geometry!");
                    Debug.LogWarning($"   BoundsChecker Size: {tightBounds.size}");
                    Debug.LogWarning($"   Actual Geometry Size: {actualBounds.size}");
                    Debug.LogWarning($"   Difference: {sizeDiff}");
                    Debug.LogWarning($"   → Click 'Auto-Fix Bounds' to recalculate!");
                }
                else
                {
                    Debug.Log($"✓ BoundsChecker bounds match actual geometry");
                }
            }

            // Check sockets
            ConnectionSocket[] sockets = _roomPrefab.GetComponentsInChildren<ConnectionSocket>();
            if (sockets.Length == 0)
            {
                Debug.LogWarning($"⚠ Warning: Room '{_roomPrefab.name}' has no ConnectionSockets!");
            }
            else
            {
                Debug.Log($"✓ Found {sockets.Length} ConnectionSockets");

                // Check if sockets are within bounds
                bool allSocketsInside = true;
                foreach (ConnectionSocket socket in sockets)
                {
                    Vector3 localPos = _roomPrefab.transform.InverseTransformPoint(socket.Position);
                    bool isInside = tightBounds.Contains(_roomPrefab.transform.TransformPoint(localPos));

                    if (!isInside)
                    {
                        Debug.LogWarning($"⚠ Socket '{socket.name}' is OUTSIDE bounds!");
                        Debug.LogWarning($"   Socket position: {localPos}");
                        Debug.LogWarning($"   Bounds: min={tightBounds.min}, max={tightBounds.max}");
                        allSocketsInside = false;
                    }
                }

                if (allSocketsInside)
                {
                    Debug.Log($"✓ All sockets are within bounds");
                }
            }

            // Check scale
            Vector3 scale = _roomPrefab.transform.localScale;
            if (scale != Vector3.one)
            {
                if (scale.x != scale.y || scale.y != scale.z)
                {
                    Debug.LogWarning($"⚠ WARNING: Room has non-uniform scale: {scale}");
                    Debug.LogWarning($"   Non-uniform scale can cause collision detection issues!");
                }
                else
                {
                    Debug.Log($"ℹ Info: Room has uniform scale: {scale}");
                }
            }
            else
            {
                Debug.Log($"✓ Room has default scale (1, 1, 1)");
            }

            // Summary
            Debug.Log($"=== ANALYSIS COMPLETE ===");
        }

        private void AutoFixBounds()
        {
            BoundsChecker boundsChecker = _roomPrefab.GetComponent<BoundsChecker>();
            if (boundsChecker == null)
            {
                Debug.LogError($"Room '{_roomPrefab.name}' is missing BoundsChecker component!");
                return;
            }

            Undo.RecordObject(boundsChecker, "Auto-Fix Bounds");
            boundsChecker.CalculateBoundsFromRenderers();
            EditorUtility.SetDirty(boundsChecker);
            EditorUtility.SetDirty(_roomPrefab);

            Debug.Log($"✓ Auto-fixed bounds for '{_roomPrefab.name}'");
            Debug.Log($"   New bounds: Center={boundsChecker.GetBounds().center}, Size={boundsChecker.GetBounds().size}");

            // Analyze again to show new values
            AnalyzeRoom();
        }
    }
}