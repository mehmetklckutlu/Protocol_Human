using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Ayarları")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;
    [SerializeField] private LayerMask unwalkableLayer;

    [Header("Tilemap ile Yürünebilirlik (isteğe bağlı)")]
    [Tooltip("Doluysa: sadece bu Tilemap'te tile olan kareler yürünebilir olur (tile olmayan boşluk yürünemez).")]
    [SerializeField] private Tilemap floorTilemap;
    [Tooltip("Doluysa: bu Tilemap'te tile olan kareler (duvarlar) yürünemez olur.")]
    [SerializeField] private Tilemap wallTilemap;
    [Tooltip("Doluysa: bu Tilemap'te tile olan kareler (siper kutuları) yürünemez olur.")]
    [SerializeField] private Tilemap coverTilemap;

    private Node[,] grid;

    public Vector2Int GridSize => gridSize;
    public float CellSize => cellSize;
    public Vector3 GridOrigin => gridOrigin;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SnapOriginToTilemap();
        CreateGrid();
    }

    // gridOrigin elle girilince (örn. -74.6) hareket gridi zemin karelerinden kayıyor.
    // Zemin tilemap'i varsa origin'i en yakın tile köşesine oturt.
    private void SnapOriginToTilemap()
    {
        Tilemap reference = floorTilemap != null ? floorTilemap : wallTilemap;
        if (reference == null) return;

        Vector3Int cell = reference.WorldToCell(gridOrigin + new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f));
        Vector3 snapped = reference.CellToWorld(cell);
        snapped.z = gridOrigin.z;

        if ((snapped - gridOrigin).sqrMagnitude > 0.0001f)
            Debug.Log("GridManager: gridOrigin " + gridOrigin + " -> " + snapped + " (tilemap'e hizalandı)");
        gridOrigin = snapped;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) SnapOriginToTilemap();
    }

    private void CreateGrid()
    {
        grid = new Node[gridSize.x, gridSize.y];

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                Vector3 worldPos = GetWorldPosition(gridPos);

                bool blockedByCollider = Physics2D.OverlapBox(worldPos, Vector2.one * (cellSize * 0.8f), 0f, unwalkableLayer);
                bool isWalkable = !blockedByCollider;
                bool isWall = blockedByCollider;

                if (isWalkable && floorTilemap != null)
                    isWalkable = floorTilemap.HasTile(floorTilemap.WorldToCell(worldPos));
                if (wallTilemap != null && wallTilemap.HasTile(wallTilemap.WorldToCell(worldPos)))
                {
                    isWalkable = false;
                    isWall = true;
                }
                if (isWalkable && coverTilemap != null)
                    isWalkable = !coverTilemap.HasTile(coverTilemap.WorldToCell(worldPos));
                // Duvar tilemap'i yoksa: yürünemeyen her kare duvar sayılır.
                if (wallTilemap == null && !isWalkable) isWall = true;

                grid[x, y] = new Node(gridPos, worldPos, isWalkable) { isWall = isWall };
            }
        }
    }

    public Node GetNode(Vector2Int gridPos)
    {
        if (IsInsideGrid(gridPos))
            return grid[gridPos.x, gridPos.y];

        return null;
    }

    public Vector3 GetWorldPosition(Vector2Int gridPos)
    {
        return gridOrigin + new Vector3(gridPos.x * cellSize + cellSize / 2f, gridPos.y * cellSize + cellSize / 2f, 0f);
    }

    public Vector2Int GetGridPosition(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - gridOrigin.y) / cellSize);
        return new Vector2Int(Mathf.Clamp(x, 0, gridSize.x - 1), Mathf.Clamp(y, 0, gridSize.y - 1));
    }

    public bool IsInsideGrid(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridSize.x && gridPos.y >= 0 && gridPos.y < gridSize.y;
    }

    public bool IsWall(Vector2Int cell)
    {
        if (!IsInsideGrid(cell)) return true;
        return grid[cell.x, cell.y].isWall;
    }

    /// <summary>
    /// İki kare arasında görüş hattı var mı? Kare merkezleri arasındaki doğrunun geçtiği her kare kontrol edilir
    /// (başlangıç ve hedef hariç); araya duvar giriyorsa saldırı yapılamaz.
    /// Doğru tam bir köşeden geçiyorsa, köşenin iki yanı da duvarsa kesilir; tek yanı duvarsa geçer.
    /// </summary>
    public bool HasLineOfSight(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x, dy = to.y - from.y;
        int nx = Mathf.Abs(dx), ny = Mathf.Abs(dy);
        int sx = dx > 0 ? 1 : -1, sy = dy > 0 ? 1 : -1;
        int x = from.x, y = from.y;
        int ix = 0, iy = 0;

        while (ix < nx || iy < ny)
        {
            long decision = (long)(1 + 2 * ix) * ny - (long)(1 + 2 * iy) * nx;
            if (decision == 0)
            {
                if (IsWall(new Vector2Int(x + sx, y)) && IsWall(new Vector2Int(x, y + sy))) return false;
                x += sx; y += sy; ix++; iy++;
            }
            else if (decision < 0) { x += sx; ix++; }
            else { y += sy; iy++; }

            if (x == to.x && y == to.y) return true;
            if (IsWall(new Vector2Int(x, y))) return false;
        }
        return true;
    }

    public bool IsCellOccupied(Vector2Int cell, Unit ignoreUnit = null)
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null) return false;

        foreach (Unit u in tm.playerUnits)
        {
            if (u == null || u.IsDead || u == ignoreUnit) continue;
            if (GetGridPosition(u.transform.position) == cell) return true;
            UnitMovement move = u.GetComponent<UnitMovement>();
            if (move != null && move.IsMoving && move.CurrentGridPosition == cell) return true;
        }

        foreach (Unit u in tm.enemyUnits)
        {
            if (u == null || u.IsDead || u == ignoreUnit) continue;
            if (GetGridPosition(u.transform.position) == cell) return true;
        }

        return false;
    }

    public List<Node> GetNeighbors(Node node, bool allowDiagonal = false)
    {
        List<Node> neighbors = new List<Node>();

        Vector2Int[] orthogonalDirections = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        foreach (var dir in orthogonalDirections)
        {
            Vector2Int checkPos = node.gridPosition + dir;
            if (IsInsideGrid(checkPos))
            {
                neighbors.Add(GetNode(checkPos));
            }
        }

        if (allowDiagonal)
        {
            Vector2Int[] diagonalDirections = {
                new Vector2Int(-1, 1), new Vector2Int(1, 1),
                new Vector2Int(-1, -1), new Vector2Int(1, -1)
            };

            foreach (var dir in diagonalDirections)
            {
                Vector2Int checkPos = node.gridPosition + dir;
                if (IsInsideGrid(checkPos))
                {
                    neighbors.Add(GetNode(checkPos));
                }
            }
        }

        return neighbors;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(gridOrigin + new Vector3(gridSize.x * cellSize / 2f, gridSize.y * cellSize / 2f, 0f), new Vector3(gridSize.x * cellSize, gridSize.y * cellSize, 0f));

        if (grid != null)
        {
            foreach (Node n in grid)
            {
                Gizmos.color = n.isWalkable ? Color.green : Color.red;
                Gizmos.DrawWireCube(n.worldPosition, Vector3.one * (cellSize * 0.9f));
            }
        }
    }
    public List<Node> GetNodesInRange(Vector2Int startPos, int maxSteps, bool allowDiagonal = false, Unit movingUnit = null)
    {
        List<Node> reachableNodes = new List<Node>();
        Dictionary<Node, int> costs = new Dictionary<Node, int>();
        Queue<Node> queue = new Queue<Node>();

        Node startNode = GetNode(startPos);
        if (startNode == null) return reachableNodes;

        queue.Enqueue(startNode);
        costs[startNode] = 0;

        while (queue.Count > 0)
        {
            Node currentNode = queue.Dequeue();
            reachableNodes.Add(currentNode);

            int currentCost = costs[currentNode];

            if (currentCost >= maxSteps) continue;

            foreach (Node neighbor in GetNeighbors(currentNode, allowDiagonal))
            {
                if (!neighbor.isWalkable) continue;
                if (IsCellOccupied(neighbor.gridPosition, movingUnit)) continue;

                int newCost = currentCost + 1;

                if (!costs.ContainsKey(neighbor) || newCost < costs[neighbor])
                {
                    costs[neighbor] = newCost;
                    if (!queue.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return reachableNodes;
    }
}
