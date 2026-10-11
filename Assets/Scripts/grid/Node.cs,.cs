using UnityEngine;

public class Node
{
    public Vector2Int gridPosition; // Grid üzerindeki (X, Y) koordinatı
    public Vector3 worldPosition;   // Dünya üzerindeki (X, Y, Z) merkez noktası
    public bool isWalkable;         // Üzerinde yürünebilir mi?

    // A* Algoritması Değişkenleri
    public int gCost; // Başlangıç düğümünden bu düğüme olan uzaklık maliyeti
    public int hCost; // Bu düğümden hedef düğüme olan tahmini uzaklık maliyeti (Heuristic)
    public Node parentNode; // Yolu geriye dönük rekonstrükte etmek için ebeveyn düğüm

    public int FCost => gCost + hCost; // Toplam maliyet

    public Node(Vector2Int gridPos, Vector3 worldPos, bool walkable)
    {
        gridPosition = gridPos;
        worldPosition = worldPos;
        isWalkable = walkable;
    }
}