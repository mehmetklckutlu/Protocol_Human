using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Patlayan varil: bir saldırı kartıyla hedef alınıp vurulunca patlar, çevresindeki karelere hasar verir.
// Zincirleme: patlama alanındaki başka variller de patlar.
// Kart hedef seçerken fareyi varilin üstüne getirince patlama alanı kırmızı gösterilir.
// Şimdilik kutu görseli kullanır; kendi sprite'ını bu objedeki SpriteRenderer'a atayabilirsin.
public class ExplosiveBarrel : MonoBehaviour
{
    public static readonly List<ExplosiveBarrel> All = new List<ExplosiveBarrel>();

    [Header("Patlama")]
    [Tooltip("Patlamanın kaç kare yarıçapında etki ettiği (1 = 3x3, 2 = 5x5).")]
    [SerializeField, Min(0)] private int explosionRadius = 2;
    [SerializeField, Min(0)] private int explosionDamage = 40;
    [Tooltip("Zincirleme patlamada diğer varilin patlamadan önce bekleme süresi.")]
    [SerializeField] private float chainDelay = 0.25f;

    [Header("Görünüm")]
    [SerializeField] private Color bodyColor = new Color(0.85f, 0.3f, 0.15f);
    [SerializeField] private Color previewColor = new Color(1f, 0.15f, 0.1f, 0.4f);
    [SerializeField] private Color blastColor = new Color(1f, 0.65f, 0.1f, 0.85f);

    public Vector2Int Cell { get; private set; }

    private GridManager gm;
    private bool exploded;
    private readonly List<SpriteRenderer> previewTiles = new List<SpriteRenderer>();

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Start()
    {
        gm = GridManager.Instance;
        if (gm == null) { enabled = false; return; }

        Cell = gm.GetGridPosition(transform.position);
        transform.position = gm.GetWorldPosition(Cell);

        Node n = gm.GetNode(Cell);
        if (n != null) n.isWalkable = false;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeArt.Square;
            sr.color = bodyColor;
            transform.localScale = new Vector3(gm.CellSize * 0.7f, gm.CellSize * 0.7f, 1f);
        }
        sr.sortingOrder = 3;
    }

    // Patlama alanındaki kareler (grid dışı ve merkez dahil)
    public List<Vector2Int> GetBlastCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int dx = -explosionRadius; dx <= explosionRadius; dx++)
            for (int dy = -explosionRadius; dy <= explosionRadius; dy++)
            {
                Vector2Int c = Cell + new Vector2Int(dx, dy);
                if (gm.IsInsideGrid(c)) cells.Add(c);
            }
        return cells;
    }

    // ---------- Önizleme ----------

    public void ShowPreview()
    {
        List<Vector2Int> cells = GetBlastCells();
        while (previewTiles.Count < cells.Count)
            previewTiles.Add(RuntimeArt.MakeSquare(null, "BlastPreview", previewColor, gm.CellSize * 0.95f, 6, Vector3.zero));

        for (int i = 0; i < previewTiles.Count; i++)
        {
            bool on = i < cells.Count;
            previewTiles[i].gameObject.SetActive(on);
            if (on) previewTiles[i].transform.position = gm.GetWorldPosition(cells[i]);
        }
    }

    public void HidePreview()
    {
        foreach (SpriteRenderer s in previewTiles) if (s != null) s.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer s in previewTiles) if (s != null) Destroy(s.gameObject);
    }

    // ---------- Patlama ----------

    public void Explode()
    {
        if (exploded) return;
        exploded = true;
        HidePreview();

        List<Vector2Int> cells = GetBlastCells();
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(cells);

        Node n = gm.GetNode(Cell);
        if (n != null) n.isWalkable = true;

        // Görsel efekt
        GameObject fx = new GameObject("BlastFx");
        fx.AddComponent<BlastFx>().Play(cells, gm, blastColor);

        // Alandaki birimlere hasar (kopya listeler: biri ölürse liste bozulmasın)
        TurnManager tm = TurnManager.Instance;
        if (tm != null)
        {
            DamageUnits(new List<Unit>(tm.playerUnits), set);
            DamageUnits(new List<Unit>(tm.enemyUnits), set);
        }

        // Alandaki diğer variller zincirleme patlar
        foreach (ExplosiveBarrel other in new List<ExplosiveBarrel>(All))
        {
            if (other != null && other != this && !other.exploded && set.Contains(other.Cell))
                other.StartCoroutine(other.ExplodeAfter(chainDelay));
        }

        // Bu objeyi gizle; zincir coroutine'leri başkalarında çalıştığı için güvenle yok edilebilir
        Destroy(gameObject);
    }

    private IEnumerator ExplodeAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Explode();
    }

    private void DamageUnits(List<Unit> units, HashSet<Vector2Int> area)
    {
        foreach (Unit u in units)
        {
            if (u == null) continue;
            if (area.Contains(gm.GetGridPosition(u.transform.position)))
                u.TakeDamage(explosionDamage);
        }
    }
}

// Patlama alanını kısa süre turuncu gösterip kaybolan efekt
public class BlastFx : MonoBehaviour
{
    public void Play(List<Vector2Int> cells, GridManager gm, Color color)
    {
        StartCoroutine(Run(cells, gm, color));
    }

    private IEnumerator Run(List<Vector2Int> cells, GridManager gm, Color color)
    {
        List<SpriteRenderer> tiles = new List<SpriteRenderer>();
        foreach (Vector2Int c in cells)
            tiles.Add(RuntimeArt.MakeSquare(transform, "Blast", color, gm.CellSize * 0.95f, 7, gm.GetWorldPosition(c)));

        float t = 0f, dur = 0.5f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = 1f - t / dur;
            foreach (SpriteRenderer s in tiles) s.color = new Color(color.r, color.g, color.b, color.a * a);
            yield return null;
        }
        Destroy(gameObject);
    }
}
