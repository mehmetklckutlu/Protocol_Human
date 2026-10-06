using System.Collections.Generic;
using UnityEngine;

public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    [SerializeField] private GameObject highlightPrefab; // Mavi renkli kare prefabımız

    private List<GameObject> activeHighlights = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    // Seçilen karelerin üzerine mavi prefabları yerleştirir
    public void ShowRange(List<Node> nodes)
    {
        ClearHighlights(); // Önce eskileri temizle

        foreach (Node node in nodes)
        {
            // Orijinal noktanın biraz üzerine (Z ekseninde) koyalım ki yerin altında kalmasın
            Vector3 spawnPos = node.worldPosition;
            spawnPos.z = -0.5f;

            GameObject highlight = Instantiate(highlightPrefab, spawnPos, Quaternion.identity);
            activeHighlights.Add(highlight);
        }
    }

    // Ekranda parlayan tüm kareleri siler
    public void ClearHighlights()
    {
        foreach (GameObject obj in activeHighlights)
        {
            Destroy(obj);
        }
        activeHighlights.Clear();
    }
}