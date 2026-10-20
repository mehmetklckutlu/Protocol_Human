using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Görüş açısı + savaş sisi (fog of war).
// - Oyuncu birimleri kendi çevrelerinde "visionRange" kare içini görür.
// - Duvarlar ve siper kutuları (GridManager'da yürünemez olan her kare) görüşü engeller.
// - Görüş dışı kareler siyah sisle kaplanır. İstenirse daha önce görülen yerler silik kalır.
// - Sisteki düşmanlar gizlenir.
public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance { get; private set; }

    [Header("Görüş")]
    [Tooltip("Birimin kaç kare uzağı görebildiği.")]
    [SerializeField] private int visionRange = 15;
    [Tooltip("Açık: daha önce gördüğün yerler silik kalır. Kapalı: görüş dışındaki her yer tamamen siyah olur.")]
    [SerializeField] private bool rememberExplored = true;
    [Tooltip("Açık: görüş dışındaki düşmanlar gizlenir.")]
    [SerializeField] private bool hideEnemiesInFog = true;

    [Header("Görünüm")]
    [Tooltip("Hiç görülmemiş karelerin sis koyuluğu (1 = tam siyah).")]
    [SerializeField, Range(0f, 1f)] private float unexploredAlpha = 1f;
    [Tooltip("Daha önce görülmüş ama şu an görünmeyen karelerin sis koyuluğu.")]
    [SerializeField, Range(0f, 1f)] private float exploredAlpha = 0.6f;
    [Tooltip("Sisin açılıp kapanma hızı.")]
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private Color fogColor = Color.black;
    [Tooltip("Sis, bu sıralamadan düşük her şeyin üstünü kaplar.")]
    [SerializeField] private int sortingOrder = 100;

    private GridManager gm;
    private int w, h;
    private bool[] blocked;
    private bool[] visible;
    private bool[] scratch;
    private bool[] explored;
    private float[] cur;
    private float[] tgt;
    private bool[] inAnim;
    private readonly List<int> animating = new List<int>();

    private Tilemap fogMap;
    private bool ready = false;
    private bool forceRefresh = true;
    private readonly List<Vector2Int> lastPositions = new List<Vector2Int>();
    private readonly List<Vector2Int> currentPositions = new List<Vector2Int>();

    private class EnemyView
    {
        public Renderer[] renderers;
        public Canvas[] canvases;
        public bool shown = true;
    }
    private readonly Dictionary<Unit, EnemyView> enemyViews = new Dictionary<Unit, EnemyView>();

    // 8 oktant için dönüşüm çarpanları (recursive shadowcasting)
    private static readonly int[,] Mult =
    {
        { 1, 0, 0, -1, -1, 0, 0, 1 },
        { 0, 1, -1, 0, 0, -1, 1, 0 },
        { 0, 1, 1, 0, 0, -1, -1, 0 },
        { 1, 0, 0, 1, -1, 0, 0, -1 }
    };

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        gm = GridManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("FogOfWar: GridManager bulunamadı, sis devre dışı.");
            enabled = false;
            return;
        }
        Build();
        PutUiAboveFog();
    }

    // Ekrana sabit arayüz (kartlar, butonlar vb.) sisin altında kalmasın
    private void PutUiAboveFog()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
            if (c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                c.overrideSorting = false;
                if (c.sortingOrder <= sortingOrder) c.sortingOrder = sortingOrder + 1;
            }
        }
    }

    // ---------- Dışarıdan kullanım ----------

    public bool IsVisible(Vector2Int cell)
    {
        if (!ready || !InGrid(cell.x, cell.y)) return !ready;
        return visible[cell.y * w + cell.x];
    }

    public bool IsExplored(Vector2Int cell)
    {
        if (!ready || !InGrid(cell.x, cell.y)) return !ready;
        return explored[cell.y * w + cell.x];
    }

    // Görüşü hemen yeniden hesaplat
    public void Refresh() { forceRefresh = true; }

    // Duvar/siper değişirse (ör. kutu yok edilirse) çağır
    public void RebuildBlockers()
    {
        if (gm == null) return;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Node n = gm.GetNode(new Vector2Int(x, y));
                blocked[y * w + x] = n == null || !n.isWalkable;
            }
        forceRefresh = true;
    }

    // ---------- Kurulum ----------

    private void Build()
    {
        w = gm.GridSize.x;
        h = gm.GridSize.y;
        int n = w * h;

        blocked = new bool[n];
        visible = new bool[n];
        scratch = new bool[n];
        explored = new bool[n];
        cur = new float[n];
        tgt = new float[n];
        inAnim = new bool[n];
        for (int i = 0; i < n; i++) { cur[i] = unexploredAlpha; tgt[i] = unexploredAlpha; }
        RebuildBlockers();

        // 1x1 beyaz sprite: tam bir grid karesi boyunda
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f / gm.CellSize);

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.flags = TileFlags.None; // kare başına renk (şeffaflık) değiştirebilmek için
        Color start = fogColor;
        start.a = unexploredAlpha;
        tile.color = start;

        GameObject go = new GameObject("FogOfWarGrid", typeof(Grid));
        go.transform.SetParent(transform, false);
        go.transform.position = gm.GridOrigin;
        Grid grid = go.GetComponent<Grid>();
        grid.cellSize = new Vector3(gm.CellSize, gm.CellSize, 0f);

        GameObject mapGo = new GameObject("Fog", typeof(Tilemap), typeof(TilemapRenderer));
        mapGo.transform.SetParent(go.transform, false);
        fogMap = mapGo.GetComponent<Tilemap>();
        mapGo.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;

        TileBase[] tiles = new TileBase[n];
        for (int i = 0; i < n; i++) tiles[i] = tile;
        fogMap.SetTilesBlock(new BoundsInt(0, 0, 0, w, h, 1), tiles);

        ready = true;
    }

    // ---------- Her kare ----------

    private void Update()
    {
        if (!ready) return;

        CollectViewerPositions();
        if (forceRefresh || PositionsChanged())
        {
            Recompute();
            lastPositions.Clear();
            lastPositions.AddRange(currentPositions);
            forceRefresh = false;
        }

        Animate();
        if (hideEnemiesInFog) UpdateEnemies();
    }

    private void CollectViewerPositions()
    {
        currentPositions.Clear();
        if (TurnManager.Instance == null) return;
        foreach (Unit u in TurnManager.Instance.playerUnits)
        {
            if (u == null) continue;
            currentPositions.Add(gm.GetGridPosition(u.transform.position));
        }
    }

    private bool PositionsChanged()
    {
        if (currentPositions.Count != lastPositions.Count) return true;
        for (int i = 0; i < currentPositions.Count; i++)
            if (currentPositions[i] != lastPositions[i]) return true;
        return false;
    }

    private void Recompute()
    {
        System.Array.Clear(scratch, 0, scratch.Length);

        foreach (Vector2Int p in currentPositions)
        {
            Mark(p.x, p.y);
            for (int oct = 0; oct < 8; oct++)
                CastLight(p.x, p.y, 1, 1f, 0f, visionRange,
                          Mult[0, oct], Mult[1, oct], Mult[2, oct], Mult[3, oct]);
        }

        bool[] old = visible;
        visible = scratch;
        scratch = old;

        for (int i = 0; i < visible.Length; i++)
        {
            if (visible[i]) explored[i] = true;

            float target = visible[i] ? 0f
                : (rememberExplored && explored[i] ? exploredAlpha : unexploredAlpha);

            if (!Mathf.Approximately(target, tgt[i]))
            {
                tgt[i] = target;
                if (!inAnim[i]) { inAnim[i] = true; animating.Add(i); }
            }
        }
    }

    private void Animate()
    {
        for (int k = animating.Count - 1; k >= 0; k--)
        {
            int i = animating[k];
            float c = Mathf.MoveTowards(cur[i], tgt[i], fadeSpeed * Time.deltaTime);
            cur[i] = c;

            Color col = fogColor;
            col.a = c;
            fogMap.SetColor(new Vector3Int(i % w, i / w, 0), col);

            if (Mathf.Approximately(c, tgt[i]))
            {
                animating.RemoveAt(k);
                inAnim[i] = false;
            }
        }
    }

    private void UpdateEnemies()
    {
        if (TurnManager.Instance == null) return;

        foreach (Unit e in TurnManager.Instance.enemyUnits)
        {
            if (e == null) continue;

            Vector2Int cell = gm.GetGridPosition(e.transform.position);
            bool show = visible[cell.y * w + cell.x];

            EnemyView view;
            if (!enemyViews.TryGetValue(e, out view))
            {
                view = new EnemyView
                {
                    renderers = e.GetComponentsInChildren<Renderer>(true),
                    canvases = e.GetComponentsInChildren<Canvas>(true),
                    shown = true
                };
                enemyViews[e] = view;
            }

            if (view.shown == show) continue;
            view.shown = show;
            foreach (Renderer r in view.renderers) if (r != null) r.enabled = show;
            foreach (Canvas c in view.canvases) if (c != null) c.enabled = show;
        }
    }

    // ---------- Görüş hesabı (recursive shadowcasting) ----------

    private bool InGrid(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h; }

    private bool IsBlocked(int x, int y)
    {
        return !InGrid(x, y) || blocked[y * w + x];
    }

    private void Mark(int x, int y)
    {
        if (InGrid(x, y)) scratch[y * w + x] = true;
    }

    private void CastLight(int cx, int cy, int row, float start, float end, int radius,
                           int xx, int xy, int yx, int yy)
    {
        if (start < end) return;

        float newStart = 0f;
        int radiusSqr = radius * radius;

        for (int j = row; j <= radius; j++)
        {
            int dx = -j - 1;
            int dy = -j;
            bool wasBlocked = false;

            while (dx <= 0)
            {
                dx++;
                int mapX = cx + dx * xx + dy * xy;
                int mapY = cy + dx * yx + dy * yy;

                float lSlope = (dx - 0.5f) / (dy + 0.5f);
                float rSlope = (dx + 0.5f) / (dy - 0.5f);

                if (start < rSlope) continue;
                if (end > lSlope) break;

                if (dx * dx + dy * dy <= radiusSqr) Mark(mapX, mapY);

                bool blockedHere = IsBlocked(mapX, mapY);
                if (wasBlocked)
                {
                    if (blockedHere)
                    {
                        newStart = rSlope;
                        continue;
                    }
                    wasBlocked = false;
                    start = newStart;
                }
                else if (blockedHere && j < radius)
                {
                    wasBlocked = true;
                    CastLight(cx, cy, j + 1, start, lSlope, radius, xx, xy, yx, yy);
                    newStart = rSlope;
                }
            }

            if (wasBlocked) break;
        }
    }
}
