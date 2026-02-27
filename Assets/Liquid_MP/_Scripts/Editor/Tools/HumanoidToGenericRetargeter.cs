// Retargets Humanoid animation clips to Generic animation clips for a target skeleton.
//
// How it works:
//  1. Samples each Humanoid clip on a temporary source model
//  2. Reads the HumanPose (muscle values) via HumanPoseHandler
//  3. Applies those muscles to a temporary target model (must also be Humanoid rig)
//  4. Records the resulting local bone transforms on the target skeleton
//  5. Writes them as Generic animation curves using the target's bone path hierarchy
//
// The output clips play directly on a Generic rig that shares the same skeleton
// as the target Humanoid model.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Editor.Tools
{
    public class HumanoidToGenericRetargeter : EditorWindow
    {
        // ── Source ──
        [SerializeField] private GameObject _sourceModel;

        // ── Target ──
        [SerializeField] private GameObject _targetHumanoidModel;

        // ── Clips ──
        [SerializeField] private List<AnimationClip> _sourceClips = new List<AnimationClip>();

        // ── Output ──
        [SerializeField] private string _outputFolder = "Assets/Animations/RetargetedLocomotion";
        [SerializeField] private bool _lowerBodyOnly = true;

        private Vector2 _scrollPos;

        // Lower-body bones we care about for locomotion
        private static readonly HumanBodyBones[] LowerBodyBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes,
        };

        private static readonly HumanBodyBones[] AllBodyBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        };

        [MenuItem("Tools/LIQUID/Humanoid To Generic Retargeter")]
        private static void ShowWindow()
        {
            var window = GetWindow<HumanoidToGenericRetargeter>();
            window.titleContent = new GUIContent("Humanoid -> Generic");
            window.minSize = new Vector2(460, 620);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Humanoid -> Generic Retargeter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Converts Humanoid animation clips to Generic animation clips\n" +
                "that can play on a Generic rig.\n\n" +
                "Source Model: Any character FBX with a Humanoid avatar\n" +
                "Target Model: A duplicate of your Generic character's FBX set to Humanoid rig\n\n" +
                "The tool transfers muscle data via HumanPoseHandler, then records\n" +
                "the resulting bone transforms as Generic curves. Output clips are\n" +
                "prefixed with 'Generic_' and saved to the output folder.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Source
            EditorGUILayout.LabelField("Source Humanoid Character", EditorStyles.boldLabel);
            _sourceModel = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Source Model", "The character FBX that the Humanoid clips were made for"),
                _sourceModel, typeof(GameObject), false);

            EditorGUILayout.Space(5);

            // Target
            EditorGUILayout.LabelField("Target Humanoid Character", EditorStyles.boldLabel);
            _targetHumanoidModel = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Target Humanoid Model",
                    "A Humanoid-rigged duplicate of your Generic character's FBX"),
                _targetHumanoidModel, typeof(GameObject), false);

            EditorGUILayout.Space(5);

            // Options
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _lowerBodyOnly = EditorGUILayout.Toggle(
                new GUIContent("Lower Body Only", "Only retarget hips and leg bones (recommended for FPS)"),
                _lowerBodyOnly);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

            EditorGUILayout.Space(5);

            // Clips list
            EditorGUILayout.LabelField($"Clips to Retarget ({_sourceClips.Count})", EditorStyles.boldLabel);

            // Drag-drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Animation Clips or FBX Files Here");
            HandleDragAndDrop(dropArea);

            for (int i = _sourceClips.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                _sourceClips[i] = (AnimationClip)EditorGUILayout.ObjectField(
                    _sourceClips[i], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                    _sourceClips.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Clip Slot"))
                _sourceClips.Add(null);
            if (GUILayout.Button("Clear All"))
                _sourceClips.Clear();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Validation
            string validationError = Validate();
            if (!string.IsNullOrEmpty(validationError))
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            GUI.enabled = string.IsNullOrEmpty(validationError);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Retarget All Clips", GUILayout.Height(40)))
            {
                RetargetAllClips();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        private string Validate()
        {
            if (_sourceModel == null)
                return "Assign the Source Model with a Humanoid avatar.";

            if (_targetHumanoidModel == null)
                return "Assign the Target Humanoid Model.";

            // Check source has an Animator/Avatar
            var srcAnimator = _sourceModel.GetComponent<Animator>();
            if (srcAnimator == null || srcAnimator.avatar == null || !srcAnimator.avatar.isHuman)
                return "Source Model must have a Humanoid Avatar. Check the FBX rig settings.";

            var tgtAnimator = _targetHumanoidModel.GetComponent<Animator>();
            if (tgtAnimator == null || tgtAnimator.avatar == null || !tgtAnimator.avatar.isHuman)
                return "Target Model must have a Humanoid Avatar. Check the FBX rig settings.";

            int validClips = _sourceClips.Count(c => c != null);
            if (validClips == 0)
                return "Add at least one animation clip to retarget.";

            return null;
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    foreach (Object obj in DragAndDrop.objectReferences)
                    {
                        if (obj is AnimationClip clip)
                        {
                            if (!_sourceClips.Contains(clip))
                                _sourceClips.Add(clip);
                        }
                        else
                        {
                            // Extract clips from FBX or other asset
                            string path = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(path))
                            {
                                foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                                {
                                    if (subAsset is AnimationClip subClip &&
                                        !subClip.name.StartsWith("__preview__"))
                                    {
                                        if (!_sourceClips.Contains(subClip))
                                            _sourceClips.Add(subClip);
                                    }
                                }
                            }
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        private void RetargetAllClips()
        {
            // Ensure output folder exists
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
                AssetDatabase.Refresh();
            }

            // Instantiate temp models
            GameObject srcInstance = Instantiate(_sourceModel);
            srcInstance.name = "__RetargetSource__";
            srcInstance.hideFlags = HideFlags.HideAndDontSave;
            srcInstance.transform.position = Vector3.zero;

            GameObject tgtInstance = Instantiate(_targetHumanoidModel);
            tgtInstance.name = "__RetargetTarget__";
            tgtInstance.hideFlags = HideFlags.HideAndDontSave;
            tgtInstance.transform.position = Vector3.right * 20f;

            Animator srcAnimator = srcInstance.GetComponent<Animator>();
            Animator tgtAnimator = tgtInstance.GetComponent<Animator>();

            try
            {
                // Build the bone path map: for each target bone, find its path relative to
                // the Animator GameObject. Generic clips use these paths to address bones.
                Transform armatureRoot = FindArmatureRoot(tgtInstance.transform);
                if (armatureRoot == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "Could not find armature root (expected 'root' bone) in target model.", "OK");
                    return;
                }

                // Generic Animator binds paths relative to the GameObject with the Animator component.
                Transform animatorRoot = tgtInstance.transform;

                HumanBodyBones[] bonesToRetarget = _lowerBodyOnly ? LowerBodyBones : AllBodyBones;

                // Build path lookup
                var bonePathMap = new Dictionary<HumanBodyBones, string>();
                foreach (HumanBodyBones bone in bonesToRetarget)
                {
                    Transform tgtBone = tgtAnimator.GetBoneTransform(bone);
                    if (tgtBone == null)
                    {
                        Debug.LogWarning($"[Retargeter] Target missing bone: {bone} - skipping");
                        continue;
                    }

                    string path = BuildRelativePath(tgtBone, animatorRoot);
                    if (path == null)
                    {
                        Debug.LogWarning($"[Retargeter] Could not build path for {bone} ({tgtBone.name})");
                        continue;
                    }

                    bonePathMap[bone] = path;
                }

                Debug.Log($"[Retargeter] Mapped {bonePathMap.Count} bones. Paths:");
                foreach (var kvp in bonePathMap)
                    Debug.Log($"  {kvp.Key} -> {kvp.Value}");

                // Set up HumanPose handlers
                var srcPoseHandler = new HumanPoseHandler(srcAnimator.avatar, srcInstance.transform);
                var tgtPoseHandler = new HumanPoseHandler(tgtAnimator.avatar, tgtInstance.transform);
                var humanPose = new HumanPose();

                int processed = 0;
                int total = _sourceClips.Count(c => c != null);

                foreach (AnimationClip sourceClip in _sourceClips)
                {
                    if (sourceClip == null) continue;

                    processed++;
                    EditorUtility.DisplayProgressBar("Retargeting",
                        $"Processing {sourceClip.name} ({processed}/{total})", (float)processed / total);

                    RetargetClip(sourceClip, srcInstance, srcAnimator, tgtInstance, tgtAnimator,
                        srcPoseHandler, tgtPoseHandler, ref humanPose,
                        animatorRoot, bonePathMap, bonesToRetarget);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Retarget Complete",
                    $"Retargeted {processed} clip(s) to:\n{_outputFolder}\n\n" +
                    "Replace the Humanoid clips in your blend tree with the new Generic_ clips.",
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (srcInstance != null) DestroyImmediate(srcInstance);
                if (tgtInstance != null) DestroyImmediate(tgtInstance);
            }
        }

        private void RetargetClip(
            AnimationClip sourceClip,
            GameObject srcGo, Animator srcAnimator,
            GameObject tgtGo, Animator tgtAnimator,
            HumanPoseHandler srcPoseHandler, HumanPoseHandler tgtPoseHandler,
            ref HumanPose humanPose,
            Transform animatorRoot,
            Dictionary<HumanBodyBones, string> bonePathMap,
            HumanBodyBones[] bonesToRetarget)
        {
            float clipLength = sourceClip.length;
            float frameRate = sourceClip.frameRate > 0 ? sourceClip.frameRate : 30f;
            float frameTime = 1f / frameRate;
            int frameCount = Mathf.CeilToInt(clipLength * frameRate) + 1;

            // Prepare curve storage
            var boneCurves = new Dictionary<HumanBodyBones, BoneCurveSet>();
            foreach (HumanBodyBones bone in bonesToRetarget)
            {
                if (bonePathMap.ContainsKey(bone))
                    boneCurves[bone] = new BoneCurveSet();
            }

            // Cache rest pose of target so we can restore after sampling
            var restPose = CachePose(tgtGo.transform);

            // Sample every frame
            for (int frame = 0; frame < frameCount; frame++)
            {
                float time = Mathf.Min(frame * frameTime, clipLength);

                // 1. Sample the Humanoid clip on the source skeleton
                sourceClip.SampleAnimation(srcGo, time);

                // 2. Read HumanPose (muscle values) from source
                srcPoseHandler.GetHumanPose(ref humanPose);

                // 3. Apply same muscles to the target skeleton
                tgtPoseHandler.SetHumanPose(ref humanPose);

                // 4. Record the local bone transforms on the target
                foreach (HumanBodyBones bone in bonesToRetarget)
                {
                    if (!boneCurves.ContainsKey(bone)) continue;

                    Transform tgtBone = tgtAnimator.GetBoneTransform(bone);
                    if (tgtBone == null) continue;

                    BoneCurveSet curves = boneCurves[bone];

                    // Position (mainly relevant for Hips / root motion)
                    curves.posX.AddKey(new Keyframe(time, tgtBone.localPosition.x));
                    curves.posY.AddKey(new Keyframe(time, tgtBone.localPosition.y));
                    curves.posZ.AddKey(new Keyframe(time, tgtBone.localPosition.z));

                    // Rotation as quaternion
                    curves.rotX.AddKey(new Keyframe(time, tgtBone.localRotation.x));
                    curves.rotY.AddKey(new Keyframe(time, tgtBone.localRotation.y));
                    curves.rotZ.AddKey(new Keyframe(time, tgtBone.localRotation.z));
                    curves.rotW.AddKey(new Keyframe(time, tgtBone.localRotation.w));
                }
            }

            // Restore rest pose
            RestorePose(restPose);

            // Build output clip
            var outputClip = new AnimationClip();
            outputClip.legacy = false;
            outputClip.frameRate = frameRate;

            // Copy loop settings from source
            AnimationClipSettings srcSettings = AnimationUtility.GetAnimationClipSettings(sourceClip);
            AnimationClipSettings outSettings = AnimationUtility.GetAnimationClipSettings(outputClip);
            outSettings.loopTime = srcSettings.loopTime;
            outSettings.loopBlend = srcSettings.loopBlend;
            outSettings.loopBlendOrientation = srcSettings.loopBlendOrientation;
            outSettings.loopBlendPositionXZ = srcSettings.loopBlendPositionXZ;
            outSettings.loopBlendPositionY = srcSettings.loopBlendPositionY;
            AnimationUtility.SetAnimationClipSettings(outputClip, outSettings);

            // Write curves
            foreach (HumanBodyBones bone in bonesToRetarget)
            {
                if (!bonePathMap.ContainsKey(bone) || !boneCurves.ContainsKey(bone)) continue;

                string path = bonePathMap[bone];
                BoneCurveSet curves = boneCurves[bone];

                // Position curves for Hips (pelvis) — essential for root motion / hip sway
                if (bone == HumanBodyBones.Hips)
                {
                    outputClip.SetCurve(path, typeof(Transform), "localPosition.x", curves.posX);
                    outputClip.SetCurve(path, typeof(Transform), "localPosition.y", curves.posY);
                    outputClip.SetCurve(path, typeof(Transform), "localPosition.z", curves.posZ);
                }

                // Rotation curves for all bones
                outputClip.SetCurve(path, typeof(Transform), "localRotation.x", curves.rotX);
                outputClip.SetCurve(path, typeof(Transform), "localRotation.y", curves.rotY);
                outputClip.SetCurve(path, typeof(Transform), "localRotation.z", curves.rotZ);
                outputClip.SetCurve(path, typeof(Transform), "localRotation.w", curves.rotW);
            }

            outputClip.EnsureQuaternionContinuity();

            // Save
            string outputName = $"Generic_{sourceClip.name}";
            string outputPath = $"{_outputFolder}/{outputName}.anim";

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(outputClip, existing);
                Debug.Log($"[Retargeter] Updated: {outputPath}");
            }
            else
            {
                AssetDatabase.CreateAsset(outputClip, outputPath);
                Debug.Log($"[Retargeter] Created: {outputPath}");
            }
        }

        /// <summary>
        /// Finds the armature root bone (e.g. "root" or "Armature").
        /// </summary>
        private Transform FindArmatureRoot(Transform modelRoot)
        {
            // Look for a child named "root"
            Transform root = modelRoot.Find("root");
            if (root != null) return root;

            // Fallback: look for "Armature" or "Root"
            foreach (Transform child in modelRoot)
            {
                string lower = child.name.ToLower();
                if (lower == "root" || lower == "armature")
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Builds a path like "root/pelvis/thigh_l" relative to the Animator GameObject.
        /// </summary>
        private string BuildRelativePath(Transform bone, Transform animatorRoot)
        {
            if (bone == null || animatorRoot == null) return null;
            if (bone == animatorRoot) return "";

            var parts = new List<string>();
            Transform current = bone;

            while (current != null && current != animatorRoot)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (current != animatorRoot) return null; // bone is not under animatorRoot

            parts.Reverse();
            return string.Join("/", parts);
        }

        #region Pose Cache

        private struct BoneCache
        {
            public Transform bone;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        private List<BoneCache> CachePose(Transform root)
        {
            var cache = new List<BoneCache>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                cache.Add(new BoneCache
                {
                    bone = t,
                    localPosition = t.localPosition,
                    localRotation = t.localRotation,
                    localScale = t.localScale
                });
            }
            return cache;
        }

        private void RestorePose(List<BoneCache> cache)
        {
            foreach (BoneCache bc in cache)
            {
                if (bc.bone == null) continue;
                bc.bone.localPosition = bc.localPosition;
                bc.bone.localRotation = bc.localRotation;
                bc.bone.localScale = bc.localScale;
            }
        }

        #endregion

        private class BoneCurveSet
        {
            public AnimationCurve posX = new AnimationCurve();
            public AnimationCurve posY = new AnimationCurve();
            public AnimationCurve posZ = new AnimationCurve();
            public AnimationCurve rotX = new AnimationCurve();
            public AnimationCurve rotY = new AnimationCurve();
            public AnimationCurve rotZ = new AnimationCurve();
            public AnimationCurve rotW = new AnimationCurve();
        }
    }
}
