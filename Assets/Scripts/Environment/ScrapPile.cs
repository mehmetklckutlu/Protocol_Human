using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ScrapPile : MonoBehaviour
{
    [Header("Araştırma")]
    [Tooltip("Araştırmanın süresi (saniye). Hurda animasyonu bu süreye göre hızlanır/yavaşlar.")]
    [SerializeField, Min(0.1f)] private float searchDuration = 2f;
    [SerializeField, Min(0)] private int scrapMin = 1;
    [SerializeField, Min(0)] private int scrapMax = 5;
    [Tooltip("Araştırmada blueprint bulma ihtimali (0.1 = %10).")]
    [SerializeField, Range(0f, 1f)] private float blueprintChance = 0.1f;
    [Tooltip("Bulunabilecek blueprint kartları (rastgele biri seçilir).")]
    [SerializeField] private List<CardData> blueprints = new List<CardData>();
    [Tooltip("Açık: sadece savaş dışında araştırılabilir.")]
    [SerializeField] private bool onlyOutOfCombat = true;

    [Header("Görünüm")]
    [Tooltip("Araştırılabilirken yığın bu renge doğru yanıp söner.")]
    [SerializeField] private Color highlightColor = new Color(0.55f, 1f, 0.6f);
    [Tooltip("Fare yığının üstündeyken kullanılan renk.")]
    [SerializeField] private Color hoverColor = new Color(0.35f, 1f, 0.45f);
    [SerializeField, Min(0f)] private float pulseSpeed = 4f;

    [Header("Animasyon")]
    [Tooltip("Boş bırakılırsa bu objede veya çocuklarında Animator aranır.")]
    [SerializeField] private Animator animator;

    public Vector2Int PileCell { get; private set; }

    private GridManager gm;
    private SpriteRenderer body;
    private Color baseColor = Color.white;
    private bool searching;
    private bool ready;

    private void Awake()
    {
        if (animator == null) animator = AnimUtil.Find(this);
    }

    private void Start()
    {
        gm = GridManager.Instance;
        if (gm == null) { enabled = false; return; }

        PileCell = gm.GetGridPosition(transform.position);
        transform.position = gm.GetWorldPosition(PileCell);

        Node n = gm.GetNode(PileCell);
        if (n != null) n.isWalkable = false;

        body = GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        if (body == null)
        {
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = RuntimeArt.Square;
            body.color = new Color(0.45f, 0.35f, 0.25f);
            transform.localScale = new Vector3(gm.CellSize * 0.8f, gm.CellSize * 0.8f, 1f);
        }
        body.sortingOrder = 3;
        baseColor = body.color;

        ScrapUI.Instance.enabled = true;
        ready = true;
    }

    private void Update()
    {
        if (!ready || searching) return;

        bool visible = FogOfWar.Instance == null || FogOfWar.Instance.IsVisible(PileCell);
        bool usable = visible && CanSearchNow();
        bool hover = usable && MouseCell() == PileCell && !PointerOverUI();

        if (!usable) body.color = baseColor;
        else if (hover) body.color = hoverColor;
        else
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            body.color = Color.Lerp(baseColor, highlightColor, pulse);
        }

        if (hover && Input.GetMouseButtonDown(0))
        {
            if (CardManager.Instance != null && CardManager.Instance.BlocksMapInput) return;
            OnClicked();
        }
    }

    private bool CanSearchNow()
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.CurrentState != TurnState.PlayerTurn) return false;
        if (onlyOutOfCombat && tm.IsInCombat()) return false;
        return true;
    }

    private Vector2Int MouseCell()
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector2Int(int.MinValue, int.MinValue);
        Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
        return gm.GetGridPosition(w);
    }

    private static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsNextToPile(Vector2Int cell)
    {
        int dx = Mathf.Abs(cell.x - PileCell.x);
        int dy = Mathf.Abs(cell.y - PileCell.y);
        return Mathf.Max(dx, dy) == 1;
    }

    private Unit PickSearcher()
    {
        TurnManager tm = TurnManager.Instance;
        Unit active = CardManager.Instance != null ? CardManager.Instance.GetActiveUnit() : null;
        if (active != null && !active.IsDead && active.GetComponent<UnitMovement>() != null) return active;

        Unit best = null;
        float bestDist = float.MaxValue;
        foreach (Unit u in tm.playerUnits)
        {
            if (u == null || u.IsDead || u.GetComponent<UnitMovement>() == null) continue;
            float d = Vector2.Distance(u.transform.position, transform.position);
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }

    private void OnClicked()
    {
        Unit u = PickSearcher();
        if (u == null) return;

        Vector2Int cell = gm.GetGridPosition(u.transform.position);
        if (IsNextToPile(cell))
        {
            StartSearch();
            return;
        }

        UnitMovement move = u.GetComponent<UnitMovement>();
        if (move == null || !move.TryMoveNextTo(PileCell, StartSearch))
            FloatingText.Spawn(transform.position, "ULASILAMIYOR", new Color(1f, 0.6f, 0.3f));
    }

    public void StartSearch()
    {
        if (searching || this == null) return;
        StartCoroutine(SearchRoutine());
    }

    private IEnumerator SearchRoutine()
    {
        searching = true;
        body.color = baseColor;
        TurnManager.Instance.SetBusy();
        ScrapUI ui = ScrapUI.Instance;
        ui.BeginSearch();

        bool animated = AnimUtil.SetBool(animator, AnimUtil.Search, true);
        if (animated)
        {
            float len = AnimUtil.LongestClipLength(animator);
            if (len > 0f) animator.speed = len / searchDuration;
        }

        float t = 0f;
        while (t < searchDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / searchDuration);
            ui.SetProgress(p);
            if (!animated) body.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - p));
            yield return null;
        }

        int scrap = Random.Range(scrapMin, Mathf.Max(scrapMin, scrapMax) + 1);
        ScrapInventory.Add(scrap);
        string msg = "+" + scrap + " hurda bulundu";

        if (blueprints.Count > 0 && CardManager.Instance != null && Random.value < blueprintChance)
        {
            CardData bp = blueprints[Random.Range(0, blueprints.Count)];
            if (bp != null)
            {
                CardManager.Instance.AddCard(bp);
                msg += "\n<color=#7CFF8A>Blueprint: " + bp.cardName + "</color>";
            }
        }

        Node n = gm.GetNode(PileCell);
        if (n != null) n.isWalkable = true;

        TurnManager.Instance.ClearBusy();
        ui.EndSearch();
        ui.ShowResult(msg);
        Destroy(gameObject);
    }
}
