using UnityEngine;

public class Node
{
    public Vector2Int gridPosition;
    public Vector3 worldPosition;
    public bool isWalkable;

    public int gCost;
    public int hCost;
    public Node parentNode;

    public int FCost => gCost + hCost;

    public Node(Vector2Int gridPos, Vector3 worldPos, bool walkable)
    {
        gridPosition = gridPos;
        worldPosition = worldPos;
        isWalkable = walkable;
    }
}
