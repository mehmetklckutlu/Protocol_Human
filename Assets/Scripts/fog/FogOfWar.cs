using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("Görülen bir duvar karesinin arkasındaki kaç sıra duvar da açılsın. " +
             "Duvarlar 2 kare kalın olduğu için 1 = duvarın iki sırası da (kablolar + borular) görünür.")]
    [SerializeField, Range(0, 3)] private int wallRevealDepth = 1;

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

    [Header("Yumuşaklık")]
    [Tooltip("Bir grid karesinin sis dokusunda kaç parçaya bölüneceği (kenar başına). 4 = kare başına 4x4 = 16 sis pikseli.")]
    [SerializeField, Range(1, 8)] private int subdivisions = 4;
    [Tooltip("Kareler arası geçişi yumuşatır (smoothstep). Kapalıysa düz doğrusal geçiş olur.")]
    [SerializeField] private bool smoothEdges = true;

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
    private readonly List<int> wallReveal = new List<int>();

    private Texture2D fogTex;
    private Color32[] pixels;
    private int texW, texH;
    private byte fogR, fogG, fogB;
    private bool texDirty = false;
    private int dirtyMinX, dirtyMinY, dirtyMaxX, dirtyMaxY;

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

    private void OnDestroy()
    {
        if (fogTex != null) Destroy(fogTex);
    }

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

    public void Refresh() { forceRefresh = true; }

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

        Color32 fc = fogColor;
        fogR = fc.r; fogG = fc.g; fogB = fc.b;

        int s = Mathf.Max(1, subdivisions);
        texW = w * s;
        texH = h * s;
        fogTex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        fogTex.name = "FogOfWarTexture";
        fogTex.filterMode = FilterMode.Bilinear;
        fogTex.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[texW * texH];

        MarkDirty(0, 0, w - 1, h - 1);
        RebuildPixels();

        Sprite sprite = Sprite.Create(fogTex, new Rect(0, 0, texW, texH), Vector2.zero, s / gm.CellSize,
                                      0, SpriteMeshType.FullRect);

        GameObject go = new GameObject("FogOfWarSprite", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.transform.position = gm.GridOrigin;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        ready = true;
    }

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
        if (texDirty) RebuildPixels();
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

        RevealWallBacks();

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

    // Görülen duvar karesine komşu olan duvar kareleri de açılır.
    // Böylece 2 kare kalınlığındaki duvarın arka sırası (borular) da görünür.
    private void RevealWallBacks()
    {
        for (int pass = 0; pass < wallRevealDepth; pass++)
        {
            wallReveal.Clear();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!scratch[i] || !blocked[i]) continue;

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!InGrid(nx, ny)) continue;
                            int j = ny * w + nx;
                            if (blocked[j] && !scratch[j]) wallReveal.Add(j);
                        }
                }
            if (wallReveal.Count == 0) break;
            foreach (int j in wallReveal) scratch[j] = true;
        }
    }

    private void Animate()
    {
        for (int k = animating.Count - 1; k >= 0; k--)
        {
            int i = animating[k];
            float c = Mathf.MoveTowards(cur[i], tgt[i], fadeSpeed * Time.deltaTime);
            cur[i] = c;

            int x = i % w, y = i / w;
            MarkDirty(x - 1, y - 1, x + 1, y + 1);

            if (Mathf.Approximately(c, tgt[i]))
            {
                animating.RemoveAt(k);
                inAnim[i] = false;
            }
        }
    }

    private void MarkDirty(int minX, int minY, int maxX, int maxY)
    {
        minX = Mathf.Max(0, minX); minY = Mathf.Max(0, minY);
        maxX = Mathf.Min(w - 1, maxX); maxY = Mathf.Min(h - 1, maxY);
        if (!texDirty)
        {
            dirtyMinX = minX; dirtyMinY = minY; dirtyMaxX = maxX; dirtyMaxY = maxY;
            texDirty = true;
            return;
        }
        if (minX < dirtyMinX) dirtyMinX = minX;
        if (minY < dirtyMinY) dirtyMinY = minY;
        if (maxX > dirtyMaxX) dirtyMaxX = maxX;
        if (maxY > dirtyMaxY) dirtyMaxY = maxY;
    }

    private float CellAlpha(int x, int y)
    {
        if (x < 0) x = 0; else if (x >= w) x = w - 1;
        if (y < 0) y = 0; else if (y >= h) y = h - 1;
        return cur[y * w + x];
    }

    // Sis dokusunu, kare merkezleri arasında çift doğrusal (bilinear) ara değerlemeyle doldurur.
    // Her kare subdivisions x subdivisions piksele bölündüğü için geçişler kare kare değil, yumuşak olur.
    private void RebuildPixels()
    {
        int s = Mathf.Max(1, subdivisions);
        int px0 = dirtyMinX * s, py0 = dirtyMinY * s;
        int px1 = (dirtyMaxX + 1) * s, py1 = (dirtyMaxY + 1) * s;
        float inv = 1f / s;

        for (int py = py0; py < py1; py++)
        {
            float v = (py + 0.5f) * inv - 0.5f;
            int cy = Mathf.FloorToInt(v);
            float fy = v - cy;
            if (smoothEdges) fy = fy * fy * (3f - 2f * fy);

            int row = py * texW;
            for (int px = px0; px < px1; px++)
            {
                float u = (px + 0.5f) * inv - 0.5f;
                int cx = Mathf.FloorToInt(u);
                float fx = u - cx;
                if (smoothEdges) fx = fx * fx * (3f - 2f * fx);

                float a00 = CellAlpha(cx, cy), a10 = CellAlpha(cx + 1, cy);
                float a01 = CellAlpha(cx, cy + 1), a11 = CellAlpha(cx + 1, cy + 1);
                float a = Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);

                pixels[row + px] = new Color32(fogR, fogG, fogB, (byte)(Mathf.Clamp01(a) * 255f + 0.5f));
            }
        }

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);
        texDirty = false;
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
