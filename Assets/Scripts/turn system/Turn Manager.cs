
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

    // Oyuncu tarafında her drone'un kendi turu var: sıra bu listede ilerler
    private readonly List<Unit> playerQueue = new List<Unit>();
    public Unit CurrentPlayerUnit { get; private set; }

    private TurnState previousState;
    private bool wasInCombat = false; // Savaşa GEÇİŞİ yakalamak için

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

        // Sadece Keşif -> Savaş geçişinde bir kere çalışır.
        // (Savaş sırasında 2. düşman uyanınca oyuncunun hareketi yeniden dolmasın)
        if (inCombat && !wasInCombat)
        {
            BeginPlayerRound();
        }

        // Savaştan -> Keşfe dönüş: hiç uyanık düşman kalmadı
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

        // Savaş yazılarını ve tur sırası arayüzünü kapat
        if (enemyAlertUI != null) enemyAlertUI.SetActive(false);
        if (roundEndUI != null) roundEndUI.SetActive(false);
        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.ClearAll();

        // Çağrılan yardımcı drone'lar savaşla birlikte kapanır
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

        // Oyuncuların hareketini/CPU'sunu doldur (UI çubukları da güncellenir, CPU barı gizlenir)
        foreach (Unit player in playerUnits)
        {
            if (player != null) player.OnTurnStart();
        }
    }

    // Verilen konuma en yakın (grid mesafesine göre) yaşayan oyuncu birimini döndürür
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

        playerQueue.Remove(unit);
        if (unit == CurrentPlayerUnit)
        {
            CurrentPlayerUnit = null;
            // Sırası gelen drone yok olduysa (kendini patlattı vb.) sıra bir sonrakine geçsin
            if (unit.isPlayerOwned && !IsGameOver && IsInCombat()) StartCoroutine(AdvanceNextFrame());
        }

        // Ölen birimin ikonu tur sırasından silinir
        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.RemoveUnit(unit);

        // Son uyanık düşman öldüyse savaş biter
        if (!unit.isPlayerOwned) UpdateCombatState();
        else if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
    }

    // Sırası gelen birime kamera gider; oyuncu tarafıysa kartları da açılır (tıklanmış gibi)
    private void FocusTurnOn(Unit u, bool selectForCards)
    {
        if (u == null) return;

        // Sisli (görünmeyen) düşmana kamera gitmesin
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
            // Keşif modu: herkes serbest
            foreach (Unit u in playerUnits) { if (u != null) u.OnTurnStart(); }
            if (CardManager.Instance != null) CardManager.Instance.OnPlayerTurnStart();
        }
    }

    // Oyuncu tarafının turu: önce yardımcı drone'lar (çağrılma sırasıyla), en son ana drone
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

    // Sıradaki drone'a geçer; kimse kalmadıysa düşman turu başlar
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
            if (u.isMainDrone) CardManager.Instance.OnPlayerTurnStart(); // tur kartları tamamlanır
            CardManager.Instance.SetActiveUnit(u);                       // kartları bu drone'un eli açılır
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

    // Çağrılan yeni drone, bu turun sonunda sıraya eklenir
    public void AddToPlayerQueue(Unit u)
    {
        if (u != null && IsInCombat() && !playerQueue.Contains(u) && u != CurrentPlayerUnit) playerQueue.Add(u);
    }

    // "Turu Bitir": sıradaki drone'a geçer
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
        foreach (Unit u in enemyUnits) { if (u != null) u.OnTurnStart(); }
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        foreach (Unit enemy in new List<Unit>(enemyUnits)) // kopya: tur sırasında biri ölürse liste bozulmasın
        {
            if (enemy == null) continue;

            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake)
            {
                // UI'da sıradaki düşmanı aktif et (Önceki ikon silinip sola kayacak)
                if (TurnOrderUI.Instance != null)
                    TurnOrderUI.Instance.SetActiveUnit(enemy);

                FocusTurnOn(enemy, false);
                yield return new WaitForSeconds(0.5f); // kamera düşmana gelsin

                yield return StartCoroutine(ai.TakeTurnRoutine());
            }
        }

        // TÜM DÜŞMANLAR BİTTİ - TUR (ROUND) BAŞA DÖNÜYOR
        yield return StartCoroutine(ShowRoundEndRoutine());

        if (IsGameOver) yield break;
        TickRoundEffects();

        StartPlayerTurn();
    }

    // Her tam tur sonunda: taunt süresi azalır, dost drone'ların şarjı azalır (0 olunca ölür)
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

    // Ana drone yok olunca çağrılır
    public void GameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        CurrentState = TurnState.Busy;
        GameOverUI.Show();
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