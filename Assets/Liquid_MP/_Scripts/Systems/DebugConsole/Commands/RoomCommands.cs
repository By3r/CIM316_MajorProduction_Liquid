using System.Collections.Generic;
using System.Text;
using _Scripts.Systems.ProceduralGeneration;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for room placement diagnostics (overlap detection, bounds, etc.).
    /// </summary>
    public static class RoomCommands
    {
        [DebugCommand("room diag", "Full room overlap diagnostic report. Copy output with 'copy' command.", "room diag")]
        public static string RoomDiag(string[] args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Room Overlap Diagnostics ===");

            // --- Registry Info ---
            if (OccupiedSpaceRegistry.Instance == null)
            {
                sb.AppendLine("<color=red>OccupiedSpaceRegistry not found.</color>");
                return sb.ToString();
            }

            List<OccupiedSpaceRegistry.OccupiedSpace> spaces = OccupiedSpaceRegistry.Instance.GetAllOccupiedSpaces();
            sb.AppendLine($"Registered rooms: {spaces.Count}");
            sb.AppendLine("");

            // --- Per-Room Info ---
            sb.AppendLine("--- Room Details ---");
            List<RoomInfo> validRooms = new List<RoomInfo>();

            for (int i = 0; i < spaces.Count; i++)
            {
                OccupiedSpaceRegistry.OccupiedSpace space = spaces[i];
                if (space.roomTransform == null)
                {
                    sb.AppendLine($"  [{i}] <color=red>NULL TRANSFORM</color> (name: {space.roomName})");
                    continue;
                }

                Vector3 pos = space.roomTransform.position;
                Vector3 rot = space.roomTransform.eulerAngles;
                Bounds aabb = space.paddedBoundsWorld;

                // Check if room is rotated (not axis-aligned)
                bool isRotated = !Mathf.Approximately(rot.y % 90f, 0f);
                string rotTag = isRotated ? " <color=yellow>[ROTATED NON-90]</color>" : "";

                // Get local bounds from BoundsChecker for comparison
                string localInfo = "";
                if (space.boundsChecker != null)
                {
                    Vector3 localSize = space.boundsChecker.LocalBoundsSize;
                    Vector3 aabbSize = aabb.size;

                    // Check if AABB size matches local size (if not, rotation might be poorly handled)
                    bool sizeMismatch = Mathf.Abs(localSize.x - aabbSize.x) > 0.5f ||
                                        Mathf.Abs(localSize.z - aabbSize.z) > 0.5f;
                    if (sizeMismatch && isRotated)
                    {
                        localInfo = $" <color=red>[AABB SIZE MISMATCH: local=({localSize.x:F1},{localSize.z:F1}) aabb=({aabbSize.x:F1},{aabbSize.z:F1})]</color>";
                    }
                }

                sb.AppendLine($"  [{i}] {space.roomName}{rotTag}{localInfo}");
                sb.AppendLine($"      pos=({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) rot=({rot.x:F0}, {rot.y:F0}, {rot.z:F0})");
                sb.AppendLine($"      AABB center=({aabb.center.x:F2}, {aabb.center.y:F2}, {aabb.center.z:F2}) size=({aabb.size.x:F2}, {aabb.size.y:F2}, {aabb.size.z:F2})");

                validRooms.Add(new RoomInfo { index = i, space = space, bounds = aabb });
            }

            sb.AppendLine("");

            // --- Pairwise Overlap Check ---
            sb.AppendLine("--- Overlap Pairs ---");
            int overlapCount = 0;

            for (int i = 0; i < validRooms.Count; i++)
            {
                for (int j = i + 1; j < validRooms.Count; j++)
                {
                    RoomInfo a = validRooms[i];
                    RoomInfo b = validRooms[j];

                    if (!a.bounds.Intersects(b.bounds)) continue;

                    // Calculate overlap volume
                    Vector3 overlapMin = Vector3.Max(a.bounds.min, b.bounds.min);
                    Vector3 overlapMax = Vector3.Min(a.bounds.max, b.bounds.max);
                    Vector3 overlapSize = overlapMax - overlapMin;

                    // Skip tiny overlaps (expected at door connections)
                    float overlapVolume = overlapSize.x * overlapSize.y * overlapSize.z;
                    float overlapXZ = overlapSize.x * overlapSize.z;

                    // Check if connected via door (adjacent rooms share a doorway)
                    float centerDist = Vector3.Distance(a.bounds.center, b.bounds.center);

                    string severity;
                    if (overlapXZ > 20f)
                        severity = "<color=red>[MAJOR OVERLAP]</color>";
                    else if (overlapXZ > 5f)
                        severity = "<color=yellow>[MODERATE OVERLAP]</color>";
                    else
                        severity = "<color=white>[minor — likely door connection]</color>";

                    sb.AppendLine($"  {severity} [{a.index}] {a.space.roomName} <-> [{b.index}] {b.space.roomName}");
                    sb.AppendLine($"      overlap XZ area={overlapXZ:F1}m2 volume={overlapVolume:F1}m3 size=({overlapSize.x:F2}, {overlapSize.y:F2}, {overlapSize.z:F2})");
                    sb.AppendLine($"      center dist={centerDist:F2}m");

                    overlapCount++;
                }
            }

            if (overlapCount == 0)
            {
                sb.AppendLine("  <color=green>No overlapping bounds detected.</color>");
            }
            else
            {
                sb.AppendLine($"  Total overlapping pairs: {overlapCount}");
            }

            sb.AppendLine("");

            // --- Rotation Analysis ---
            sb.AppendLine("--- Rotation Analysis ---");
            int rotatedCount = 0;
            int nonAxis90 = 0;

            foreach (RoomInfo room in validRooms)
            {
                float yRot = room.space.roomTransform.eulerAngles.y;
                if (!Mathf.Approximately(yRot, 0f)) rotatedCount++;
                if (!Mathf.Approximately(yRot % 90f, 0f)) nonAxis90++;
            }

            sb.AppendLine($"  Rooms with Y rotation: {rotatedCount}/{validRooms.Count}");
            sb.AppendLine($"  Rooms with non-90 rotation: {nonAxis90}/{validRooms.Count}");

            if (nonAxis90 > 0)
            {
                sb.AppendLine("  <color=yellow>WARNING: Non-90 degree rotations with AABB bounds can cause inaccurate overlap detection.</color>");
            }

            // --- AABB vs Rotated Bounds Check ---
            sb.AppendLine("");
            sb.AppendLine("--- AABB Accuracy Check ---");
            int inaccurateCount = 0;

            foreach (RoomInfo room in validRooms)
            {
                if (room.space.boundsChecker == null) continue;

                Vector3 localSize = room.space.boundsChecker.LocalBoundsSize;
                Vector3 localCenter = room.space.boundsChecker.LocalBoundsCenter;
                Quaternion rotation = room.space.roomTransform.rotation;

                // Calculate what the AABB should be if rotation were properly accounted for
                Bounds correctAABB = CalculateRotatedAABB(localCenter, localSize, room.space.roomTransform);
                Bounds currentAABB = room.bounds;

                float sizeDiffX = Mathf.Abs(correctAABB.size.x - currentAABB.size.x);
                float sizeDiffZ = Mathf.Abs(correctAABB.size.z - currentAABB.size.z);

                if (sizeDiffX > 0.5f || sizeDiffZ > 0.5f)
                {
                    inaccurateCount++;
                    sb.AppendLine($"  <color=red>[INACCURATE]</color> [{room.index}] {room.space.roomName}");
                    sb.AppendLine($"      current AABB size=({currentAABB.size.x:F2}, {currentAABB.size.z:F2}) correct=({correctAABB.size.x:F2}, {correctAABB.size.z:F2})");
                    sb.AppendLine($"      missing coverage: X={sizeDiffX:F2}m Z={sizeDiffZ:F2}m");
                }
            }

            if (inaccurateCount == 0)
            {
                sb.AppendLine("  <color=green>All AABBs match their rotated extents.</color>");
            }
            else
            {
                sb.AppendLine($"  <color=red>{inaccurateCount} rooms have undersized AABBs due to rotation — THIS CAUSES OVERLAPS</color>");
            }

            sb.AppendLine("");
            sb.AppendLine("--- End Report ---");

            return sb.ToString();
        }

        /// <summary>
        /// Calculates the correct axis-aligned bounding box for a rotated room
        /// by transforming all 8 corners of the local bounds through the room's transform.
        /// </summary>
        private static Bounds CalculateRotatedAABB(Vector3 localCenter, Vector3 localSize, Transform roomTransform)
        {
            Vector3 halfSize = localSize * 0.5f;

            // 8 corners of the local bounds box
            Vector3[] corners = new Vector3[8];
            corners[0] = localCenter + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            corners[1] = localCenter + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z);
            corners[2] = localCenter + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z);
            corners[3] = localCenter + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z);
            corners[4] = localCenter + new Vector3( halfSize.x, -halfSize.y, -halfSize.z);
            corners[5] = localCenter + new Vector3( halfSize.x, -halfSize.y,  halfSize.z);
            corners[6] = localCenter + new Vector3( halfSize.x,  halfSize.y, -halfSize.z);
            corners[7] = localCenter + new Vector3( halfSize.x,  halfSize.y,  halfSize.z);

            // Transform each corner to world space
            Bounds worldAABB = new Bounds(roomTransform.TransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                worldAABB.Encapsulate(roomTransform.TransformPoint(corners[i]));
            }

            return worldAABB;
        }

        private struct RoomInfo
        {
            public int index;
            public OccupiedSpaceRegistry.OccupiedSpace space;
            public Bounds bounds;
        }
    }
}
