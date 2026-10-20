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
        else if (card.cardType == CardType.Summon) PlaySummon(card);
        else BeginTargeting(card);
    }

    public void CancelTargeting()
    {
        if (pendingAttackCard == null) return;
        pendingAttackCard = null;
        ClearBarrelPreview();
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
        List<ExplosiveBarrel> barrels = GetBarrelsInRange(card);
        if (targets.Count == 0 && barrels.Count == 0)
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
        foreach (ExplosiveBarrel b in barrels)
        {
            Node n = GridManager.Instance.GetNode(b.Cell);
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

        UpdateBarrelPreview();

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

            foreach (ExplosiveBarrel barrel in GetBarrelsInRange(pendingAttackCard))
            {
                if (barrel.Cell == clickedPos)
                {
                    ExecuteBarrelAttack(pendingAttackCard, barrel);
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
        ClearBarrelPreview();
        HighlightManager.Instance.ClearHighlights();

        // Hasar en sonda: düşman ölürse savaş bitebilir
        target.TakeDamage(card.damage);

        NotifyHandChanged();
    }

    private void ExecuteBarrelAttack(CardData card, ExplosiveBarrel barrel)
    {
        Unit attacker = GetActiveUnit();
        if (attacker == null || barrel == null) { CancelTargeting(); return; }

        attacker.UseCPU(card.cpuCost);
        Hand.Remove(card);

        pendingAttackCard = null;
        ClearBarrelPreview();
        HighlightManager.Instance.ClearHighlights();

        barrel.Explode();
        NotifyHandChanged();
    }

    // ---------- Müttefik çağırma ----------

    private void PlaySummon(CardData card)
    {
        Unit caster = GetActiveUnit();
        if (caster == null) return;

        Vector2Int from = GridManager.Instance.GetGridPosition(caster.transform.position);
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        Vector2Int? spot = null;
        foreach (Vector2Int d in dirs)
        {
            Vector2Int c = from + d;
            Node n = GridManager.Instance.GetNode(c);
            if (n == null || !n.isWalkable || IsOccupied(c)) continue;
            spot = c;
            break;
        }

        if (spot == null) { Debug.Log("Müttefik çağıracak boş kare yok!"); NotifyHandChanged(); return; }

        caster.UseCPU(card.cpuCost);
        Hand.Remove(card);

        Vector3 pos = GridManager.Instance.GetWorldPosition(spot.Value);
        GameObject ally;
        if (card.allyPrefab != null)
        {
            ally = Instantiate(card.allyPrefab, pos, Quaternion.identity);
        }
        else
        {
            // Geçici kutu müttefik: yerinde durur, düşmanların hedefi olabilir
            ally = new GameObject("Müttefik");
            ally.transform.position = pos;
            ally.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            SpriteRenderer sr = ally.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeArt.Square;
            sr.color = new Color(0.3f, 0.85f, 0.45f);
            sr.sortingOrder = 3;
            Unit u = ally.AddComponent<Unit>();
            u.isPlayerOwned = true;
            u.maxHealth = card.allyHealth;
            u.currentHealth = card.allyHealth;
        }
        NotifyHandChanged();
    }

    private bool IsOccupied(Vector2Int cell)
    {
        TurnManager tm = TurnManager.Instance;
        foreach (Unit u in tm.playerUnits)
            if (u != null && GridManager.Instance.GetGridPosition(u.transform.position) == cell) return true;
        foreach (Unit u in tm.enemyUnits)
            if (u != null && GridManager.Instance.GetGridPosition(u.transform.position) == cell) return true;
        return false;
    }

    // ---------- Varil hedefleme ----------

    private ExplosiveBarrel hoveredBarrel;

    private List<ExplosiveBarrel> GetBarrelsInRange(CardData card)
    {
        List<ExplosiveBarrel> result = new List<ExplosiveBarrel>();
        Unit attacker = GetActiveUnit();
        if (attacker == null) return result;

        Vector2Int from = GridManager.Instance.GetGridPosition(attacker.transform.position);
        foreach (ExplosiveBarrel b in ExplosiveBarrel.All)
        {
            if (b == null) continue;
            if (FogOfWar.Instance != null && !FogOfWar.Instance.IsVisible(b.Cell)) continue;
            int dist = Mathf.Abs(from.x - b.Cell.x) + Mathf.Abs(from.y - b.Cell.y);
            if (dist <= card.range) result.Add(b);
        }
        return result;
    }

    // Fare menzildeki varilin üstündeyse patlama alanını göster
    private void UpdateBarrelPreview()
    {
        ExplosiveBarrel target = null;
        if (pendingAttackCard != null && Camera.main != null &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            Vector2Int cell = GridManager.Instance.GetGridPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            foreach (ExplosiveBarrel b in GetBarrelsInRange(pendingAttackCard))
                if (b.Cell == cell) { target = b; break; }
        }

        if (target == hoveredBarrel) return;
        if (hoveredBarrel != null) hoveredBarrel.HidePreview();
        hoveredBarrel = target;
        if (hoveredBarrel != null) hoveredBarrel.ShowPreview();
    }

    private void ClearBarrelPreview()
    {
        if (hoveredBarrel != null) hoveredBarrel.HidePreview();
        hoveredBarrel = null;
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

            // Görüş dışındaki (sisli) düşman hedeflenemez
            if (FogOfWar.Instance != null && !FogOfWar.Instance.IsVisible(to)) continue;
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