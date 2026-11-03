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

    private Unit myUnit;

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
        if (isMoving || TurnManager.Instance.CurrentState != TurnState.PlayerTurn || !myUnit.isPlayerOwned) return;

        if (TurnManager.Instance.IsInCombat() && TurnManager.Instance.CurrentPlayerUnit != myUnit) return;

        if (CardManager.Instance != null && CardManager.Instance.BlocksMapInput) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int clickedGridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            if (clickedGridPos == CurrentGridPosition && CardManager.Instance != null)
                CardManager.Instance.SetActiveUnit(myUnit);

            if (!TurnManager.Instance.IsInCombat())
            {
                myUnit.OnTurnStart();
            }

            if (myUnit.currentMovement <= 0)
            {
                if (isSelected)
                {
                    isSelected = false;
                    HighlightManager.Instance.ClearHighlights();
                }
                return;
            }

            if (clickedGridPos == CurrentGridPosition)
            {
                isSelected = true;
                reachableNodes = GridManager.Instance.GetNodesInRange(CurrentGridPosition, myUnit.currentMovement, allowDiagonalMovement);
                HighlightManager.Instance.ShowRange(reachableNodes);
            }
            else if (isSelected)
            {
                if (IsOccupiedByOtherUnit(clickedGridPos))
                {
                    isSelected = false;
                    HighlightManager.Instance.ClearHighlights();
                    return;
                }

                Node clickedNode = GridManager.Instance.GetNode(clickedGridPos);

                if (clickedNode != null && reachableNodes.Contains(clickedNode))
                {
                    List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, clickedGridPos, allowDiagonalMovement);
                    if (path != null && path.Count > 0)
                    {
                        isSelected = false;
                        HighlightManager.Instance.ClearHighlights();

                        TurnManager.Instance.SetBusy();
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

        if (Input.GetMouseButtonDown(1) && isSelected)
        {
            isSelected = false;
            HighlightManager.Instance.ClearHighlights();
        }
    }

    public bool IsMoving => isMoving;

    public bool TryMoveNextTo(Vector2Int target, System.Action onArrive)
    {
        if (isMoving || !enabled) return false;
        if (!TurnManager.Instance.IsInCombat()) myUnit.OnTurnStart();

        List<Node> best = null;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int c = target + new Vector2Int(dx, dy);
                if (c == CurrentGridPosition)
                {
                    onArrive?.Invoke();
                    return true;
                }

                Node node = GridManager.Instance.GetNode(c);
                if (node == null || !node.isWalkable || IsOccupiedByOtherUnit(c)) continue;

                List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, c, allowDiagonalMovement);
                if (path == null || path.Count == 0 || path.Count > myUnit.currentMovement) continue;
                if (best == null || path.Count < best.Count) best = path;
            }
        }

        if (best == null) return false;

        isSelected = false;
        HighlightManager.Instance.ClearHighlights();
        TurnManager.Instance.SetBusy();
        StartCoroutine(MoveThen(best, onArrive));
        return true;
    }

    private IEnumerator MoveThen(List<Node> path, System.Action onArrive)
    {
        yield return MoveAlongPath(path);
        onArrive?.Invoke();
    }

    private bool IsOccupiedByOtherUnit(Vector2Int cell)
    {
        TurnManager tm = TurnManager.Instance;
        foreach (Unit u in tm.playerUnits)
            if (u != null && u != myUnit && GridManager.Instance.GetGridPosition(u.transform.position) == cell) return true;
        foreach (Unit u in tm.enemyUnits)
            if (u != null && GridManager.Instance.GetGridPosition(u.transform.position) == cell) return true;
        return false;
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

        myUnit.UseMovement(stepsTaken);

        isMoving = false;
        TurnManager.Instance.ClearBusy();
    }
}
