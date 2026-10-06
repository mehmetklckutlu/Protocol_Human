using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Ayarları")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;
    [SerializeField] private LayerMask unwalkableLayer;

    private Node[,] grid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreateGrid();
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

                // Fizik kontrolü ile engelleri tespit etme (Box overlap)
                bool isWalkable = !Physics2D.OverlapBox(worldPos, Vector2.one * (cellSize * 0.8f), 0f, unwalkableLayer);

                grid[x, y] = new Node(gridPos, worldPos, isWalkable);
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

    // Komşu kareleri getirir (Çapraz veya Dik)
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
}