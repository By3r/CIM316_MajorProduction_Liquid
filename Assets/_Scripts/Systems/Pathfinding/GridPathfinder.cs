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

        public int gCost;
        public int hCost;
        public Node parent;

        public int fCost => gCost + hCost;

        public Node(bool walkable, Vector3 worldPos, int x, int y, int z)
        {
            this.walkable = walkable;
            this.worldPosition = worldPos;
            this.gridX = x;
            this.gridY = y;
            this.gridZ = z;

            gCost = int.MaxValue;
            hCost = 0;
            parent = null;
        }
    }
    #endregion

    #region Variables
    private static GridPathfinder _instance;
    public static GridPathfinder Instance => _instance;

    [Header("Grid Settings")]
    [SerializeField] private Vector3 gridWorldSize = new Vector3(50f, 10f, 50f);
    [SerializeField] private float nodeRadius = 0.5f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Fallback Search")]
    [Tooltip("How many cells outward to search for the nearest walkable node if start/target is blocked.")]
    [SerializeField] private int maxWalkableSearchRadius = 4;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = false;
    [SerializeField] private Color walkableColor = Color.white;
    [SerializeField] private Color unwalkableColor = Color.red;

    [Header("Vertical Alignment")]
    [Tooltip("Additional offset (world units) applied to the grid center on Y. Negative = down, positive = up.")]
    [SerializeField] private float gridCenterYOffset = 0f;

    private Node[,,] _grid;
    private float _nodeDiameter;
    private int _gridSizeX;
    private int _gridSizeY;
    private int _gridSizeZ;
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

        _nodeDiameter = nodeRadius * 2f;
        _gridSizeX = Mathf.RoundToInt(gridWorldSize.x / _nodeDiameter);
        _gridSizeY = Mathf.RoundToInt(gridWorldSize.y / _nodeDiameter);
        _gridSizeZ = Mathf.RoundToInt(gridWorldSize.z / _nodeDiameter);

        CreateGrid();
    }

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

                    bool walkable = true;
                    int surfaceLayer = -1;

                    Collider[] hits = Physics.OverlapSphere(worldPoint, nodeRadius);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        Collider collider = hits[i];
                        if (((1 << collider.gameObject.layer) & obstacleMask.value) != 0 &&
                            collider.CompareTag("Obstacle"))
                        {
                            walkable = false;
                        }

                        if (!collider.isTrigger && surfaceLayer == -1)
                        {
                            surfaceLayer = collider.gameObject.layer;
                        }
                    }

                    if (walkable) walkableCount++;
                    else unwalkableCount++;

                    Node node = new Node(walkable, worldPoint, x, y, z);
                    node.surfaceLayer = surfaceLayer;

                    _grid[x, y, z] = node;
                }
            }
        }

        Debug.Log($"[GridPathfinder] Grid created. Walkable: {walkableCount}, Unwalkable: {unwalkableCount}");
    }

    private Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x - (transform.position.x - gridWorldSize.x / 2f)) / gridWorldSize.x;
        float percentY = (worldPosition.y - (transform.position.y - gridWorldSize.y / 2f)) / gridWorldSize.y;
        float percentZ = (worldPosition.z - (transform.position.z - gridWorldSize.z / 2f)) / gridWorldSize.z;

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);
        percentZ = Mathf.Clamp01(percentZ);

        int x = Mathf.Clamp(Mathf.RoundToInt((_gridSizeX - 1) * percentX), 0, _gridSizeX - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt((_gridSizeY - 1) * percentY), 0, _gridSizeY - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt((_gridSizeZ - 1) * percentZ), 0, _gridSizeZ - 1);

        return _grid[x, y, z];
    }

    private List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                    {
                        continue;
                    }

                    int checkX = node.gridX + x;
                    int checkY = node.gridY + y;
                    int checkZ = node.gridZ + z;

                    if (checkX < 0 || checkX >= _gridSizeX ||
                        checkY < 0 || checkY >= _gridSizeY ||
                        checkZ < 0 || checkZ >= _gridSizeZ)
                    {
                        continue;
                    }

                    neighbours.Add(_grid[checkX, checkY, checkZ]);
                }
            }
        }

        return neighbours;
    }

    private static int GetDistance(Node a, Node b)
    {
        int distancetX = Mathf.Abs(a.gridX - b.gridX);
        int distanceY = Mathf.Abs(a.gridY - b.gridY);
        int distanceZ = Mathf.Abs(a.gridZ - b.gridZ);

        return 10 * (distancetX + distanceY + distanceZ);
    }

    private bool IsNodeWalkableForLayers(Node node, LayerMask allowedLayers)
    {
        if (!node.walkable)
        {
            return false;
        }

        if (allowedLayers.value == 0)
        {
            return true;
        }

        if (node.surfaceLayer < 0)
        {
            return false;
        }

        int mask = 1 << node.surfaceLayer;
        return (allowedLayers.value & mask) != 0;
    }

    private Node FindClosestWalkable(Node startNode, LayerMask allowedLayers)
    {
        if (IsNodeWalkableForLayers(startNode, allowedLayers))
        {
            return startNode;
        }

        Node best = null;
        float bestDist = float.MaxValue;

        for (int radius = 1; radius <= maxWalkableSearchRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        int checkX = startNode.gridX + x;
                        int checkY = startNode.gridY + y;
                        int checkZ = startNode.gridZ + z;

                        if (checkX < 0 || checkX >= _gridSizeX ||
                            checkY < 0 || checkY >= _gridSizeY ||
                            checkZ < 0 || checkZ >= _gridSizeZ)
                        {
                            continue;
                        }

                        Node candidate = _grid[checkX, checkY, checkZ];
                        if (!IsNodeWalkableForLayers(candidate, allowedLayers))
                        {
                            continue;
                        }

                        float dist = Vector3.SqrMagnitude(candidate.worldPosition - startNode.worldPosition);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = candidate;
                        }
                    }
                }
            }

            if (best != null)
            {
                break;
            }
        }

        return best;
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        return FindPath(startPos, targetPos, default);
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, LayerMask allowedLayers)
    {
        Node startNode = NodeFromWorldPoint(startPos);
        Node targetNode = NodeFromWorldPoint(targetPos);

        Node snappedStart = FindClosestWalkable(startNode, allowedLayers);
        Node snappedTarget = FindClosestWalkable(targetNode, allowedLayers);

        if (snappedStart == null)
        {
            Debug.LogWarning($"Start area has no walkable nodes for layers {allowedLayers.value}. StartPos: {startPos}");
            return null;
        }

        if (snappedTarget == null)
        {
            Debug.LogWarning($"Target area has no walkable nodes for layers {allowedLayers.value}. TargetPos: {targetPos}");
            return null;
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        foreach (Node node in _grid)
        {
            node.gCost = int.MaxValue;
            node.hCost = 0;
            node.parent = null;
        }

        snappedStart.gCost = 0;
        snappedStart.hCost = GetDistance(snappedStart, snappedTarget);

        openSet.Add(snappedStart);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost ||
                    openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost)
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == snappedTarget)
            {
                return RetracePath(snappedStart, snappedTarget);
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (closedSet.Contains(neighbour))
                {
                    continue;
                }

                if (!IsNodeWalkableForLayers(neighbour, allowedLayers))
                {
                    continue;
                }

                int newCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                if (newCostToNeighbour < neighbour.gCost)
                {
                    neighbour.gCost = newCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, snappedTarget);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                }
            }
        }

        Debug.LogWarning($"No path found from {startPos} to {targetPos} for layers {allowedLayers.value}.");
        return null;
    }

    private static List<Vector3> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Reverse();

        List<Vector3> worldPath = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
        {
            worldPath.Add(path[i].worldPosition);
        }

        return worldPath;
    }

    #region Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, gridWorldSize.z));

        if (!drawGizmos || _grid == null)
        {
            return;
        }

        float nodeDiameter = nodeRadius * 2f;

        foreach (Node node in _grid)
        {
            Gizmos.color = node.walkable ? walkableColor : unwalkableColor;
            Gizmos.DrawCube(node.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
        }
    }
    #endregion
}