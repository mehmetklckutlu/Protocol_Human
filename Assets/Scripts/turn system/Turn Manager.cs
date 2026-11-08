using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TurnState { PlayerTurn, EnemyTurn, Busy }

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }
    public TurnState CurrentState { get; private set; }

    [Header("Sahnedeki Karakterler")]
    public List<Unit> playerUnits = new List<Unit>();
    public List<Unit> enemyUnits = new List<Unit>();

    [Header("Arayüz (UI)")]
    public GameObject enemyAlertUI;
    public GameObject endTurnButton;
    public GameObject roundEndUI;

    private readonly List<Unit> playerQueue = new List<Unit>();
    public Unit CurrentPlayerUnit { get; private set; }

    private TurnState previousState;
    private bool wasInCombat = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartPlayerTurn();
        UpdateCombatState();
    }

    public bool IsInCombat()
    {
        foreach (Unit enemy in enemyUnits)
        {
            if (enemy == null) continue;
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake) return true;
        }
        return false;
    }

    public void UpdateCombatState()
    {
        bool inCombat = IsInCombat();

        if (endTurnButton != null)
            endTurnButton.SetActive(inCombat);

        if (inCombat && !wasInCombat)
        {
            BeginPlayerRound();
        }

        if (!inCombat && wasInCombat)
        {
            EndCombat();
        }

        wasInCombat = inCombat;

        if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
    }

    private void EndCombat()
    {
        Debug.Log("<color=green>Savaş bitti, Keşif Moduna dönüldü.</color>");

        if (enemyAlertUI != null) enemyAlertUI.SetActive(false);
        if (roundEndUI != null) roundEndUI.SetActive(false);
        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.ClearAll();

        foreach (Unit player in new List<Unit>(playerUnits))
        {
            if (player == null) continue;
            DroneUnit d = player.GetComponent<DroneUnit>();
            if (d != null && d.isAlly)
            {
                FloatingText.Spawn(player.transform.position, "KAPANDI", Color.gray);
                player.Kill();
            }
        }
        playerQueue.Clear();
        CurrentPlayerUnit = null;

        foreach (Unit player in playerUnits)
        {
            if (player != null) player.OnTurnStart();
        }
    }

    public Unit GetClosestPlayer(Vector3 fromWorldPos)
    {
        Unit best = null;
        int bestDist = int.MaxValue;
        Vector2Int from = GridManager.Instance.GetGridPosition(fromWorldPos);

        foreach (Unit p in playerUnits)
        {
            if (p == null) continue;
            Vector2Int pp = GridManager.Instance.GetGridPosition(p.transform.position);
            int d = Mathf.Abs(from.x - pp.x) + Mathf.Abs(from.y - pp.y);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    public IEnumerator ShowEnemyAlert()
    {
        if (enemyAlertUI != null)
        {
            enemyAlertUI.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            enemyAlertUI.SetActive(false);
        }
    }

    private IEnumerator ShowRoundEndRoutine()
    {
        if (roundEndUI != null && IsInCombat())
        {
            roundEndUI.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            roundEndUI.SetActive(false);
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit.isPlayerOwned && !playerUnits.Contains(unit)) playerUnits.Add(unit);
        else if (!unit.isPlayerOwned && !enemyUnits.Contains(unit)) enemyUnits.Add(unit);
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit.isPlayerOwned) playerUnits.Remove(unit);
        else enemyUnits.Remove(unit);

        playerQueue.Remove(unit);
        if (unit == CurrentPlayerUnit)
        {
            CurrentPlayerUnit = null;
            if (unit.isPlayerOwned && !IsGameOver && IsInCombat()) StartCoroutine(AdvanceNextFrame());
        }

        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.RemoveUnit(unit);

        if (!unit.isPlayerOwned) UpdateCombatState();
        else if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
    }

    private void FocusTurnOn(Unit u, bool selectForCards)
    {
        if (u == null) return;

        if (!u.isPlayerOwned && FogOfWar.Instance != null && GridManager.Instance != null &&
            !FogOfWar.Instance.IsVisible(GridManager.Instance.GetGridPosition(u.transform.position)))
            return;

        if (TacticalCamera.Instance != null && IsInCombat()) TacticalCamera.Instance.FollowUnit(u.transform);
        if (selectForCards && CardManager.Instance != null) CardManager.Instance.SetActiveUnit(u);
    }

    public void StartPlayerTurn()
    {
        CurrentState = TurnState.PlayerTurn;
        CurrentPlayerUnit = null;

        if (IsInCombat())
        {
            BeginPlayerRound();
        }
        else
        {
            foreach (Unit u in playerUnits) { if (u != null) u.OnTurnStart(); }
            if (CardManager.Instance != null) CardManager.Instance.OnPlayerTurnStart();
        }
    }

    private void BeginPlayerRound()
    {
        playerQueue.Clear();
        Unit main = null;
        foreach (Unit u in playerUnits)
        {
            if (u == null) continue;
            if (u.isMainDrone) main = u; else playerQueue.Add(u);
        }
        if (main != null) playerQueue.Add(main);

        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.BuildNewRoundQueue();
        AdvancePlayerQueue();
    }

    private void AdvancePlayerQueue()
    {
        while (playerQueue.Count > 0)
        {
            Unit u = playerQueue[0];
            playerQueue.RemoveAt(0);
            if (u == null) continue;
            BeginUnitTurn(u);
            return;
        }
        CurrentPlayerUnit = null;
        StartEnemyTurn();
    }

    private void BeginUnitTurn(Unit u)
    {
        CurrentPlayerUnit = u;
        u.OnTurnStart();

        if (CardManager.Instance != null)
        {
            if (u.isMainDrone) CardManager.Instance.OnPlayerTurnStart();
            CardManager.Instance.SetActiveUnit(u);
        }

        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.SetActiveUnit(u);
        FocusTurnOn(u, false);
    }

    private IEnumerator AdvanceNextFrame()
    {
        yield return null;
        if (CurrentState == TurnState.PlayerTurn && CurrentPlayerUnit == null && !IsGameOver && IsInCombat())
            AdvancePlayerQueue();
    }

    public void AddToPlayerQueue(Unit u)
    {
        if (u != null && IsInCombat() && !playerQueue.Contains(u) && u != CurrentPlayerUnit) playerQueue.Add(u);
    }

    public void EndPlayerTurn()
    {
        if (CurrentState != TurnState.PlayerTurn) return;
        if (CardManager.Instance != null) CardManager.Instance.CancelTargeting();
        if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();

        if (!IsInCombat()) return;
        AdvancePlayerQueue();
    }

    private void StartEnemyTurn()
    {
        CurrentState = TurnState.EnemyTurn;
        if (CardManager.Instance != null) CardManager.Instance.CancelTargeting();
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        foreach (Unit enemy in new List<Unit>(enemyUnits))
        {
            if (enemy == null) continue;

            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake)
            {
                enemy.OnTurnStart();

                if (TurnOrderUI.Instance != null)
                    TurnOrderUI.Instance.SetActiveUnit(enemy);

                FocusTurnOn(enemy, false);
                yield return new WaitForSeconds(0.5f);

                yield return StartCoroutine(ai.TakeTurnRoutine());
            }
        }

        yield return StartCoroutine(ShowRoundEndRoutine());

        if (IsGameOver) yield break;
        TickRoundEffects();

        StartPlayerTurn();
    }

    private void TickRoundEffects()
    {
        if (!IsInCombat()) return;

        foreach (Unit u in new List<Unit>(playerUnits))
        {
            if (u == null) continue;
            if (u.tauntTurns > 0) u.tauntTurns--;

            DroneUnit d = u.GetComponent<DroneUnit>();
            if (d != null && d.isAlly) d.TickCharge();
        }
    }

    public bool IsGameOver { get; private set; }

    public void GameOver(bool showUI = true)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        CurrentState = TurnState.Busy;
        if (showUI) GameOverUI.Show();
    }

    public void SetBusy()
    {
        if (CurrentState != TurnState.Busy)
        {
            previousState = CurrentState;
            CurrentState = TurnState.Busy;
            if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
        }
    }

    public void ClearBusy()
    {
        if (CurrentState == TurnState.Busy)
        {
            CurrentState = previousState;
            if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
        }
    }
}
