using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Ana Drone: Her Tur Başında Elde Olması Gereken Kartlar")]
    [Tooltip("Bu kartlar her tur başında, elde yoksa yeniden verilir (bedelsiz).")]
    [SerializeField] private List<CardData> turnCards = new List<CardData>();

    [Header("Ana Drone: Oyun Başında Elde Olan Kartlar (isteğe bağlı)")]
    [SerializeField] private List<CardData> startingCards = new List<CardData>();

    // Ana drone'un eli: kullanılan kart elden gider
    public List<CardData> Hand { get; } = new List<CardData>();
    public event Action OnHandChanged;

    private Unit activeUnit;               // Kartları oynayan seçili birim
    private CardData pendingCard;          // Hedef seçilmeyi bekleyen saldırı kartı
    private int clickConsumedFrame = -1;
    private ExplosiveBarrel hoveredBarrel;

    public bool IsTargeting => pendingCard != null;
    public CardData PendingCard => pendingCard;

    // Harita tıklamalarını (hareket) engellemek için UnitMovement bunu kontrol eder
    public bool BlocksMapInput => IsTargeting || clickConsumedFrame == Time.frameCount;

    private void Awake()
    {
        Instance = this;
        Hand.AddRange(startingCards);
    }

    private void Start()
    {
        ScrapUI.Instance.enabled = true;              // hurda sayacı arayüzünü hazırla
        ScrapInventory.OnChanged += NotifyHandChanged; // hurda değişince kartların aktifliği güncellensin
    }

    private void OnDestroy()
    {
        ScrapInventory.OnChanged -= NotifyHandChanged;
    }

    // ---------- Aktif birim ve el ----------

    private Unit GetMainUnit()
    {
        if (TurnManager.Instance == null) return null;
        Unit first = null;
        foreach (Unit u in TurnManager.Instance.playerUnits)
        {
            if (u == null) continue;
            if (u.isMainDrone) return u;
            if (first == null && u.usesCpu) first = u;
        }
        return first;
    }

    public Unit GetActiveUnit()
    {
        TurnManager tm = TurnManager.Instance;
        if (tm != null && tm.IsInCombat() && tm.CurrentPlayerUnit != null) return tm.CurrentPlayerUnit; // sırası gelen
        if (activeUnit != null && activeUnit.isPlayerOwned) return activeUnit;
        return GetMainUnit();
    }

    public void SetActiveUnit(Unit u)
    {
        if (u == activeUnit) return;
        CancelTargeting();
        activeUnit = u;
        NotifyHandChanged();
    }

    // Arayüzde gösterilen el: seçili birim dost drone ise onun kiti, değilse ana drone'un eli
    public List<CardData> CurrentHand
    {
        get
        {
            Unit u = GetActiveUnit();
            if (u != null)
            {
                DroneUnit d = u.GetComponent<DroneUnit>();
                if (d != null && d.isAlly) return d.kit;
            }
            return Hand;
        }
    }

    // ---------- Dışarıdan kullanılan fonksiyonlar ----------

    // Yerden toplanan / yaratıktan düşen kart ana drone'un eline eklenir
    public void AddCard(CardData card)
    {
        if (card == null) return;
        Hand.Add(card);
        NotifyHandChanged();
    }

    // Oyuncu turu başında çağrılır: tur kartları elde yoksa verilir
    public void OnPlayerTurnStart()
    {
        CancelTargeting();
        activeUnit = null;

        foreach (CardData c in turnCards)
        {
            if (c != null && !Hand.Contains(c)) Hand.Add(c);
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
        if (unit == null) return false;

        if (unit.usesCpu) { if (unit.currentCPU < card.cpuCost) return false; }
        else if (unit.cardsPlayedThisTurn >= unit.cardsPerTurn) return false;

        if (card.scrapCost > ScrapInventory.Count) return false;
        if (card.cardType == CardType.Summon && card.summonPrefab == null) return false;
        return true;
    }

    // Karta tıklandığında CardView bunu çağırır
    public void OnCardClicked(CardData card)
    {
        if (!CurrentHand.Contains(card)) return;

        // Hedef bekleyen karta tekrar tıklanırsa iptal et
        if (pendingCard == card)
        {
            CancelTargeting();
            NotifyHandChanged();
            return;
        }

        CancelTargeting();
        if (!CanPlay(card)) { NotifyHandChanged(); return; }

        switch (card.cardType)
        {
            case CardType.Attack: BeginTargeting(card); break;
            case CardType.Summon: PlaySummon(card); break;
            default: PlayImmediate(card); break;
        }
    }

    public void CancelTargeting()
    {
        if (pendingCard == null) return;
        pendingCard = null;
        ClearBarrelPreview();
        if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
        NotifyHandChanged();
    }

    // ---------- Bedel ödeme ----------

    // CPU / kart hakkı / hurda öder, ana drone'un kartıysa elden çıkarır
    private void Pay(CardData card, Unit unit)
    {
        bool fromMainHand = ReferenceEquals(CurrentHand, Hand);

        if (card.scrapCost > 0) ScrapInventory.TrySpend(card.scrapCost);
        if (unit.usesCpu) unit.UseCPU(card.cpuCost);
        else unit.cardsPlayedThisTurn++;

        if (fromMainHand) Hand.Remove(card);
    }

    // ---------- Anında kullanılan kartlar (kalkan, taunt, CPU, şarj, patlatma) ----------

    private void PlayImmediate(CardData card)
    {
        Unit unit = GetActiveUnit();
        if (unit == null) return;

        Pay(card, unit);
        Vector3 pos = unit.transform.position;

        if (card.shield > 0) unit.AddShield(card.shield);

        if (card.tauntTurns > 0)
        {
            unit.tauntTurns = Mathf.Max(unit.tauntTurns, card.tauntTurns);
            FloatingText.Spawn(pos, "TAUNT", new Color(1f, 0.6f, 0.2f), 1);
        }

        if (card.cpuRestore > 0)
        {
            // CPU her zaman ana drone'a gider
            foreach (Unit u in new List<Unit>(TurnManager.Instance.playerUnits))
            {
                if (u == null || !u.usesCpu) continue;
                u.RestoreCPU(card.cpuRestore);
                FloatingText.Spawn(u.transform.position, "+" + card.cpuRestore + " CPU", new Color(0.4f, 1f, 1f), 1);
            }
        }

        if (card.chargeAll > 0)
        {
            foreach (Unit u in new List<Unit>(TurnManager.Instance.playerUnits))
            {
                if (u == null) continue;
                DroneUnit d = u.GetComponent<DroneUnit>();
                if (d != null && d.isAlly) d.AddCharge(card.chargeAll);
            }
        }

        // En sonda: birim yok olabilir (ana drone ise oyun kaybedilir)
        if (card.selfDestruct)
        {
            Vector2Int c = GridManager.Instance.GetGridPosition(unit.transform.position);
            BlastUtil.Explode(c, card.blastRadius, card.blastDamage, unit);
            unit.Kill();
        }

        NotifyHandChanged();
    }

    // ---------- Saldırı kartları ----------

    private void BeginTargeting(CardData card)
    {
        List<Unit> targets = GetTargetsInRange(card);
        List<ExplosiveBarrel> barrels = GetBarrelsInRange(card);
        if (targets.Count == 0 && barrels.Count == 0)
        {
            Debug.Log("Menzilde hedef yok!");
            NotifyHandChanged();
            return;
        }

        pendingCard = card;

        // Vurulabilecek hedeflerin karelerini mavi göster
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

            foreach (Unit enemy in GetTargetsInRange(pendingCard))
            {
                if (GridManager.Instance.GetGridPosition(enemy.transform.position) == clickedPos)
                {
                    ExecuteAttack(pendingCard, enemy);
                    return;
                }
            }

            foreach (ExplosiveBarrel barrel in GetBarrelsInRange(pendingCard))
            {
                if (barrel.Cell == clickedPos)
                {
                    ExecuteBarrelAttack(pendingCard, barrel);
                    return;
                }
            }

            // Boş bir yere tıklandı: seçimi iptal et
            CancelTargeting();
        }
    }

    // İsabet şansını atar. Iskalarsa yazı gösterir ve false döner.
    private bool RollHit(CardData card, Vector3 pos)
    {
        if (UnityEngine.Random.value > card.hitChance)
        {
            FloatingText.Spawn(pos, "ISKA", new Color(0.8f, 0.8f, 0.8f));
            return false;
        }
        return true;
    }

    private void ExecuteAttack(CardData card, Unit target)
    {
        Unit attacker = GetActiveUnit();
        if (attacker == null || target == null) { CancelTargeting(); return; }

        Pay(card, attacker);
        pendingCard = null;
        ClearBarrelPreview();
        HighlightManager.Instance.ClearHighlights();

        if (RollHit(card, target.transform.position))
        {
            // Stun, hasardan önce uygulanır (hasar hedefi öldürebilir)
            if (card.stunChance > 0f && UnityEngine.Random.value <= card.stunChance)
            {
                target.stunTurns = Mathf.Max(target.stunTurns, card.stunTurns);
                FloatingText.Spawn(target.transform.position, "STUN!", new Color(1f, 0.9f, 0.3f), 1);
            }
            target.TakeDamage(card.damage);
        }

        NotifyHandChanged();
    }

    private void ExecuteBarrelAttack(CardData card, ExplosiveBarrel barrel)
    {
        Unit attacker = GetActiveUnit();
        if (attacker == null || barrel == null) { CancelTargeting(); return; }

        Pay(card, attacker);
        pendingCard = null;
        ClearBarrelPreview();
        HighlightManager.Instance.ClearHighlights();

        if (RollHit(card, barrel.transform.position)) barrel.Explode();
        NotifyHandChanged();
    }

    // ---------- Drone çağırma (blueprint) ----------

    private void PlaySummon(CardData card)
    {
        Unit caster = GetActiveUnit();
        if (caster == null) return;

        Vector2Int from = GridManager.Instance.GetGridPosition(caster.transform.position);
        Vector2Int[] dirs =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
            new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
        };

        Vector2Int? spot = null;
        foreach (Vector2Int d in dirs)
        {
            Vector2Int c = from + d;
            Node n = GridManager.Instance.GetNode(c);
            if (n == null || !n.isWalkable || IsOccupied(c)) continue;
            spot = c;
            break;
        }

        if (spot == null)
        {
            Debug.Log("Drone çağıracak boş kare yok!");
            NotifyHandChanged();
            return;
        }

        Pay(card, caster);

        GameObject go = Instantiate(card.summonPrefab, GridManager.Instance.GetWorldPosition(spot.Value), Quaternion.identity);
        Unit ally = go.GetComponent<Unit>();
        if (ally != null)
        {
            ally.isPlayerOwned = true;
            TurnManager.Instance.RegisterUnit(ally);
            TurnManager.Instance.AddToPlayerQueue(ally); // tur sırasına otomatik eklenir
            if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.AddUnitToQueue(ally);
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
        if (pendingCard != null && Camera.main != null &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            Vector2Int cell = GridManager.Instance.GetGridPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            foreach (ExplosiveBarrel b in GetBarrelsInRange(pendingCard))
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

    private void NotifyHandChanged() { OnHandChanged?.Invoke(); }
}
