using System.Collections.Generic;
using UnityEngine;

public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    [SerializeField] private GameObject highlightPrefab;

    private List<GameObject> activeHighlights = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    public void ShowRange(List<Node> nodes)
    {
        ClearHighlights();

        foreach (Node node in nodes)
        {
            Vector3 spawnPos = node.worldPosition;
            spawnPos.z = -0.5f;

            GameObject highlight = Instantiate(highlightPrefab, spawnPos, Quaternion.identity);
            activeHighlights.Add(highlight);
        }
    }

    public void ClearHighlights()
    {
        foreach (GameObject obj in activeHighlights)
        {
            Destroy(obj);
        }
        activeHighlights.Clear();
    }
}