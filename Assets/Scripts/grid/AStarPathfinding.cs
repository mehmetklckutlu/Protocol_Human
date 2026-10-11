using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinding
{
    private const int MOVE_STRAIGHT_COST = 10;
    private const int MOVE_DIAGONAL_COST = 14;

    public static List<Node> FindPath(Vector2Int startPos, Vector2Int targetPos, bool allowDiagonal = false)
    {
        Node startNode = GridManager.Instance.GetNode(startPos);
        Node targetNode = GridManager.Instance.GetNode(targetPos);

        if (startNode == null || targetNode == null || !targetNode.isWalkable)
        {
            return null; // Hedef kare veya başlangıç geçersiz
        }

        List<Node> openSet = new List<Node> { startNode };
        HashSet<Node> closedSet = new HashSet<Node>();

        startNode.gCost = 0;
        startNode.hCost = CalculateDistanceCost(startNode, targetNode, allowDiagonal);
        startNode.parentNode = null;

        while (openSet.Count > 0)
        {
            // OpenSet içinde F maliyeti en düşük olan nodu seç
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost || (openSet[i].FCost == currentNode.FCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // Hedefe ulaşıldıysa yolu oluştur ve dön
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in GridManager.Instance.GetNeighbors(currentNode, allowDiagonal))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor))
                    continue;

                int newMovementCostToNeighbor = currentNode.gCost + CalculateDistanceCost(currentNode, neighbor, allowDiagonal);
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = CalculateDistanceCost(neighbor, targetNode, allowDiagonal);
                    neighbor.parentNode = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // Yol bulunamadı
    }

    private static List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parentNode;
        }

        path.Reverse();
        return path;
    }

    private static int CalculateDistanceCost(Node a, Node b, bool allowDiagonal)
    {
        int xDistance = Mathf.Abs(a.gridPosition.x - b.gridPosition.x);
        int yDistance = Mathf.Abs(a.gridPosition.y - b.gridPosition.y);

        if (allowDiagonal)
        {
            int remaining = Mathf.Abs(xDistance - yDistance);
            return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
        }
        else
        {
            // Manhattan Uzaklığı (Sadece dik hareketler için)
            return MOVE_STRAIGHT_COST * (xDistance + yDistance);
        }
    }
}