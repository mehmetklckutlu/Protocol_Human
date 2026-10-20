using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Unit))]
public class UnitMovement : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool allowDiagonalMovement = false;

    public Vector2Int CurrentGridPosition { get; private set; }

    private bool isMoving = false;
    private bool isSelected = false;
    private List<Node> reachableNodes = new List<Node>();

    private Unit myUnit; // Karakterin verilerini (Can, Hareket, CPU) tutan referans

    private void Awake()
    {
        myUnit = GetComponent<Unit>();
    }

    private void Start()
    {
        CurrentGridPosition = GridManager.Instance.GetGridPosition(transform.position);
        transform.position = GridManager.Instance.GetWorldPosition(CurrentGridPosition);
    }

    private void Update()
    {
        // Karakter hareket ediyorsa, sıra bizde değilse veya bu karakter düşmansa iptal et
        if (isMoving || TurnManager.Instance.CurrentState != TurnState.PlayerTurn || !myUnit.isPlayerOwned) return;

        // Farenin sol tuşuna tıklandığında
        if (Input.GetMouseButtonDown(0))
        {
            // UI (Turu Bitir butonu vb.) üzerine tıklandıysa haritayı tıklamış sayma
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // SAVAŞTA DEĞİLSEK: Her tıklamada hareketi ve CPU'yu fullüyoruz (Sınırsız gezinti)
            if (!TurnManager.Instance.IsInCombat())
            {
                myUnit.OnTurnStart();
            }

            // Hareket puanı kalmadıysa tıklamayı iptal et
            if (myUnit.currentMovement <= 0)
            {
                if (isSelected)
                {
                    isSelected = false;
                    HighlightManager.Instance.ClearHighlights();
                }
                return;
            }

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int clickedGridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            // DURUM 1: Kendi üzerine tıklandı (Seçim Modu)
            if (clickedGridPos == CurrentGridPosition)
            {
                isSelected = true;
                reachableNodes = GridManager.Instance.GetNodesInRange(CurrentGridPosition, myUnit.currentMovement, allowDiagonalMovement);
                HighlightManager.Instance.ShowRange(reachableNodes);
            }
            // DURUM 2: Seçiliyken başka yere tıklandı (Yürüme)
            else if (isSelected)
            {
                Node clickedNode = GridManager.Instance.GetNode(clickedGridPos);

                if (clickedNode != null && reachableNodes.Contains(clickedNode))
                {
                    List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, clickedGridPos, allowDiagonalMovement);
                    if (path != null && path.Count > 0)
                    {
                        isSelected = false;
                        HighlightManager.Instance.ClearHighlights();

                        TurnManager.Instance.SetBusy(); // Yürürken turu kilitle
                        StartCoroutine(MoveAlongPath(path));
                    }
                }
                else
                {
                    isSelected = false;
                    HighlightManager.Instance.ClearHighlights();
                }
            }
        }

        // Sağ tık ile seçimi iptal et
        if (Input.GetMouseButtonDown(1) && isSelected)
        {
            isSelected = false;
            HighlightManager.Instance.ClearHighlights();
        }
    }

    private IEnumerator MoveAlongPath(List<Node> path)
    {
        isMoving = true;
        int stepsTaken = path.Count;

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

        // Yürüme bittiğinde adımları düş (Bu sayede UI çizgileri sönecek)
        myUnit.UseMovement(stepsTaken);

        isMoving = false;
        TurnManager.Instance.ClearBusy();
    }
}