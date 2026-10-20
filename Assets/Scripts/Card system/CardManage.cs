using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Her Tur Başında Tamamlanacak Kart Sayısı")]
    [SerializeField] private int minAttackCards = 2;
    [SerializeField] private int minDefenseCards = 2;

    [Header("Tamamlama Kartları (eksik kalınca bunlar verilir)")]
    [SerializeField] private CardData defaultAttackCard;
    [SerializeField] private CardData defaultDefenseCard;

    [Header("Oyun Başında Elde Olan Kartlar (isteğe bağlı)")]
    [SerializeField] private List<CardData> startingCards = new List<CardData>();

    public List<CardData> Hand { get; } = new List<CardData>();
    public event Action OnHandChanged;

    private CardData pendingAttackCard;   // Hedef seçilmeyi bekleyen saldırı kartı
    private int clickConsumedFrame = -1;

    public bool IsTargeting => pendingAttackCard != null;
    public CardData PendingCard => pendingAttackCard;

    // Harita tıklamalarını (hareket) engellemek için UnitMovement bunu kontrol eder
    public bool BlocksMapInput => IsTargeting || clickConsumedFrame == Time.frameCount;

    private void Awake()
    {
        Instance = this;
        Hand.AddRange(startingCards);
    }

    // ---------- Dışarıdan kullanılan fonksiyonlar ----------

    // Yerden toplanan / yaratıktan düşen kart elimize eklenir
    public void AddCard(CardData card)
    {
        if (card == null) return;
        Hand.Add(card);
        NotifyHandChanged();
    }

    // Oyuncu turu başında çağrılır: eksik kartları 2 attack + 2 defans olana kadar tamamlar
    public void OnPlayerTurnStart()
    {
        CancelTargeting();

        int attackCount = CountCards(CardType.Attack);
        int defenseCount = CountCards(CardType.Defense);

        while (attackCount < minAttackCards && defaultAttackCard != null)
        {
            Hand.Add(defaultAttackCard);
            attackCount++;
        }
        while (defenseCount < minDefenseCards && defaultDefenseCard != null)
        {
            Hand.Add(defaultDefenseCard);
            defenseCount++;
        }

        NotifyHandChanged();
    }

    public void RefreshUI() { NotifyHandChanged(); }

    public bool CanPlay(CardData card)
    {
        if (card == null) return false;

        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.CurrentState != TurnState.PlayerTurn || !tm.IsInCombat()) return false;

        Unit unit = GetActiveUnit();
        return unit != null && unit.currentCPU >= card.cpuCost;
    }

    // Karta tıklandığında CardView bunu çağırır
    public void OnCardClicked(CardData card)
    {
        if (!Hand.Contains(card)) return;

        // Hedef bekleyen karta tekrar tıklanırsa iptal et
        if (pendingAttackCard == card)
        {
            CancelTargeting();
            NotifyHandChanged();
            return;
        }

        CancelTargeting();
        if (!CanPlay(card)) { NotifyHandChanged(); return; }

        if (card.cardType == CardType.Defense) PlayDefense(card);
        else BeginTargeting(card);
    }

    public void CancelTargeting()
    {
        if (pendingAttackCard == null) return;
        pendingAttackCard = null;
        if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
        NotifyHandChanged();
    }

    // ---------- Kart oynama ----------

    private void PlayDefense(CardData card)
    {
        Unit unit = GetActiveUnit();
        if (unit == null) return;

        unit.UseCPU(card.cpuCost);
        unit.AddShield(card.shield);
        Hand.Remove(card);
        NotifyHandChanged();
    }

    private void BeginTargeting(CardData card)
    {
        List<Unit> targets = GetTargetsInRange(card);
        if (targets.Count == 0)
        {
            Debug.Log("Menzilde düşman yok!");
            NotifyHandChanged();
            return;
        }

        pendingAttackCard = card;

        // Vurulabilecek düşmanların karelerini mavi göster
        List<Node> nodes = new List<Node>();
        foreach (Unit t in targets)
        {
            Node n = GridManager.Instance.GetNode(GridManager.Instance.GetGridPosition(t.transform.position));
            if (n != null) nodes.Add(n);
        }
        HighlightManager.Instance.ShowRange(nodes);
        NotifyHandChanged();
    }

    private void Update()
    {
        if (!IsTargeting) return;

        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.CurrentState != TurnState.PlayerTurn || !tm.IsInCombat())
        {
            CancelTargeting();
            return;
        }

        // Sağ tık: iptal
        if (Input.GetMouseButtonDown(1))
        {
            clickConsumedFrame = Time.frameCount;
            CancelTargeting();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // Kart/UI üzerine tıklandıysa karışma
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            clickConsumedFrame = Time.frameCount;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int clickedPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            foreach (Unit enemy in GetTargetsInRange(pendingAttackCard))
            {
                if (GridManager.Instance.GetGridPosition(enemy.transform.position) == clickedPos)
                {
                    ExecuteAttack(pendingAttackCard, enemy);
                    return;
                }
            }

            // Boş bir yere tıklandı: seçimi iptal et
            CancelTargeting();
        }
    }

    private void ExecuteAttack(CardData card, Unit target)
    {
        Unit attacker = GetActiveUnit();
        if (attacker == null || target == null) { CancelTargeting(); return; }

        attacker.UseCPU(card.cpuCost);
        Hand.Remove(card);

        pendingAttackCard = null;
        HighlightManager.Instance.ClearHighlights();

        // Hasar en sonda: düşman ölürse savaş bitebilir
        target.TakeDamage(card.damage);

        NotifyHandChanged();
    }

    // ---------- Yardımcılar ----------

    // Şimdilik kartı oynayan birim: listedeki ilk oyuncu birimi (TurnOrderUI'daki aktif birimle aynı)
    private Unit GetActiveUnit()
    {
        if (TurnManager.Instance == null) return null;
        foreach (Unit u in TurnManager.Instance.playerUnits)
        {
            if (u != null) return u;
        }
        return null;
    }

    private List<Unit> GetTargetsInRange(CardData card)
    {
        List<Unit> result = new List<Unit>();
        Unit attacker = GetActiveUnit();
        if (attacker == null) return result;

        Vector2Int from = GridManager.Instance.GetGridPosition(attacker.transform.position);

        foreach (Unit enemy in TurnManager.Instance.enemyUnits)
        {
            if (enemy == null) continue;

            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai == null || !ai.isAwake) continue;

            Vector2Int to = GridManager.Instance.GetGridPosition(enemy.transform.position);
            int dist = Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
            if (dist <= card.range) result.Add(enemy);
        }
        return result;
    }

    private int CountCards(CardType type)
    {
        int count = 0;
        foreach (CardData c in Hand) { if (c != null && c.cardType == type) count++; }
        return count;
    }

    private void NotifyHandChanged() { OnHandChanged?.Invoke(); }
}