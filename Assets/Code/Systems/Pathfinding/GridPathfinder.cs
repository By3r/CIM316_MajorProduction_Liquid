using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple grid based A* pathfinder for navigation on the XZ plane.
/// Obstacles are colliders with tag "Obstacle" on layers included in obstacleMask.
/// </summary>
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

        public int gCost;
        public int hCost;
        public Node parent;

        public int fCost => gCost + hCost;

        public Node(bool walkable, Vector3 worldPos, int x, int y)
        {
            this.walkable = walkable;
            this.worldPosition = worldPos;
            this.gridX = x;
            this.gridY = y;

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
    [SerializeField] private Vector2 gridWorldSize = new Vector2(50f, 50f);
    [SerializeField] private float nodeRadius = 0.5f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Fallback Search")]
    [Tooltip("How many cells outward to search for the nearest walkable node if start/target is blocked.")]
    [SerializeField] private int maxWalkableSearchRadius = 4;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = false;
    [SerializeField] private Color walkableColor = Color.white;
    [SerializeField] private Color unwalkableColor = Color.red;

    private Node[,] _grid;
    private float _nodeDiameter;
    private int _gridSizeX;
    private int _gridSizeY;
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

        CreateGrid();
    }

    private void CreateGrid()
    {
        _grid = new Node[_gridSizeX, _gridSizeY];

        Vector3 bottomLeft = transform.position
                             - Vector3.right * gridWorldSize.x / 2f
                             - Vector3.forward * gridWorldSize.y / 2f;

        int walkableCount = 0;
        int unwalkableCount = 0;

        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int y = 0; y < _gridSizeY; y++)
            {
                Vector3 worldPoint =
                    bottomLeft
                    + Vector3.right * (x * _nodeDiameter + nodeRadius)
                    + Vector3.forward * (y * _nodeDiameter + nodeRadius);

                bool walkable = true;

                Collider[] hits = Physics.OverlapSphere(worldPoint, nodeRadius, obstacleMask);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].CompareTag("Obstacle"))
                    {
                        walkable = false;
                        break;
                    }
                }

                if (walkable) walkableCount++; else unwalkableCount++;

                _grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }

        Debug.Log($"[GridPathfinder] Grid created. Walkable: {walkableCount}, Unwalkable: {unwalkableCount}");
    }

    private Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x - (transform.position.x - gridWorldSize.x / 2f)) / gridWorldSize.x;
        float percentY = (worldPosition.z - (transform.position.z - gridWorldSize.y / 2f)) / gridWorldSize.y;

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.Clamp(Mathf.RoundToInt((_gridSizeX - 1) * percentX), 0, _gridSizeX - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt((_gridSizeY - 1) * percentY), 0, _gridSizeY - 1);

        return _grid[x, y];
    }

    private List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX < 0 || checkX >= _gridSizeX ||
                    checkY < 0 || checkY >= _gridSizeY)
                {
                    continue;
                }

                if (x != 0 && y != 0)
                {
                    Node sideA = _grid[node.gridX + x, node.gridY];
                    Node sideB = _grid[node.gridX, node.gridY + y];

                    if (!sideA.walkable || !sideB.walkable)
                    {
                        continue;
                    }
                }

                neighbours.Add(_grid[checkX, checkY]);
            }
        }

        return neighbours;
    }


    private static int GetDistance(Node a, Node b)
    {
        int dstX = Mathf.Abs(a.gridX - b.gridX);
        int dstY = Mathf.Abs(a.gridY - b.gridY);

        if (dstX > dstY)
        {
            return 14 * dstY + 10 * (dstX - dstY);
        }

        return 14 * dstX + 10 * (dstY - dstX);
    }

    private Node FindClosestWalkable(Node startNode)
    {
        if (startNode.walkable)
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
                    int checkX = startNode.gridX + x;
                    int checkY = startNode.gridY + y;

                    if (checkX < 0 || checkX >= _gridSizeX || checkY < 0 || checkY >= _gridSizeY)
                    {
                        continue;
                    }

                    Node candidate = _grid[checkX, checkY];
                    if (!candidate.walkable)
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

            if (best != null)
            {
                break;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns a world-space path between two positions or null if none exists.
    /// If start or target cell is blocked, tries to snap to nearest walkable cell first.
    /// </summary>
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = NodeFromWorldPoint(startPos);
        Node targetNode = NodeFromWorldPoint(targetPos);

        Node snappedStart = FindClosestWalkable(startNode);
        Node snappedTarget = FindClosestWalkable(targetNode);

        if (snappedStart == null)
        {
            Debug.LogWarning($"Start area has no walkable nodes. StartPos: {startPos}");
            return null;
        }

        if (snappedTarget == null)
        {
            Debug.LogWarning($"Target area has no walkable nodes. TargetPos: {targetPos}");
            return null;
        }

        if (!snappedStart.walkable || !snappedTarget.walkable)
        {
            return null;
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        foreach (Node n in _grid)
        {
            n.gCost = int.MaxValue;
            n.hCost = 0;
            n.parent = null;
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
                if (!neighbour.walkable || closedSet.Contains(neighbour))
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

        Debug.LogWarning($"No path found from {startPos} to {targetPos}.");
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
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 0.1f, gridWorldSize.y));

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
