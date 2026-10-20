using System.Collections;
using UnityEngine;

// Hurda yığını: yanındaki parlayan karoya gelince "Araştır" paneli açılır.
// Araştırma süresince hurda (min-max arası) ve düşük ihtimalle müttefik çağırma kartı bulunur.
// Şimdilik kutu görseli kullanır; kendi sprite'ını bu objedeki SpriteRenderer'a atayabilirsin.
public class ScrapPile : MonoBehaviour
{
    [Header("Araştırma")]
    [SerializeField] private float searchDuration = 3f;
    [SerializeField, Min(0)] private int scrapMin = 1;
    [SerializeField, Min(0)] private int scrapMax = 5;
    [Tooltip("Araştırmada müttefik çağırma kartı bulma ihtimali (0.1 = %10).")]
    [SerializeField, Range(0f, 1f)] private float allyCardChance = 0.1f;
    [Tooltip("Bulunacak müttefik çağırma kartı (Assets/Cards/MuttefikCagir).")]
    [SerializeField] private CardData allyCard;
    [Tooltip("Açık: yığın bir kez araştırılınca tükenir.")]
    [SerializeField] private bool searchOnlyOnce = true;
    [Tooltip("Açık: sadece savaş dışında araştırılabilir.")]
    [SerializeField] private bool onlyOutOfCombat = true;

    [Header("Etkileşim Karosu")]
    [Tooltip("Açık: etkileşim karosunu aşağıdaki değerden al. Kapalı: yığının yanındaki yürünebilir ilk kare otomatik seçilir.")]
    [SerializeField] private bool useManualCell = false;
    [SerializeField] private Vector2Int manualCell;

    [Header("Görünüm")]
    [SerializeField] private Color bodyColor = new Color(0.45f, 0.35f, 0.25f);
    [SerializeField] private Color depletedColor = new Color(0.22f, 0.2f, 0.2f);
    [SerializeField] private Color glowColor = new Color(0.2f, 1f, 0.35f);

    public Vector2Int PileCell { get; private set; }
    public Vector2Int InteractCell { get; private set; }

    private GridManager gm;
    private SpriteRenderer body;
    private SpriteRenderer glow;
    private SpriteRenderer starIcon;
    private bool depleted;
    private bool searching;
    private bool ready;

    private void Start()
    {
        gm = GridManager.Instance;
        if (gm == null) { enabled = false; return; }

        PileCell = gm.GetGridPosition(transform.position);
        transform.position = gm.GetWorldPosition(PileCell);

        Node n = gm.GetNode(PileCell);
        if (n != null) n.isWalkable = false;   // yığının üstünden yürünmez

        InteractCell = useManualCell ? manualCell : FindInteractCell();

        body = GetComponent<SpriteRenderer>();
        if (body == null)
        {
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = RuntimeArt.Square;
            body.color = bodyColor;
            transform.localScale = new Vector3(gm.CellSize * 0.8f, gm.CellSize * 0.8f, 1f);
        }
        body.sortingOrder = 3;

        Vector3 gp = gm.GetWorldPosition(InteractCell);
        glow = RuntimeArt.MakeSquare(null, "ScrapGlow", new Color(glowColor.r, glowColor.g, glowColor.b, 0.35f), gm.CellSize * 0.95f, 2, gp);
        starIcon = RuntimeArt.MakeSquare(null, "ScrapStar", Color.white, gm.CellSize * 0.6f, 4, gp);
        starIcon.sprite = RuntimeArt.Star;
        starIcon.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);

        ScrapUI.Instance.enabled = true; // arayüzü hazırla
        ready = true;
    }

    private Vector2Int FindInteractCell()
    {
        Vector2Int[] dirs = { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up };
        foreach (Vector2Int d in dirs)
        {
            Node n = gm.GetNode(PileCell + d);
            if (n != null && n.isWalkable) return PileCell + d;
        }
        Debug.LogWarning("ScrapPile: yanında yürünebilir kare yok: " + name);
        return PileCell + Vector2Int.down;
    }

    private void OnDestroy()
    {
        if (glow != null) Destroy(glow.gameObject);
        if (starIcon != null) Destroy(starIcon.gameObject);
    }

    private void Update()
    {
        if (!ready) return;

        bool visible = !depleted && (FogOfWar.Instance == null || FogOfWar.Instance.IsVisible(InteractCell));
        glow.enabled = visible;
        starIcon.enabled = visible;
        if (visible)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, Mathf.Lerp(0.2f, 0.55f, pulse));
            starIcon.transform.localScale = Vector3.one * gm.CellSize * Mathf.Lerp(0.55f, 0.7f, pulse);
        }

        if (searching) return;

        bool playerHere = !depleted && PlayerOnCell();
        if (playerHere) ScrapUI.Instance.Show(this);
        else ScrapUI.Instance.Hide(this);
    }

    private bool PlayerOnCell()
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.CurrentState != TurnState.PlayerTurn) return false; // yürürken açılmasın
        if (onlyOutOfCombat && tm.IsInCombat()) return false;

        foreach (Unit u in tm.playerUnits)
        {
            if (u == null || u.GetComponent<UnitMovement>() == null) continue;
            if (gm.GetGridPosition(u.transform.position) != InteractCell) continue;
            Vector3 c = gm.GetWorldPosition(InteractCell);
            if (Vector2.Distance(u.transform.position, c) < 0.1f) return true;
        }
        return false;
    }

    // "Araştır" butonu çağırır
    public void StartSearch()
    {
        if (searching || depleted) return;
        StartCoroutine(SearchRoutine());
    }

    private IEnumerator SearchRoutine()
    {
        searching = true;
        TurnManager.Instance.SetBusy();      // araştırırken hareket/tur bitirme kilitli
        ScrapUI ui = ScrapUI.Instance;
        ui.BeginSearch();

        float t = 0f;
        while (t < searchDuration)
        {
            t += Time.deltaTime;
            ui.SetProgress(Mathf.Clamp01(t / searchDuration));
            yield return null;
        }

        int scrap = Random.Range(scrapMin, Mathf.Max(scrapMin, scrapMax) + 1);
        ScrapInventory.Add(scrap);
        string msg = "+" + scrap + " hurda bulundu";

        if (allyCard != null && CardManager.Instance != null && Random.value < allyCardChance)
        {
            CardManager.Instance.AddCard(allyCard);
            msg += "\n<color=#7CFF8A>" + allyCard.cardName + " kartı bulundu!</color>";
        }

        TurnManager.Instance.ClearBusy();
        searching = false;

        if (searchOnlyOnce)
        {
            depleted = true;
            if (body != null) body.color = depletedColor;
            ui.Hide(this);
        }
        ui.ShowResult(msg);
    }
}
