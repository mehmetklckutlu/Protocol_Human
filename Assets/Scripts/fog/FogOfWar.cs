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
    [Tooltip("Grid dışını kaplayan siyah alanın genişliği (birim).")]
    [SerializeField, Min(10f)] private float outsideCoverSize = 500f;

    [Header("Yumuşaklık")]
    [Tooltip("Bir grid karesinin sis dokusunda kaç parçaya bölüneceği (kenar başına). 4 = kare başına 4x4 = 16 sis pikseli.")]
    [SerializeField, Range(1, 8)] private int subdivisions = 4;
    [Tooltip("Kareler arası geçişi yumuşatır (smoothstep). Kapalıysa düz doğrusal geçiş olur.")]
    [SerializeField] private bool smoothEdges = true;

    [Header("Karanlık Mod (labirent)")]
    [Tooltip("Oyuncu drone'unun ışık rengi.")]
    [SerializeField] private Color playerLightColor = new Color(1f, 0.8f, 0.35f);
    [Tooltip("Uyanmış düşman drone'larının ışık rengi.")]
    [SerializeField] private Color enemyLightColor = new Color(1f, 0.32f, 0.22f);
    [Tooltip("Işığın tam merkezindeki karanlık (0 = hiç karanlık yok, 1 = tam siyah).")]
    [SerializeField, Range(0f, 1f)] private float darkCenterAlpha = 0.1f;
    [Tooltip("Işık renginin yoğunluğu.")]
    [SerializeField, Range(0f, 2f)] private float darkTintStrength = 0.6f;
    [Tooltip("Işığın kenara doğru kararma eğrisi (büyük = daha keskin halka).")]
    [SerializeField, Range(0.5f, 3f)] private float darkFalloff = 1.5f;
    [Tooltip("Karanlık modda görülen duvarın arkasındaki kaç sıra duvar da açılsın. 0 = sadece ışığın değdiği duvar görünür.")]
    [SerializeField, Range(0, 3)] private int darkWallRevealDepth = 0;
    [Tooltip("Açık: karanlıkta ışığın yumuşak kenarı, görünmeyen karelere (duvar arkasına) sızmaz.")]
    [SerializeField] private bool darkNoBleed = true;

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
    // Sadece birimlerin görüş alanı çevresi yeniden hesaplanır (büyük haritada takılmayı önler).
    private RectInt lastBox;
    private bool hasLastBox = false;

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
    // Her görüş kaynağının yarıçapı ve oyuncuya ait olup olmadığı (currentPositions ile aynı sıra).
    private readonly List<int> viewerRadius = new List<int>();
    private readonly List<bool> viewerIsPlayer = new List<bool>();
    private readonly List<int> lastRadius = new List<int>();

    // ---- karanlık mod
    private bool darkMode = false;
    private RectInt darkZone;
    private int darkPlayerRadius = 6;
    private int darkEnemyRadius = 10;
    private float[] lightP, lightE;          // bu hesaplamadaki ışık miktarı (0-1)
    private float[] curP, tgtP, curE, tgtE;  // ekrandaki ışık rengi miktarı
    private bool[] seenByPlayer;             // oyuncunun kendi ışığıyla gördüğü kareler
    // Gizli bölge (labirent): dışarıdayken içi hiç görünmez, görüş sınırında kesilir.
    private bool hasHidden = false;
    private RectInt hiddenZone;
    private int srcX, srcY, srcRadius;
    private bool srcIsPlayer;
    private byte plR, plG, plB, enR, enG, enB;

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

    // Oyuncunun kendi görüşüyle (karanlık modda kendi ışığıyla) gördüğü kare mi?
    // Düşmanlar uyanmak için buna bakar: karanlıkta başka bir düşmanın ışığı onları uyandırmaz.
    public bool IsSeenByPlayer(Vector2Int cell)
    {
        if (!darkMode) return IsVisible(cell);
        if (!ready || !InGrid(cell.x, cell.y)) return !ready;
        return seenByPlayer[cell.y * w + cell.x];
    }

    public bool DarkMode => darkMode;

    // Labirent gibi karanlık bölgeler: görüş dışı her yer tamamen kararır,
    // oyuncu ve uyanmış düşmanlar etraflarına (duvarlarda kesilen) ışık yayar.
    public void SetDarkMode(bool on, RectInt zone, int playerRadius, int enemyRadius)
    {
        darkMode = on;
        darkZone = zone;
        darkPlayerRadius = Mathf.Max(0, playerRadius);
        darkEnemyRadius = Mathf.Max(0, enemyRadius);
        Refresh();
    }

    // Bu bölgenin içi, karanlık mod açılana kadar (yani oyuncu içeri girene kadar) asla görünmez.
    public void SetHiddenZone(RectInt zone)
    {
        hasHidden = true;
        hiddenZone = zone;
        // Perde kurulmadan önce yanlışlıkla görülmüş olabilecek kareler hafızadan silinir.
        if (explored != null)
            for (int y = Mathf.Max(0, zone.yMin); y < Mathf.Min(h, zone.yMax); y++)
                for (int x = Mathf.Max(0, zone.xMin); x < Mathf.Min(w, zone.xMax); x++)
                    explored[y * w + x] = false;
        Refresh();
    }

    // Gizli bölgenin, görünen bir kareye bitişik ilk karesi: içi görünmese de oraya tıklayıp girilebilsin.
    public bool IsHiddenEntrance(Vector2Int cell)
    {
        if (!ready || !HiddenCell(cell.x, cell.y)) return false;
        return VisibleAt(cell.x + 1, cell.y) || VisibleAt(cell.x - 1, cell.y)
            || VisibleAt(cell.x, cell.y + 1) || VisibleAt(cell.x, cell.y - 1);
    }

    private bool VisibleAt(int x, int y)
    {
        return InGrid(x, y) && visible[y * w + x] && !HiddenCell(x, y);
    }

    private bool HiddenCell(int x, int y)
    {
        return hasHidden && !darkMode && x >= hiddenZone.xMin && x < hiddenZone.xMax && y >= hiddenZone.yMin && y < hiddenZone.yMax;
    }

    public void SetDarkPlayerRadius(int r)
    {
        if (darkPlayerRadius == r) return;
        darkPlayerRadius = Mathf.Max(0, r);
        Refresh();
    }

    public bool IsExplored(Vector2Int cell)
    {
        if (!ready || !InGrid(cell.x, cell.y)) return !ready;
        return explored[cell.y * w + cell.x];
    }

    public void Refresh() { forceRefresh = true; hasLastBox = false; }

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
        hasLastBox = false;
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
        lightP = new float[n]; lightE = new float[n];
        curP = new float[n]; tgtP = new float[n]; curE = new float[n]; tgtE = new float[n];
        seenByPlayer = new bool[n];
        for (int i = 0; i < n; i++) { cur[i] = unexploredAlpha; tgt[i] = unexploredAlpha; }
        RebuildBlockers();

        Color32 fc = fogColor;
        fogR = fc.r; fogG = fc.g; fogB = fc.b;
        Color32 pc = playerLightColor; plR = pc.r; plG = pc.g; plB = pc.b;
        Color32 ec = enemyLightColor; enR = ec.r; enG = ec.g; enB = ec.b;

        int s = Mathf.Max(1, subdivisions);
        texW = w * s;
        texH = h * s;
        fogTex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        fogTex.name = "FogOfWarTexture";
        fogTex.filterMode = FilterMode.Bilinear;
        fogTex.wrapMode = TextureWrapMode.Clamp;
        pixels = null; // piksel verisi doğrudan dokuya yazılıyor (GetPixelData)

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

        BuildOutsideCover();

        ready = true;
    }

    // Grid'in dışında kalan alan (kamera arka planı) gri görünmesin diye
    // grid dikdörtgeninin dört tarafına tamamen kapalı sis şeritleri koyar.
    private void BuildOutsideCover()
    {
        Texture2D white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        white.name = "FogOfWarOutsideTexture";
        white.SetPixel(0, 0, Color.white);
        white.Apply(false);
        Sprite sq = Sprite.Create(white, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

        float gw = w * gm.CellSize;
        float gh = h * gm.CellSize;
        Vector3 o = gm.GridOrigin;
        float b = outsideCoverSize;
        Color c = fogColor; c.a = 1f;

        MakeCover("FogOutside_Left",   sq, c, new Vector3(o.x - b / 2f,      o.y + gh / 2f, o.z), new Vector3(b, gh + 2f * b, 1f));
        MakeCover("FogOutside_Right",  sq, c, new Vector3(o.x + gw + b / 2f, o.y + gh / 2f, o.z), new Vector3(b, gh + 2f * b, 1f));
        MakeCover("FogOutside_Bottom", sq, c, new Vector3(o.x + gw / 2f, o.y - b / 2f,      o.z), new Vector3(gw, b, 1f));
        MakeCover("FogOutside_Top",    sq, c, new Vector3(o.x + gw / 2f, o.y + gh + b / 2f, o.z), new Vector3(gw, b, 1f));
    }

    private void MakeCover(string name, Sprite sprite, Color color, Vector3 pos, Vector3 scale)
    {
        GameObject go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
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
            lastRadius.Clear();
            lastRadius.AddRange(viewerRadius);
            forceRefresh = false;
        }

        Animate();
        if (texDirty) RebuildPixels();
        if (hideEnemiesInFog) UpdateEnemies();
    }

    private void CollectViewerPositions()
    {
        currentPositions.Clear();
        viewerRadius.Clear();
        viewerIsPlayer.Clear();
        if (TurnManager.Instance == null) return;
        foreach (Unit u in TurnManager.Instance.playerUnits)
        {
            if (u == null) continue;
            currentPositions.Add(gm.GetGridPosition(u.transform.position));
            viewerRadius.Add(darkMode ? darkPlayerRadius : visionRange);
            viewerIsPlayer.Add(true);
        }

        if (!darkMode || darkEnemyRadius <= 0) return;

        // Karanlıkta: harekete geçmiş (uyanmış) düşmanlar etraflarına ışık yayar.
        RectInt zone = new RectInt(darkZone.xMin - 2, darkZone.yMin - 2, darkZone.width + 4, darkZone.height + 4);
        foreach (Unit e in TurnManager.Instance.enemyUnits)
        {
            if (e == null || e.IsDead) continue;
            EnemyAI ai = e.GetComponent<EnemyAI>();
            if (ai == null || !ai.isAwake) continue;
            Vector2Int p = gm.GetGridPosition(e.transform.position);
            if (!zone.Contains(p)) continue;
            currentPositions.Add(p);
            viewerRadius.Add(darkEnemyRadius);
            viewerIsPlayer.Add(false);
        }
    }

    private bool PositionsChanged()
    {
        if (currentPositions.Count != lastPositions.Count) return true;
        for (int i = 0; i < currentPositions.Count; i++)
            if (currentPositions[i] != lastPositions[i] || viewerRadius[i] != lastRadius[i]) return true;
        return false;
    }

    private RectInt ViewBox()
    {
        int maxR = 0;
        foreach (int r in viewerRadius) if (r > maxR) maxR = r;
        int m = maxR + wallRevealDepth + 1;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int p in currentPositions)
        {
            if (p.x - m < minX) minX = p.x - m;
            if (p.y - m < minY) minY = p.y - m;
            if (p.x + m > maxX) maxX = p.x + m;
            if (p.y + m > maxY) maxY = p.y + m;
        }
        if (minX == int.MaxValue) return new RectInt(0, 0, 0, 0);
        minX = Mathf.Max(0, minX); minY = Mathf.Max(0, minY);
        maxX = Mathf.Min(w - 1, maxX); maxY = Mathf.Min(h - 1, maxY);
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static RectInt Union(RectInt a, RectInt b)
    {
        if (a.width <= 0 || a.height <= 0) return b;
        if (b.width <= 0 || b.height <= 0) return a;
        int minX = Mathf.Min(a.xMin, b.xMin), minY = Mathf.Min(a.yMin, b.yMin);
        int maxX = Mathf.Max(a.xMax, b.xMax), maxY = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private void Recompute()
    {
        RectInt box = ViewBox();
        RectInt work = hasLastBox ? Union(box, lastBox) : new RectInt(0, 0, w, h);

        for (int y = work.yMin; y < work.yMax; y++)
        {
            int row = y * w + work.xMin;
            System.Array.Clear(scratch, row, work.width);
            System.Array.Clear(seenByPlayer, row, work.width);
            System.Array.Clear(lightP, row, work.width);
            System.Array.Clear(lightE, row, work.width);
        }

        for (int v = 0; v < currentPositions.Count; v++)
        {
            Vector2Int p = currentPositions[v];
            srcX = p.x; srcY = p.y; srcRadius = viewerRadius[v]; srcIsPlayer = viewerIsPlayer[v];
            Mark(p.x, p.y);
            for (int oct = 0; oct < 8; oct++)
                CastLight(p.x, p.y, 1, 1f, 0f, srcRadius,
                          Mult[0, oct], Mult[1, oct], Mult[2, oct], Mult[3, oct]);
        }

        RevealWallBacks(box);

        // work dışındaki kareler hiç değişmedi; scratch'e taşı ki dizi takası tutarlı kalsın.
        if (hasLastBox)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                if (y < work.yMin || y >= work.yMax)
                {
                    System.Array.Copy(visible, row, scratch, row, w);
                    continue;
                }
                if (work.xMin > 0) System.Array.Copy(visible, row, scratch, row, work.xMin);
                if (work.xMax < w) System.Array.Copy(visible, row + work.xMax, scratch, row + work.xMax, w - work.xMax);
            }
        }

        bool[] old = visible;
        visible = scratch;
        scratch = old;

        lastBox = box;
        hasLastBox = true;

        for (int y = work.yMin; y < work.yMax; y++)
        for (int x = work.xMin; x < work.xMax; x++)
        {
            int i = y * w + x;
            if (visible[i]) explored[i] = true;

            float target, tp = 0f, te = 0f;
            if (darkMode)
            {
                if (visible[i])
                {
                    float L = Mathf.Max(lightP[i], lightE[i]);
                    target = darkCenterAlpha + (1f - darkCenterAlpha) * Mathf.Pow(1f - L, darkFalloff);
                    // Renk ışığın merkezinde yoğun, kenara doğru hızla söner (kenarlar sıcak değil karanlık kalsın).
                    tp = lightP[i] * Mathf.Sqrt(lightP[i]);
                    te = lightE[i] * Mathf.Sqrt(lightE[i]);
                }
                else target = 1f;
            }
            else
            {
                target = visible[i] ? 0f
                    : (rememberExplored && explored[i] ? exploredAlpha : unexploredAlpha);
            }

            if (!Mathf.Approximately(target, tgt[i]) || !Mathf.Approximately(tp, tgtP[i]) || !Mathf.Approximately(te, tgtE[i]))
            {
                tgt[i] = target;
                tgtP[i] = tp;
                tgtE[i] = te;
                if (!inAnim[i]) { inAnim[i] = true; animating.Add(i); }
            }
        }
    }

    // Görülen duvar karesine komşu olan duvar kareleri de açılır.
    // Böylece 2 kare kalınlığındaki duvarın arka sırası (borular) da görünür.
    private void RevealWallBacks(RectInt box)
    {
        int depth = darkMode ? darkWallRevealDepth : wallRevealDepth;
        for (int pass = 0; pass < depth; pass++)
        {
            wallReveal.Clear();
            for (int y = box.yMin; y < box.yMax; y++)
                for (int x = box.xMin; x < box.xMax; x++)
                {
                    int i = y * w + x;
                    if (!scratch[i] || !blocked[i]) continue;

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!InGrid(nx, ny) || HiddenCell(nx, ny)) continue;
                            int j = ny * w + nx;
                            if (blocked[j] && !scratch[j])
                            {
                                wallReveal.Add(j);
                                // Karanlıkta duvarın arka sırası, önündeki duvarın ışığını biraz alır.
                                if (lightP[i] * 0.6f > lightP[j]) lightP[j] = lightP[i] * 0.6f;
                                if (lightE[i] * 0.6f > lightE[j]) lightE[j] = lightE[i] * 0.6f;
                            }
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
            float step = fadeSpeed * Time.deltaTime;
            float c = Mathf.MoveTowards(cur[i], tgt[i], step);
            cur[i] = c;
            curP[i] = Mathf.MoveTowards(curP[i], tgtP[i], step);
            curE[i] = Mathf.MoveTowards(curE[i], tgtE[i], step);

            int x = i % w, y = i / w;
            MarkDirty(x - 1, y - 1, x + 1, y + 1);

            if (Mathf.Approximately(c, tgt[i]) && Mathf.Approximately(curP[i], tgtP[i]) && Mathf.Approximately(curE[i], tgtE[i]))
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

    private static float Cell(float[] a, int x, int y, int w, int h)
    {
        if (x < 0) x = 0; else if (x >= w) x = w - 1;
        if (y < 0) y = 0; else if (y >= h) y = h - 1;
        return a[y * w + x];
    }

    // Sis dokusunu, kare merkezleri arasında çift doğrusal (bilinear) ara değerlemeyle doldurur.
    // Her kare subdivisions x subdivisions piksele bölündüğü için geçişler kare kare değil, yumuşak olur.
    private void RebuildPixels()
    {
        int s = Mathf.Max(1, subdivisions);
        int px0 = dirtyMinX * s, py0 = dirtyMinY * s;
        int px1 = (dirtyMaxX + 1) * s, py1 = (dirtyMaxY + 1) * s;
        float inv = 1f / s;
        Unity.Collections.NativeArray<Color32> data = fogTex.GetPixelData<Color32>(0);
        // Karanlık moddan çıkarken renkli ışık sönene kadar renk hesabı sürer.
        bool tint = darkMode || animating.Count > 0 && AnyTint();
        bool noBleed = (darkMode && darkNoBleed) || hasHidden;

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

                // Karanlıkta ve gizli bölgede: tamamen kapalı bir karenin içine yumuşak kenar sızmasın
                // (yoksa duvarın arkasındaki karenin yarısı görünür).
                if (noBleed)
                {
                    int ox = px / s, oy = py / s;
                    if (cur[oy * w + ox] >= 0.999f && ((darkMode && darkNoBleed) || HiddenCell(ox, oy)))
                    {
                        data[row + px] = new Color32(fogR, fogG, fogB, 255);
                        continue;
                    }
                }

                if (!tint)
                {
                    data[row + px] = new Color32(fogR, fogG, fogB, (byte)(Mathf.Clamp01(a) * 255f + 0.5f));
                    continue;
                }

                float lp = Mathf.Lerp(Mathf.Lerp(Cell(curP, cx, cy, w, h), Cell(curP, cx + 1, cy, w, h), fx),
                                      Mathf.Lerp(Cell(curP, cx, cy + 1, w, h), Cell(curP, cx + 1, cy + 1, w, h), fx), fy) * darkTintStrength;
                float le = Mathf.Lerp(Mathf.Lerp(Cell(curE, cx, cy, w, h), Cell(curE, cx + 1, cy, w, h), fx),
                                      Mathf.Lerp(Cell(curE, cx, cy + 1, w, h), Cell(curE, cx + 1, cy + 1, w, h), fx), fy) * darkTintStrength;
                byte r = (byte)Mathf.Clamp(fogR + plR * lp + enR * le, 0f, 255f);
                byte g = (byte)Mathf.Clamp(fogG + plG * lp + enG * le, 0f, 255f);
                byte b = (byte)Mathf.Clamp(fogB + plB * lp + enB * le, 0f, 255f);
                data[row + px] = new Color32(r, g, b, (byte)(Mathf.Clamp01(a) * 255f + 0.5f));
            }
        }

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

    private bool AnyTint()
    {
        foreach (int i in animating)
            if (curP[i] > 0f || curE[i] > 0f) return true;
        return false;
    }

    private bool InGrid(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h; }

    private bool IsBlocked(int x, int y)
    {
        return !InGrid(x, y) || blocked[y * w + x] || HiddenCell(x, y);
    }

    private void Mark(int x, int y)
    {
        if (!InGrid(x, y) || HiddenCell(x, y)) return;
        int i = y * w + x;
        scratch[i] = true;
        if (srcIsPlayer) seenByPlayer[i] = true;
        if (!darkMode) return;

        float dx = x - srcX, dy = y - srcY;
        float L = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / (srcRadius + 1f));
        if (srcIsPlayer) { if (L > lightP[i]) lightP[i] = L; }
        else if (L > lightE[i]) lightE[i] = L;
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
