
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
    public GameObject enemyAlertUI; // Uyanma yazısı (Düşman Hareketi)
    public GameObject endTurnButton; // Turu Bitir Butonu
    public GameObject roundEndUI; // Tur Bitti yazısı

    private TurnState previousState;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartPlayerTurn();
        UpdateCombatState(); // Oyun başlarken butonları/UI'ı duruma göre gizler
    }

    public bool IsInCombat()
    {
        foreach (Unit enemy in enemyUnits)
        {
            if (enemy == null) continue;
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake) return true; // Biri bile uyanıksa savaş modundayız!
        }
        return false;
    }

    public void UpdateCombatState()
    {
        bool inCombat = IsInCombat();

        if (endTurnButton != null)
            endTurnButton.SetActive(inCombat);

        foreach (Unit player in playerUnits)
        {
            if (player != null) player.OnTurnStart();
        }
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

    // YENİ EKLENEN TUR BİTTİ YAZISI FONKSİYONU
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
    }

    public void StartPlayerTurn()
    {
        CurrentState = TurnState.PlayerTurn;

        // Yeni tur (Round) başladığında UI'a listeyi baştan oluşturmasını söylüyoruz
        if (TurnOrderUI.Instance != null && IsInCombat())
            TurnOrderUI.Instance.BuildNewRoundQueue();

        foreach (Unit u in playerUnits) { if (u != null) u.OnTurnStart(); }

        // Oyuncuyu aktif olarak göster
        if (TurnOrderUI.Instance != null && playerUnits.Count > 0)
            TurnOrderUI.Instance.SetActiveUnit(playerUnits[0]);
    }

    public void EndPlayerTurn()
    {
        if (CurrentState != TurnState.PlayerTurn) return;
        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
        CurrentState = TurnState.EnemyTurn;
        foreach (Unit u in enemyUnits) { if (u != null) u.OnTurnStart(); }
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        foreach (Unit enemy in enemyUnits)
        {
            if (enemy == null) continue;

            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake)
            {
                // UI'da sıradaki düşmanı aktif et (Önceki ikon silinip sola kayacak)
                if (TurnOrderUI.Instance != null)
                    TurnOrderUI.Instance.SetActiveUnit(enemy);

                yield return StartCoroutine(ai.TakeTurnRoutine());
            }
        }

        // TÜM DÜŞMANLAR BİTTİ - TUR (ROUND) BAŞA DÖNÜYOR
        yield return StartCoroutine(ShowRoundEndRoutine());

        StartPlayerTurn();
    }

    public void SetBusy()
    {
        if (CurrentState != TurnState.Busy)
        {
            previousState = CurrentState;
            CurrentState = TurnState.Busy;
        }
    }

    public void ClearBusy()
    {
        if (CurrentState == TurnState.Busy)
            CurrentState = previousState;
    }
}