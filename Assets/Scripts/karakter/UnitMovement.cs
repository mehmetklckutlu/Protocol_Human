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
    private List<Node> reachableNodes = new List<Node>();
    private bool rangeShown = false;
    private Vector2Int lastRangeOrigin = new Vector2Int(int.MinValue, int.MinValue);
    private int lastRangeSteps = -1;

    private List<Node> pendingRedirectPath = null;
    private System.Action pendingArrivalAction = null;
    private Coroutine moveCoroutine = null;

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
        if (TurnManager.Instance == null || GridManager.Instance == null || !myUnit.isPlayerOwned || myUnit.IsDead)
            return;

        bool inCombat = TurnManager.Instance.IsInCombat();
        bool isCardTargeting = CardManager.Instance != null && CardManager.Instance.IsTargeting;
        bool blocksMapInput = CardManager.Instance != null && CardManager.Instance.BlocksMapInput;

        if (inCombat)
        {
            UpdateCombatMovement(isCardTargeting, blocksMapInput);
        }
        else
        {
            UpdateExplorationMovement(blocksMapInput);
        }
    }

    private void UpdateCombatMovement(bool isCardTargeting, bool blocksMapInput)
    {
        bool isMyTurn = TurnManager.Instance.CurrentState == TurnState.PlayerTurn
                        && TurnManager.Instance.CurrentPlayerUnit == myUnit;

        if (!isMyTurn || isMoving || isCardTargeting)
        {
            if (rangeShown)
            {
                if (!isCardTargeting && HighlightManager.Instance != null && isMyTurn)
                    HighlightManager.Instance.ClearHighlights();
                rangeShown = false;
            }
            return;
        }

        if (myUnit.currentMovement <= 0)
        {
            if (rangeShown)
            {
                if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
                rangeShown = false;
            }
            return;
        }

        if (!rangeShown || lastRangeOrigin != CurrentGridPosition || lastRangeSteps != myUnit.currentMovement)
        {
            RefreshCombatRange();
        }

        if (blocksMapInput) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (Camera.main == null) return;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int clickedGridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            if (clickedGridPos == CurrentGridPosition || IsOccupiedByOtherUnit(clickedGridPos))
                return;

            Node clickedNode = GridManager.Instance.GetNode(clickedGridPos);
            if (clickedNode != null && reachableNodes.Contains(clickedNode))
            {
                List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, clickedGridPos, allowDiagonalMovement);
                if (path != null && path.Count > 0 && path.Count <= myUnit.currentMovement)
                {
                    if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
                    rangeShown = false;
                    TurnManager.Instance.SetBusy();
                    moveCoroutine = StartCoroutine(MoveAlongPath(path, true, null));
                }
            }
        }
    }

    private void RefreshCombatRange()
    {
        List<Node> raw = GridManager.Instance.GetNodesInRange(CurrentGridPosition, myUnit.currentMovement, allowDiagonalMovement);
        reachableNodes.Clear();
        foreach (Node n in raw)
        {
            if (n == null) continue;
            if (n.gridPosition != CurrentGridPosition && IsOccupiedByOtherUnit(n.gridPosition)) continue;
            reachableNodes.Add(n);
        }

        if (HighlightManager.Instance != null)
            HighlightManager.Instance.ShowRange(reachableNodes);

        rangeShown = true;
        lastRangeOrigin = CurrentGridPosition;
        lastRangeSteps = myUnit.currentMovement;
    }

    private void UpdateExplorationMovement(bool blocksMapInput)
    {
        if (rangeShown)
        {
            if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
            rangeShown = false;
        }

        if (TurnManager.Instance.CurrentState != TurnState.PlayerTurn || blocksMapInput) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (Camera.main == null) return;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int clickedGridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            if (clickedGridPos == CurrentGridPosition)
            {
                if (CardManager.Instance != null) CardManager.Instance.SetActiveUnit(myUnit);
                return;
            }

            Unit active = CardManager.Instance != null ? CardManager.Instance.GetActiveUnit() : null;
            if (active != null && active != myUnit) return;
            if (active == null && !myUnit.isMainDrone) return;

            if (FogOfWar.Instance != null && !FogOfWar.Instance.IsVisible(clickedGridPos) && !FogOfWar.Instance.IsExplored(clickedGridPos))
                return;

            Node clickedNode = GridManager.Instance.GetNode(clickedGridPos);
            if (clickedNode == null || !clickedNode.isWalkable || IsOccupiedByOtherUnit(clickedGridPos))
                return;

            myUnit.currentMovement = myUnit.maxMovement;

            if (isMoving)
            {
                List<Node> redirect = AStarPathfinding.FindPath(CurrentGridPosition, clickedGridPos, allowDiagonalMovement);
                if (redirect != null && redirect.Count > 0)
                {
                    pendingRedirectPath = redirect;
                    pendingArrivalAction = null;
                }
                return;
            }

            List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, clickedGridPos, allowDiagonalMovement);
            if (path != null && path.Count > 0)
            {
                moveCoroutine = StartCoroutine(MoveAlongPath(path, false, null));
            }
        }
    }

    public bool IsMoving => isMoving;

    public bool TryMoveNextTo(Vector2Int target, System.Action onArrive)
    {
        if (!enabled) return false;
        bool inCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
        if (inCombat && isMoving) return false;
        if (!inCombat) myUnit.currentMovement = myUnit.maxMovement;

        List<Node> best = null;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int c = target + new Vector2Int(dx, dy);
                if (c == CurrentGridPosition && !isMoving)
                {
                    onArrive?.Invoke();
                    return true;
                }

                Node node = GridManager.Instance.GetNode(c);
                if (node == null || !node.isWalkable || IsOccupiedByOtherUnit(c)) continue;

                List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, c, allowDiagonalMovement);
                if (path == null || path.Count == 0) continue;
                if (inCombat && path.Count > myUnit.currentMovement) continue;
                if (best == null || path.Count < best.Count) best = path;
            }
        }

        if (best == null) return false;

        if (rangeShown && HighlightManager.Instance != null)
        {
            HighlightManager.Instance.ClearHighlights();
            rangeShown = false;
        }

        if (isMoving && !inCombat)
        {
            pendingRedirectPath = best;
            pendingArrivalAction = onArrive;
            return true;
        }

        if (inCombat) TurnManager.Instance.SetBusy();
        moveCoroutine = StartCoroutine(MoveAlongPath(best, inCombat, onArrive));
        return true;
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

    private IEnumerator MoveAlongPath(List<Node> path, bool combatMove, System.Action onArrive)
    {
        isMoving = true;
        pendingRedirectPath = null;
        pendingArrivalAction = onArrive;

        int i = 0;
        int stepsTaken = 0;

        while (i < path.Count)
        {
            Node node = path[i];
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
            stepsTaken++;
            i++;

            if (!combatMove)
            {
                if (FogOfWar.Instance != null) FogOfWar.Instance.Refresh();
                yield return null;

                if (TurnManager.Instance != null && TurnManager.Instance.IsInCombat())
                {
                    pendingArrivalAction = null;
                    myUnit.currentMovement = myUnit.maxMovement;
                    break;
                }

                if (pendingRedirectPath != null)
                {
                    List<Node> nextPath = pendingRedirectPath;
                    pendingRedirectPath = null;
                    if (nextPath.Count > 0 && nextPath[0].gridPosition == CurrentGridPosition)
                        nextPath.RemoveAt(0);
                    path = nextPath;
                    i = 0;
                }
            }
        }

        if (combatMove)
        {
            myUnit.UseMovement(stepsTaken);
            isMoving = false;
            moveCoroutine = null;
            TurnManager.Instance.ClearBusy();
        }
        else
        {
            myUnit.currentMovement = myUnit.maxMovement;
            isMoving = false;
            moveCoroutine = null;
        }

        System.Action callback = pendingArrivalAction;
        pendingArrivalAction = null;
        if (callback != null && (TurnManager.Instance == null || !TurnManager.Instance.IsInCombat()))
        {
            callback.Invoke();
        }
    }
}
