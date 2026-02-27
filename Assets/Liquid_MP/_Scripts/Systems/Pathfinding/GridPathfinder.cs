using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridPathfinder : MonoBehaviour
{
    #region Node Class
    [System.Serializable]
    private class Node
    {
        public bool walkable;
        public Vector3 worldPosition;
        public int gridX;
        public int gridY;
        public int gridZ;

        public int surfaceLayer = -1;
        public Vector3 surfaceNormal = Vector3.up;
        public SurfaceType surfaceType = SurfaceType.Unknown;

        public int gCost;
        public int hCost;
        public Node parent;

        public int lastSearchId;

        public int fCost => gCost + hCost;

        public Node(bool walkable, Vector3 worldPos, int x, int y, int z)
        {
            this.walkable = walkable;
            worldPosition = worldPos;
            gridX = x;
            gridY = y;
            gridZ = z;

            gCost = int.MaxValue;
            hCost = 0;
            parent = null;
            lastSearchId = 0;
        }
    }

    public enum SurfaceType
    {
        Unknown,
        Floor,
        Wall,
        Ceiling
    }
    #endregion

    #region Variables
    private static GridPathfinder _instance;
    public static GridPathfinder Instance => _instance;

    [Header("Grid Settings")]
    [SerializeField] private Vector3 gridWorldSize = new Vector3(50f, 10f, 50f);
    [SerializeField] private float nodeRadius = 0.5f;

    [Tooltip("Layers that block nodes (solid obstacles).")]
    [SerializeField] private LayerMask obstacleMask;

    [Tooltip("Optional: also require the obstacle tag to mark blocked nodes.")]
    [SerializeField] private bool requireObstacleTag = false;

    [Tooltip("If requireObstacleTag is enabled, this tag is treated as blocking.")]
    [SerializeField] private string obstacleTag = "Obstacle";

    [Header("Surface Probing (Floor / Wall / Ceiling)")]
    [Tooltip("Layers considered valid surfaces. If empty, surface probing is disabled and nodes are walkable if not blocked.")]
    [SerializeField] private LayerMask surfaceMask;

    [Tooltip("Max distance to probe for a nearby surface from a node center.")]
    [SerializeField] private float surfaceProbeDistance = 1.25f;

    [Tooltip("If true, the node stores the closest surface normal and layer for filtering.")]
    [SerializeField] private bool storeSurfaceInfo = true;

    [Tooltip("Normal.y >= this means Floor.")]
    [Range(0f, 1f)]
    [SerializeField] private float floorNormalYThreshold = 0.6f;

    [Tooltip("Normal.y <= -this means Ceiling.")]
    [Range(0f, 1f)]
    [SerializeField] private float ceilingNormalYThreshold = 0.6f;

    [Header("Fallback Search")]
    [Tooltip("How many cells outward to search for the nearest walkable node if start/target is blocked.")]
    [SerializeField] private int maxWalkableSearchRadius = 4;

    [Header("Path Rules")]
    [Tooltip("If true, disallow diagonals that cut through corners (prevents squeezing through cracks).")]
    [SerializeField] private bool preventCornerCutting = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = false;
    [Tooltip("Only draw walkable nodes within this radius of the player (0 = draw all, NOT recommended).")]
    [SerializeField] private float gizmoViewRadius = 15f;
    [SerializeField] private Color walkableColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color unwalkableColor = new Color(1f, 0f, 0f, 0.3f);

    [Header("Vertical Alignment")]
    [Tooltip("Additional offset (world units) applied to the grid center on Y. Negative = down, positive = up.")]
    [SerializeField] private float gridCenterYOffset = 0f;

    private Node[,,] _grid;
    private float _nodeDiameter;
    private int _gridSizeX;
    private int _gridSizeY;
    private int _gridSizeZ;

    private int _lastWalkableCount;
    private int _lastUnwalkableCount;

    private int _searchId;

    private const int OverlapBufferSize = 32;
    private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

    /// <summary>Grid dimensions info for diagnostics.</summary>
    public string GridStatsString => _grid == null ? "Grid not built" : $"Grid: {_gridSizeX}x{_gridSizeY}x{_gridSizeZ} = {_gridSizeX * _gridSizeY * _gridSizeZ} nodes | Walkable: {_lastWalkableCount} | Unwalkable: {_lastUnwalkableCount}";
       
    /// <summary>Whether the grid has been built.</summary>
    public bool IsGridBuilt => _grid != null;

    /// <summary>Checks if a world position falls inside the grid bounds.</summary>
    public bool IsInsideGrid(Vector3 worldPos)
    {
        Vector3 half = gridWorldSize * 0.5f;
        Vector3 min = transform.position - half;
        Vector3 max = transform.position + half;
        return worldPos.x >= min.x && worldPos.x <= max.x &&
               worldPos.y >= min.y && worldPos.y <= max.y &&
               worldPos.z >= min.z && worldPos.z <= max.z;
    }
    #endregion

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Multiple GridPathfinder instances in scene. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;
        RebuildWithCurrentSettings();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        nodeRadius = Mathf.Max(0.05f, nodeRadius);
        surfaceProbeDistance = Mathf.Max(0.05f, surfaceProbeDistance);

        if (!Application.isPlaying)
        {
            RebuildWithCurrentSettings();
        }
    }
#endif

    /// <summary>
    /// Rebuilds the grid so it fits the combined bounds of the generated floor.
    /// Height comes from floorBounds.size.y automatically.
    /// </summary>
    public void RebuildToFitBounds(Bounds floorBounds, float extraPaddingXZ = 1f)
    {
        Bounds bound = floorBounds;
        bound.Expand(new Vector3(extraPaddingXZ * 2f, 0f, extraPaddingXZ * 2f));

        float heightFromFloor = Mathf.Max(0.1f, floorBounds.size.y);
        gridWorldSize = new Vector3(bound.size.x, heightFromFloor, bound.size.z);

        float centerY = floorBounds.center.y + gridCenterYOffset;
        transform.position = new Vector3(bound.center.x, centerY, bound.center.z);

        RebuildWithCurrentSettings();
    }

    private void RebuildWithCurrentSettings()
    {
        _nodeDiameter = nodeRadius * 2f;

        _gridSizeX = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.x / _nodeDiameter));
        _gridSizeY = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.y / _nodeDiameter));
        _gridSizeZ = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.z / _nodeDiameter));

        CreateGrid();
    }

    private void CreateGrid()
    {
        _grid = new Node[_gridSizeX, _gridSizeY, _gridSizeZ];

        Vector3 bottomLeft =
            transform.position
            - Vector3.right * gridWorldSize.x / 2f
            - Vector3.up * gridWorldSize.y / 2f
            - Vector3.forward * gridWorldSize.z / 2f;

        int walkableCount = 0;
        int unwalkableCount = 0;

        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int y = 0; y < _gridSizeY; y++)
            {
                for (int z = 0; z < _gridSizeZ; z++)
                {
                    Vector3 worldPoint =
                        bottomLeft
                        + Vector3.right * (x * _nodeDiameter + nodeRadius)
                        + Vector3.up * (y * _nodeDiameter + nodeRadius)
                        + Vector3.forward * (z * _nodeDiameter + nodeRadius);

                    bool blocked = IsBlocked(worldPoint);

                    bool walkable = !blocked;
                    int surfaceLayer = -1;
                    Vector3 surfaceNormal = Vector3.up;
                    SurfaceType surfaceType = SurfaceType.Unknown;

                    if (walkable && surfaceMask.value != 0)
                    {
                        if (TryProbeSurface(worldPoint, out var hit))
                        {
                            surfaceLayer = hit.collider.gameObject.layer;
                            surfaceNormal = hit.normal;
                            surfaceType = ClassifySurface(surfaceNormal);
                        }
                        else
                        {
                            walkable = false;
                        }
                    }

                    if (walkable) walkableCount++;
                    else unwalkableCount++;

                    Node node = new Node(walkable, worldPoint, x, y, z);
                    if (storeSurfaceInfo)
                    {
                        node.surfaceLayer = surfaceLayer;
                        node.surfaceNormal = surfaceNormal;
                        node.surfaceType = surfaceType;
                    }

                    _grid[x, y, z] = node;
                }
            }
        }

        _lastWalkableCount = walkableCount;
        _lastUnwalkableCount = unwalkableCount;
    }

    private bool IsBlocked(Vector3 worldPoint)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(worldPoint, nodeRadius, _overlapBuffer, obstacleMask, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return false;

        if (!requireObstacleTag)
            return true;

        for (int i = 0; i < hitCount; i++)
        {
            var c = _overlapBuffer[i];
            if (c == null) continue;
            if (c.CompareTag(obstacleTag))
                return true;
        }

        return false;
    }

    private bool TryProbeSurface(Vector3 worldPoint, out RaycastHit bestHit)
    {
        bestHit = default;

        float bestDist = float.MaxValue;
        bool found = false;

        Vector3[] dirs =
        {
            Vector3.down, Vector3.up,
            Vector3.left, Vector3.right,
            Vector3.forward, Vector3.back
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            if (Physics.Raycast(worldPoint, dirs[i], out RaycastHit hit, surfaceProbeDistance, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }
        }

        return found;
    }

    private SurfaceType ClassifySurface(Vector3 normal)
    {
        if (normal.y >= floorNormalYThreshold)
            return SurfaceType.Floor;

        if (normal.y <= -ceilingNormalYThreshold)
            return SurfaceType.Ceiling;

        return SurfaceType.Wall;
    }

    private Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x - (transform.position.x - gridWorldSize.x / 2f)) / gridWorldSize.x;
        float percentY = (worldPosition.y - (transform.position.y - gridWorldSize.y / 2f)) / gridWorldSize.y;
        float percentZ = (worldPosition.z - (transform.position.z - gridWorldSize.z / 2f)) / gridWorldSize.z;

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);
        percentZ = Mathf.Clamp01(percentZ);

        // FloorToInt reduces cell jitter compared to RoundToInt
        int x = Mathf.Clamp(Mathf.FloorToInt(_gridSizeX * percentX), 0, _gridSizeX - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(_gridSizeY * percentY), 0, _gridSizeY - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(_gridSizeZ * percentZ), 0, _gridSizeZ - 1);

        return _grid[x, y, z];
    }

    private bool IsNodeWalkableForFilter(Node node, LayerMask allowedLayers, SurfaceType allowedSurface)
    {
        if (!node.walkable)
            return false;

        if (allowedLayers.value != 0)
        {
            if (node.surfaceLayer < 0)
                return false;

            int mask = 1 << node.surfaceLayer;
            if ((allowedLayers.value & mask) == 0)
                return false;
        }

        if (allowedSurface != SurfaceType.Unknown && allowedSurface != SurfaceType.Floor && allowedSurface != SurfaceType.Wall && allowedSurface != SurfaceType.Ceiling)
        {
            return true;
        }

        if (allowedSurface != SurfaceType.Unknown)
        {
            if (node.surfaceType != allowedSurface)
                return false;
        }

        return true;
    }

    private Node FindClosestWalkable(Node startNode, LayerMask allowedLayers, SurfaceType allowedSurface)
    {
        if (IsNodeWalkableForFilter(startNode, allowedLayers, allowedSurface))
            return startNode;

        Node best = null;
        float bestDist = float.MaxValue;

        for (int radius = 1; radius <= maxWalkableSearchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int checkX = startNode.gridX + dx;
                        int checkY = startNode.gridY + dy;
                        int checkZ = startNode.gridZ + dz;

                        if (checkX < 0 || checkX >= _gridSizeX ||
                            checkY < 0 || checkY >= _gridSizeY ||
                            checkZ < 0 || checkZ >= _gridSizeZ)
                        {
                            continue;
                        }

                        Node candidate = _grid[checkX, checkY, checkZ];
                        if (!IsNodeWalkableForFilter(candidate, allowedLayers, allowedSurface))
                            continue;

                        float dist = (candidate.worldPosition - startNode.worldPosition).sqrMagnitude;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = candidate;
                        }
                    }
                }
            }

            if (best != null)
                break;
        }

        return best;
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        return FindPath(startPos, targetPos, default, SurfaceType.Unknown);
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, LayerMask allowedLayers)
    {
        return FindPath(startPos, targetPos, allowedLayers, SurfaceType.Unknown);
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, LayerMask allowedLayers, SurfaceType allowedSurface)
    {
        if (_grid == null)
            return null;

        Node startNode = NodeFromWorldPoint(startPos);
        Node targetNode = NodeFromWorldPoint(targetPos);

        Node snappedStart = FindClosestWalkable(startNode, allowedLayers, allowedSurface);
        Node snappedTarget = FindClosestWalkable(targetNode, allowedLayers, allowedSurface);

        if (snappedStart == null || snappedTarget == null)
            return null;

        _searchId++;
        if (_searchId == int.MaxValue) _searchId = 1;

        var open = new MinHeap(128);
        var closed = new HashSet<Node>();

        PrepareNodeForSearch(snappedStart);
        snappedStart.gCost = 0;
        snappedStart.hCost = GetDistance(snappedStart, snappedTarget);
        snappedStart.parent = null;

        open.Push(snappedStart);

        while (open.Count > 0)
        {
            Node current = open.Pop();
            closed.Add(current);

            if (current == snappedTarget)
            {
                return RetracePath(snappedStart, snappedTarget);
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0)
                            continue;

                        int nx = current.gridX + dx;
                        int ny = current.gridY + dy;
                        int nz = current.gridZ + dz;

                        if (nx < 0 || nx >= _gridSizeX ||
                            ny < 0 || ny >= _gridSizeY ||
                            nz < 0 || nz >= _gridSizeZ)
                            continue;

                        Node neighbour = _grid[nx, ny, nz];

                        if (closed.Contains(neighbour))
                            continue;

                        if (!IsNodeWalkableForFilter(neighbour, allowedLayers, allowedSurface))
                            continue;

                        if (preventCornerCutting && IsCuttingCorner(current, dx, dy, dz, allowedLayers, allowedSurface))
                            continue;

                        PrepareNodeForSearch(neighbour);

                        int stepCost = GetStepCost(dx, dy, dz);
                        int newCost = current.gCost + stepCost;

                        if (newCost < neighbour.gCost)
                        {
                            neighbour.gCost = newCost;
                            neighbour.hCost = GetDistance(neighbour, snappedTarget);
                            neighbour.parent = current;

                            open.PushOrUpdate(neighbour);
                        }
                    }
                }
            }
        }

        return null;
    }

    private void PrepareNodeForSearch(Node node)
    {
        if (node.lastSearchId == _searchId)
            return;

        node.lastSearchId = _searchId;
        node.gCost = int.MaxValue;
        node.hCost = 0;
        node.parent = null;
    }

    private bool IsCuttingCorner(Node current, int dx, int dy, int dz, LayerMask allowedLayers, SurfaceType allowedSurface)
    {
        int axisCount = (dx != 0 ? 1 : 0) + (dy != 0 ? 1 : 0) + (dz != 0 ? 1 : 0);
        if (axisCount <= 1)
            return false;

        if (dx != 0)
        {
            Node n = _grid[current.gridX + dx, current.gridY, current.gridZ];
            if (!IsNodeWalkableForFilter(n, allowedLayers, allowedSurface)) return true;
        }

        if (dy != 0)
        {
            Node n = _grid[current.gridX, current.gridY + dy, current.gridZ];
            if (!IsNodeWalkableForFilter(n, allowedLayers, allowedSurface)) return true;
        }

        if (dz != 0)
        {
            Node n = _grid[current.gridX, current.gridY, current.gridZ + dz];
            if (!IsNodeWalkableForFilter(n, allowedLayers, allowedSurface)) return true;
        }

        return false;
    }

    private static int GetStepCost(int dx, int dy, int dz)
    {
        int axisCount = (dx != 0 ? 1 : 0) + (dy != 0 ? 1 : 0) + (dz != 0 ? 1 : 0);

        return axisCount switch
        {
            1 => 10,
            2 => 14,
            3 => 17,
            _ => 10
        };
    }

    private static int GetDistance(Node a, Node b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);
        int dz = Mathf.Abs(a.gridZ - b.gridZ);

        int min = Mathf.Min(dx, Mathf.Min(dy, dz));
        dx -= min; dy -= min; dz -= min;

        int mid = Mathf.Min(dx, Mathf.Min(dy, dz));
        dx -= mid; dy -= mid; dz -= mid;

        int max = dx + dy + dz; // one is nonzero

        return min * 17 + mid * 14 + max * 10;
    }

    private static List<Vector3> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>(64);
        Node currentNode = endNode;

        while (currentNode != null && currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Reverse();

        List<Vector3> worldPath = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
            worldPath.Add(path[i].worldPosition);

        return worldPath;
    }

    #region MinHeap (priority queue)
    private sealed class MinHeap
    {
        private readonly List<Node> _items;
        private readonly Dictionary<Node, int> _indices;

        public int Count => _items.Count;

        public MinHeap(int capacity)
        {
            _items = new List<Node>(capacity);
            _indices = new Dictionary<Node, int>(capacity);
        }

        public void Push(Node node)
        {
            if (_indices.ContainsKey(node))
            {
                Update(node);
                return;
            }

            _items.Add(node);
            int i = _items.Count - 1;
            _indices[node] = i;
            HeapifyUp(i);
        }

        public void PushOrUpdate(Node node)
        {
            if (_indices.ContainsKey(node)) Update(node);
            else Push(node);
        }

        public Node Pop()
        {
            int last = _items.Count - 1;
            Node root = _items[0];

            Swap(0, last);
            _items.RemoveAt(last);
            _indices.Remove(root);

            if (_items.Count > 0)
                HeapifyDown(0);

            return root;
        }

        public void Update(Node node)
        {
            if (!_indices.TryGetValue(node, out int i))
                return;

            HeapifyUp(i);
            HeapifyDown(i);
        }

        private void HeapifyUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Compare(_items[i], _items[parent]) >= 0)
                    break;

                Swap(i, parent);
                i = parent;
            }
        }

        private void HeapifyDown(int i)
        {
            int count = _items.Count;
            while (true)
            {
                int left = i * 2 + 1;
                int right = i * 2 + 2;
                int smallest = i;

                if (left < count && Compare(_items[left], _items[smallest]) < 0)
                    smallest = left;

                if (right < count && Compare(_items[right], _items[smallest]) < 0)
                    smallest = right;

                if (smallest == i)
                    break;

                Swap(i, smallest);
                i = smallest;
            }
        }

        private int Compare(Node a, Node b)
        {
            int f = a.fCost.CompareTo(b.fCost);
            if (f != 0) return f;
            return a.hCost.CompareTo(b.hCost);
        }

        private void Swap(int i, int j)
        {
            Node a = _items[i];
            Node b = _items[j];

            _items[i] = b;
            _items[j] = a;

            _indices[a] = j;
            _indices[b] = i;
        }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, gridWorldSize.z));

        if (!drawGizmos || _grid == null)
            return;

        Vector3 viewCenter = transform.position;
        float viewRadiusSqr = gizmoViewRadius * gizmoViewRadius;

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                viewCenter = player.transform.position;
        }
        else
        {
            if (UnityEditor.SceneView.lastActiveSceneView != null)
                viewCenter = UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;
        }
#endif

        bool useSpotlight = gizmoViewRadius > 0f;
        float nodeDiameter = nodeRadius * 2f;
        Vector3 cubeSize = Vector3.one * (nodeDiameter - 0.1f);

        foreach (Node node in _grid)
        {
            if (useSpotlight)
            {
                float sqrDist = (node.worldPosition - viewCenter).sqrMagnitude;
                if (sqrDist > viewRadiusSqr) continue;
            }

            if (node.walkable)
            {
                Gizmos.color = walkableColor;
                Gizmos.DrawCube(node.worldPosition, cubeSize);
            }
            else
            {
                Gizmos.color = unwalkableColor;
                Gizmos.DrawWireCube(node.worldPosition, cubeSize * 0.5f);
            }
        }
    }
    #endregion
}