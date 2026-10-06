using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool allowDiagonalMovement = false;

    [Header("Menzil (AP) Ayarları")]
    [SerializeField] private int maxMoveRange = 4; // Karakter tek turda en fazla 4 kare gidebilir

    public Vector2Int CurrentGridPosition { get; private set; }

    private bool isMoving = false;
    private bool isSelected = false; // Karakter şu an seçili mi?
    private List<Node> reachableNodes = new List<Node>(); // Gidebileceği karelerin listesi

    private void Start()
    {
        CurrentGridPosition = GridManager.Instance.GetGridPosition(transform.position);
        transform.position = GridManager.Instance.GetWorldPosition(CurrentGridPosition);
    }

    private void Update()
    {
        if (isMoving) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int clickedGridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            // 1. DURUM: Karakterin kendi üzerine tıklandıysa (Seçim Modu)
            if (clickedGridPos == CurrentGridPosition)
            {
                isSelected = true;

                // Gidebileceği yerleri hesapla ve HighlightManager'a çizdir
                reachableNodes = GridManager.Instance.GetNodesInRange(CurrentGridPosition, maxMoveRange, allowDiagonalMovement);
                HighlightManager.Instance.ShowRange(reachableNodes);

                Debug.Log("Karakter Seçildi. Gidebileceği kare sayısı: " + reachableNodes.Count);
            }
            // 2. DURUM: Karakter seçiliyken haritada BAŞKA bir yere tıklandıysa
            else if (isSelected)
            {
                Node clickedNode = GridManager.Instance.GetNode(clickedGridPos);

                // Tıklanan yer menzil içindeyse oraya yürü
                if (clickedNode != null && reachableNodes.Contains(clickedNode))
                {
                    List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, clickedGridPos, allowDiagonalMovement);
                    if (path != null && path.Count > 0)
                    {
                        isSelected = false;
                        HighlightManager.Instance.ClearHighlights(); // Yürümeye başlarken boyaları sil
                        StartCoroutine(MoveAlongPath(path));
                    }
                }
                else
                {
                    // Menzil dışına tıklandıysa seçimi iptal et (Vazgeçme)
                    isSelected = false;
                    HighlightManager.Instance.ClearHighlights();
                    Debug.Log("Menzil dışı veya iptal.");
                }
            }
        }

        // Sağ tıklayarak da seçimi iptal edebilsin
        if (Input.GetMouseButtonDown(1) && isSelected)
        {
            isSelected = false;
            HighlightManager.Instance.ClearHighlights();
        }
    }

    private IEnumerator MoveAlongPath(List<Node> path)
    {
        isMoving = true;

        foreach (Node node in path)
        {
            Vector3 targetWorldPos = node.worldPosition;

            while (Vector2.Distance(new Vector2(transform.position.x, transform.position.y), new Vector2(targetWorldPos.x, targetWorldPos.y)) > 0.01f)
            {
                float step = moveSpeed * Time.deltaTime;
                Vector3 newPos = Vector3.MoveTowards(transform.position, targetWorldPos, step);

                newPos.z = newPos.y * 0.01f;
                transform.position = newPos;

                yield return null;
            }

            targetWorldPos.z = targetWorldPos.y * 0.01f;
            transform.position = targetWorldPos;
            CurrentGridPosition = node.gridPosition;
        }

        isMoving = false;
    }
}