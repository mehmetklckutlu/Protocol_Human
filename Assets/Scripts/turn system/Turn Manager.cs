
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
    private bool wasInCombat = false; // Savaşa GEÇİŞİ yakalamak için

    private void Awake()
    {
        Instance = this;
    }

    // TEST: K tuşu uyanık bir düşmanı öldürür. Savaştan çıkışı denemek için; işin bitince sil.
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            foreach (Unit enemy in new List<Unit>(enemyUnits))
            {
                if (enemy == null) continue;
                EnemyAI ai = enemy.GetComponent<EnemyAI>();
                if (ai != null && ai.isAwake) { enemy.TakeDamage(9999); break; }
            }
        }
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
            foreach (Unit player in playerUnits)
            {
                if (player != null) player.OnTurnStart();
            }

            // Tur sırası UI'ını savaş başlarken kur (oyuncu ikonları + aktif portre)
            if (TurnOrderUI.Instance != null)
            {
                TurnOrderUI.Instance.BuildNewRoundQueue();
                if (playerUnits.Count > 0 && playerUnits[0] != null)
                    TurnOrderUI.Instance.SetActiveUnit(playerUnits[0]);
            }
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

        // Ölen birimin ikonu tur sırasından silinir
        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.RemoveUnit(unit);

        // Son uyanık düşman öldüyse savaş biter
        if (!unit.isPlayerOwned) UpdateCombatState();
    }

    public void StartPlayerTurn()
    {
        CurrentState = TurnState.PlayerTurn;

        // Yeni tur (Round) başladığında UI'a listeyi baştan oluşturmasını söylüyoruz
        if (TurnOrderUI.Instance != null && IsInCombat())
            TurnOrderUI.Instance.BuildNewRoundQueue();

        foreach (Unit u in playerUnits) { if (u != null) u.OnTurnStart(); }

        // Eksik kartları 2 attack + 2 defans olana kadar tamamla
        if (CardManager.Instance != null) CardManager.Instance.OnPlayerTurnStart();

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