using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class RuntimeArt
{
    private static Sprite square;
    private static Sprite star;
    private static Sprite ring;
    private static Sprite cpuIcon;
    private static Sprite scrapIcon;
    private static Sprite allyHpFrame;
    private static Sprite enemyHpFrame;
    private static Sprite shieldedHpFrame;
    private static Sprite allyHpFill;
    private static Sprite enemyHpFill;
    private static Sprite shieldIcon;
    private static Sprite staminaPipActive;
    private static Sprite staminaPipEmpty;
    private static Sprite endTurnButtonSprite;
    private static TMP_FontAsset pixelFont;
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> cardArtCache = new System.Collections.Generic.Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitPixelFontHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyPixelFontToScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyPixelFontToScene();
    }

    public static TMP_FontAsset PixelFont
    {
        get
        {
            if (pixelFont == null)
            {
                Font src = Resources.Load<Font>("Fonts/RetroPixelFont");
                if (src == null) src = Resources.Load<Font>("Fonts/PixelFont");
#if UNITY_EDITOR
                if (src == null)
                    src = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/RetroPixelFont.ttf");
                if (src == null)
                    src = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/PixelFont.ttf");
#endif
                if (src != null)
                {
                    pixelFont = TMP_FontAsset.CreateFontAsset(
                        src, 64, 6,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                        1024, 1024,
                        AtlasPopulationMode.Dynamic);
                }
            }
            return pixelFont;
        }
    }

    public static void ApplyPixelFont(TMP_Text t)
    {
        if (t == null) return;
        TMP_FontAsset pf = PixelFont;
        if (pf != null && t.font != pf) t.font = pf;
    }

    public static void ApplyPixelFontToScene()
    {
        TMP_FontAsset pf = PixelFont;
        if (pf == null) return;
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.font != pf) t.font = pf;
        }
    }

    public static Sprite EndTurnButtonSprite
    {
        get
        {
            if (endTurnButtonSprite == null)
            {
                const int w = 72;
                const int h = 24;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color outer = new Color(0.03f, 0.08f, 0.12f, 0.98f);
                Color border = new Color(0.18f, 0.72f, 0.88f, 1f);
                Color accent = new Color(0.45f, 0.98f, 1f, 1f);
                Color amber = new Color(1f, 0.72f, 0.20f, 1f);
                Color bodyTop = new Color(0.08f, 0.22f, 0.30f, 0.96f);
                Color bodyBot = new Color(0.04f, 0.12f, 0.18f, 0.96f);
                Color stripe = new Color(0.06f, 0.16f, 0.23f, 0.96f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool cut = ((x == 0 || x == w - 1) && (y <= 1 || y >= h - 2)) ||
                                   ((x == 1 || x == w - 2) && (y == 0 || y == h - 1));
                        if (cut)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool isOuter = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                        bool isBorder = x == 1 || x == w - 2 || y == 1 || y == h - 2;
                        if (isOuter)
                        {
                            t.SetPixel(x, y, outer);
                        }
                        else if (isBorder)
                        {
                            bool cornerBracket = (x <= 7 || x >= w - 8) && (y <= 5 || y >= h - 6);
                            bool sideTick = (x == 1 || x == w - 2) && (y >= h / 2 - 2 && y <= h / 2 + 2);
                            t.SetPixel(x, y, sideTick ? amber : (cornerBracket ? accent : border));
                        }
                        else
                        {
                            bool isScanline = (y % 3 == 0);
                            Color bg = y > h / 2 ? bodyTop : bodyBot;
                            t.SetPixel(x, y, isScanline ? stripe : bg);
                        }
                    }
                }

                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                endTurnButtonSprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return endTurnButtonSprite;
        }
    }

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                t.SetPixel(0, 0, Color.white);
                t.Apply();
                t.filterMode = FilterMode.Point;
                square = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return square;
        }
    }

    public static Sprite Ring
    {
        get
        {
            if (ring == null)
            {
                const int size = 32;
                Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        bool onRing = dist >= 11.5f && dist <= 14.5f;
                        t.SetPixel(x, y, onRing ? Color.white : Color.clear);
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                ring = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return ring;
        }
    }

    public static Sprite AllyHpFrame
    {
        get
        {
            if (allyHpFrame == null)
            {
                const int w = 52;
                const int h = 10;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color outer = new Color(0.05f, 0.11f, 0.16f, 0.96f);
                Color trim = new Color(0.12f, 0.38f, 0.28f, 0.96f);
                Color accent = new Color(0.22f, 0.92f, 0.55f, 1f);
                Color bgDark = new Color(0.04f, 0.08f, 0.12f, 0.92f);
                Color bgSlot = new Color(0.07f, 0.14f, 0.20f, 0.92f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool cornerCut = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                        if (cornerCut)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool isOuter = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                        bool isTrim = x == 1 || x == w - 2 || y == 1 || y == h - 2;

                        if (isOuter)
                        {
                            bool isBracket = (x >= 1 && x <= 4) || (x >= w - 5 && x <= w - 2);
                            t.SetPixel(x, y, isBracket ? accent : outer);
                        }
                        else if (isTrim)
                        {
                            t.SetPixel(x, y, trim);
                        }
                        else
                        {
                            bool slotLine = ((x - 2) % 6 == 5);
                            t.SetPixel(x, y, slotLine ? bgSlot : bgDark);
                        }
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                allyHpFrame = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return allyHpFrame;
        }
    }

    public static Sprite ShieldedHpFrame
    {
        get
        {
            if (shieldedHpFrame == null)
            {
                const int w = 52;
                const int h = 10;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color outer = new Color(0.08f, 0.45f, 0.88f, 0.98f);
                Color trim = new Color(0.22f, 0.76f, 1f, 0.98f);
                Color accent = new Color(0.65f, 0.95f, 1f, 1f);
                Color bgDark = new Color(0.04f, 0.09f, 0.16f, 0.92f);
                Color bgSlot = new Color(0.08f, 0.17f, 0.28f, 0.92f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool cornerCut = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                        if (cornerCut)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool isOuter = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                        bool isTrim = x == 1 || x == w - 2 || y == 1 || y == h - 2;

                        if (isOuter)
                        {
                            bool isBracket = (x >= 1 && x <= 5) || (x >= w - 6 && x <= w - 2);
                            t.SetPixel(x, y, isBracket ? accent : outer);
                        }
                        else if (isTrim)
                        {
                            t.SetPixel(x, y, trim);
                        }
                        else
                        {
                            bool slotLine = ((x - 2) % 6 == 5);
                            t.SetPixel(x, y, slotLine ? bgSlot : bgDark);
                        }
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                shieldedHpFrame = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return shieldedHpFrame;
        }
    }

    public static Sprite EnemyHpFrame
    {
        get
        {
            if (enemyHpFrame == null)
            {
                const int w = 52;
                const int h = 10;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color outer = new Color(0.12f, 0.05f, 0.07f, 0.96f);
                Color trim = new Color(0.45f, 0.12f, 0.15f, 0.96f);
                Color hazard = new Color(1f, 0.35f, 0.22f, 1f);
                Color bgDark = new Color(0.09f, 0.04f, 0.05f, 0.92f);
                Color bgStripe = new Color(0.16f, 0.07f, 0.09f, 0.92f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool cornerCut = ((x == 0 || x == w - 1) && (y <= 1 || y >= h - 2)) ||
                                         ((x == 1 || x == w - 2) && (y == 0 || y == h - 1));
                        if (cornerCut)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        if ((x == 0 || x == w - 1) && (y >= 3 && y <= h - 4))
                        {
                            t.SetPixel(x, y, hazard);
                            continue;
                        }

                        bool isOuter = x <= 1 || x >= w - 2 || y == 0 || y == h - 1;
                        bool isTrim = x == 2 || x == w - 3 || y == 1 || y == h - 2;

                        if (isOuter)
                        {
                            t.SetPixel(x, y, outer);
                        }
                        else if (isTrim)
                        {
                            bool notch = (x == 2 || x == w - 3) && (y == 2 || y == h - 3);
                            t.SetPixel(x, y, notch ? hazard : trim);
                        }
                        else
                        {
                            bool stripe = ((x + y) % 5 == 0);
                            t.SetPixel(x, y, stripe ? bgStripe : bgDark);
                        }
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                enemyHpFrame = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return enemyHpFrame;
        }
    }

    public static Sprite AllyHpFill
    {
        get
        {
            if (allyHpFill == null)
            {
                const int w = 48;
                const int h = 6;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color top = new Color(0.45f, 1f, 0.65f, 1f);
                Color mid = new Color(0.14f, 0.88f, 0.40f, 1f);
                Color low = new Color(0.08f, 0.68f, 0.28f, 1f);
                Color bot = new Color(0.05f, 0.45f, 0.18f, 1f);
                Color sep = new Color(0.06f, 0.52f, 0.22f, 1f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool isSep = (x % 6 == 5) && x < w - 1;
                        if (isSep && y >= 1 && y <= h - 2)
                        {
                            t.SetPixel(x, y, sep);
                            continue;
                        }

                        Color c = y == h - 1 ? top : (y >= 3 ? mid : (y >= 1 ? low : bot));
                        t.SetPixel(x, y, c);
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                allyHpFill = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return allyHpFill;
        }
    }

    public static Sprite EnemyHpFill
    {
        get
        {
            if (enemyHpFill == null)
            {
                const int w = 46;
                const int h = 6;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color top = new Color(1f, 0.48f, 0.42f, 1f);
                Color mid = new Color(0.92f, 0.16f, 0.18f, 1f);
                Color low = new Color(0.74f, 0.10f, 0.12f, 1f);
                Color bot = new Color(0.48f, 0.06f, 0.08f, 1f);
                Color sep = new Color(0.58f, 0.08f, 0.10f, 1f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool isSep = ((x + y) % 6 == 5) && x < w - 1;
                        if (isSep && y >= 1 && y <= h - 2)
                        {
                            t.SetPixel(x, y, sep);
                            continue;
                        }

                        Color c = y == h - 1 ? top : (y >= 3 ? mid : (y >= 1 ? low : bot));
                        t.SetPixel(x, y, c);
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                enemyHpFill = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return enemyHpFill;
        }
    }

    public static Sprite ShieldIcon
    {
        get
        {
            if (shieldIcon == null)
            {
                const int w = 15;
                const int h = 17;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color outline = new Color(0.03f, 0.08f, 0.15f, 1f);
                Color rim = new Color(0.58f, 0.93f, 1f, 1f);
                Color bodyLight = new Color(0.18f, 0.72f, 1f, 1f);
                Color bodyDark = new Color(0.08f, 0.46f, 0.85f, 1f);
                Color core = new Color(0.05f, 0.28f, 0.58f, 1f);

                int cx = w / 2;
                for (int y = 0; y < h; y++)
                {
                    int halfWidth;
                    if (y >= 15) halfWidth = 5;
                    else if (y >= 7) halfWidth = 7;
                    else halfWidth = Mathf.Clamp(y + 1, 1, 7);

                    for (int x = 0; x < w; x++)
                    {
                        int dx = Mathf.Abs(x - cx);
                        if (dx > halfWidth)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool isOuter = dx == halfWidth || y == h - 1 || y == 0 || (y == 15 && dx >= 5);
                        bool isRim = dx == halfWidth - 1 || y == h - 2 || (y == 14 && dx >= 5);

                        if (isOuter)
                        {
                            t.SetPixel(x, y, outline);
                        }
                        else if (isRim)
                        {
                            t.SetPixel(x, y, rim);
                        }
                        else if (dx <= 3 && y >= 5 && y <= 12)
                        {
                            t.SetPixel(x, y, core);
                        }
                        else
                        {
                            t.SetPixel(x, y, x <= cx ? bodyLight : bodyDark);
                        }
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                shieldIcon = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return shieldIcon;
        }
    }

    public static Sprite StaminaPipActive
    {
        get
        {
            if (staminaPipActive == null)
            {
                const int w = 14;
                const int h = 7;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color border = new Color(0.12f, 0.08f, 0.03f, 0.96f);
                Color top = new Color(1f, 0.92f, 0.52f, 1f);
                Color mid = new Color(0.98f, 0.68f, 0.12f, 1f);
                Color bot = new Color(0.72f, 0.42f, 0.05f, 1f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                        if (corner)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool isBorder = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                        if (isBorder)
                        {
                            t.SetPixel(x, y, border);
                        }
                        else
                        {
                            Color c = y == h - 2 ? top : (y >= 2 ? mid : bot);
                            t.SetPixel(x, y, c);
                        }
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                staminaPipActive = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return staminaPipActive;
        }
    }

    public static Sprite StaminaPipEmpty
    {
        get
        {
            if (staminaPipEmpty == null)
            {
                const int w = 14;
                const int h = 7;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color border = new Color(0.08f, 0.07f, 0.05f, 0.90f);
                Color innerRim = new Color(0.22f, 0.18f, 0.11f, 0.85f);
                Color innerDark = new Color(0.11f, 0.09f, 0.06f, 0.85f);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                        if (corner)
                        {
                            t.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool isBorder = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                        if (isBorder)
                            t.SetPixel(x, y, border);
                        else if (y == h - 2)
                            t.SetPixel(x, y, innerRim);
                        else
                            t.SetPixel(x, y, innerDark);
                    }
                }
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                staminaPipEmpty = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return staminaPipEmpty;
        }
    }

    public static Sprite CpuIcon
    {
        get
        {
            if (cpuIcon == null)
            {
                cpuIcon = Resources.Load<Sprite>("Icons/CPU");
#if UNITY_EDITOR
                if (cpuIcon == null)
                    cpuIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/Kaynaklar/CPU.png");
#endif
            }
            return cpuIcon;
        }
    }

    public static Sprite ScrapIcon
    {
        get
        {
            if (scrapIcon == null)
            {
                scrapIcon = Resources.Load<Sprite>("Icons/Hurda");
#if UNITY_EDITOR
                if (scrapIcon == null)
                    scrapIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/Kaynaklar/Hurda.png");
#endif
            }
            return scrapIcon;
        }
    }

    public static Sprite Star
    {
        get
        {
            if (star == null)
            {
                const int size = 64;
                Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2[] pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    float r = (i % 2 == 0) ? 0.48f : 0.20f;
                    pts[i] = new Vector2(0.5f + Mathf.Cos(ang) * r, 0.5f + Mathf.Sin(ang) * r);
                }
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                        t.SetPixel(x, y, Inside(pts, p) ? Color.white : Color.clear);
                    }
                t.Apply();
                t.filterMode = FilterMode.Bilinear;
                star = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return star;
        }
    }

    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    public static SpriteRenderer MakeSquare(Transform parent, string name, Color color, float size, int order, Vector3 worldPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        go.transform.localScale = new Vector3(size, size, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public static Sprite GetCardArt(CardData card)
    {
        if (card == null) return null;
        string key = !string.IsNullOrEmpty(card.name) ? card.name : card.cardName;
        if (cardArtCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite created = BuildCardArt(key, card);
        cardArtCache[key] = created;
        return created;
    }

    private static Sprite BuildCardArt(string key, CardData card)
    {
        const int S = 32;
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        Color[] px = new Color[S * S];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color col)
        {
            if (x >= 0 && x < S && y >= 0 && y < S) px[y * S + x] = col;
        }

        void FillRect(int x0, int y0, int x1, int y1, Color col)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, col);
        }

        void DrawCircle(float cx, float cy, float rMin, float rMax, Color col)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (d >= rMin && d <= rMax) Set(x, y, col);
                }
        }

        void DrawLine(int x0, int y0, int x1, int y1, int radius, Color col)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int i = 0; i <= steps; i++)
            {
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, i / (float)steps));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, i / (float)steps));
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius + 1)
                            Set(cx + dx, cy + dy, col);
            }
        }

        Color dark = new Color(0.06f, 0.09f, 0.14f, 1f);
        Color cyan = new Color(0.30f, 0.95f, 1f, 1f);
        Color cyanDark = new Color(0.10f, 0.55f, 0.78f, 1f);
        Color yellow = new Color(1f, 0.92f, 0.28f, 1f);
        Color orange = new Color(1f, 0.55f, 0.15f, 1f);
        Color red = new Color(1f, 0.25f, 0.25f, 1f);
        Color green = new Color(0.35f, 0.98f, 0.52f, 1f);
        Color steel = new Color(0.65f, 0.74f, 0.84f, 1f);

        if (key.Contains("YakinSaldiri"))
        {
            // Enerji kılıcı ve kesik izi
            DrawCircle(16, 16, 10.5f, 12.5f, new Color(1f, 0.85f, 0.35f, 0.35f));
            DrawLine(6, 6, 11, 11, 1, dark);
            DrawLine(6, 6, 10, 10, 0, steel);
            DrawLine(8, 12, 12, 8, 1, orange);
            DrawLine(11, 11, 26, 26, 2, cyanDark);
            DrawLine(11, 11, 26, 26, 1, cyan);
            DrawLine(12, 12, 25, 25, 0, Color.white);
            // Kesik efekti yayları
            DrawLine(16, 26, 26, 16, 0, yellow);
            DrawLine(19, 27, 27, 19, 0, Color.white);
        }
        else if (key.Contains("P_Guclu"))
        {
            // Ağır enerji çekici ve darbe dalgası
            DrawLine(6, 5, 20, 19, 1, dark);
            DrawLine(6, 5, 19, 18, 0, steel);
            // Çekiç başı
            for (int i = -6; i <= 6; i++)
            {
                for (int w = -3; w <= 3; w++)
                {
                    int hx = 20 + i - w;
                    int hy = 20 - i - w;
                    bool edge = Mathf.Abs(i) == 6 || Mathf.Abs(w) == 3;
                    Set(hx, hy, edge ? dark : (Mathf.Abs(i) >= 4 ? orange : red));
                }
            }
            // Parlama ve şok halkası
            DrawCircle(23, 9, 2f, 5.5f, yellow);
            DrawLine(18, 9, 28, 9, 0, Color.white);
            DrawLine(23, 4, 23, 14, 0, Color.white);
        }
        else if (key.Contains("P_Stun"))
        {
            // Elektrik şoku / yıldırım
            DrawCircle(16, 16, 9f, 12f, new Color(0.25f, 0.85f, 1f, 0.45f));
            DrawLine(19, 28, 11, 17, 2, dark);
            DrawLine(11, 17, 19, 17, 2, dark);
            DrawLine(19, 17, 11, 4, 2, dark);

            DrawLine(19, 28, 11, 17, 1, yellow);
            DrawLine(11, 17, 19, 17, 1, yellow);
            DrawLine(19, 17, 11, 4, 1, yellow);

            DrawLine(18, 26, 12, 17, 0, Color.white);
            DrawLine(18, 17, 12, 6, 0, Color.white);
            // Kıvılcımlar
            Set(6, 22, cyan); Set(7, 23, Color.white);
            Set(25, 22, cyan); Set(24, 23, Color.white);
            Set(7, 9, cyan); Set(25, 10, cyan);
        }
        else if (key.Contains("UzakSaldiri"))
        {
            // Lazer mermisi ve nişangah
            DrawCircle(22, 16, 6.5f, 8.2f, cyanDark);
            DrawLine(22, 5, 22, 11, 0, cyan);
            DrawLine(22, 21, 22, 27, 0, cyan);
            DrawLine(13, 16, 17, 16, 0, cyan);
            DrawLine(27, 16, 30, 16, 0, cyan);
            // Lazer oku
            DrawLine(3, 16, 21, 16, 2, cyanDark);
            DrawLine(4, 16, 21, 16, 1, cyan);
            DrawLine(6, 16, 22, 16, 0, Color.white);
            DrawCircle(22, 16, 0f, 2.2f, yellow);
        }
        else if (key.Contains("P_Uzak2x"))
        {
            // Riskli çift lazer ve keskin nişangah
            DrawCircle(21, 16, 7.5f, 9.2f, red);
            DrawLine(21, 4, 21, 10, 0, orange);
            DrawLine(21, 22, 21, 28, 0, orange);
            DrawLine(10, 16, 15, 16, 0, orange);
            DrawLine(27, 16, 30, 16, 0, orange);
            // İki güçlü ışın
            DrawLine(3, 21, 21, 17, 1, orange);
            DrawLine(3, 11, 21, 15, 1, orange);
            DrawLine(4, 21, 21, 17, 0, yellow);
            DrawLine(4, 11, 21, 15, 0, yellow);
            DrawCircle(21, 16, 0f, 3f, Color.white);
        }
        else if (key.Contains("AnaKalkan"))
        {
            // Enerji kalkanı
            for (int y = 4; y <= 27; y++)
            {
                int hw = y >= 16 ? 10 : Mathf.Clamp(y - 2, 2, 10);
                if (y >= 25) hw = 8;
                for (int x = 16 - hw; x <= 16 + hw; x++)
                {
                    int dx = Mathf.Abs(x - 16);
                    bool border = dx == hw || y == 4 || y == 27 || (y == 25 && dx >= 8);
                    bool rim = dx == hw - 1 || y == 26 || y == 5;
                    if (border) Set(x, y, dark);
                    else if (rim) Set(x, y, cyan);
                    else if (dx <= 4 && y >= 11 && y <= 21) Set(x, y, (dx + y) % 2 == 0 ? Color.white : cyan);
                    else Set(x, y, x < 16 ? new Color(0.18f, 0.68f, 0.96f, 1f) : cyanDark);
                }
            }
        }
        else if (key.Contains("P_Taunt"))
        {
            // Taunt: Kalkan + yanlardan yayılan dikkat dalgaları
            for (int y = 6; y <= 25; y++)
            {
                int hw = y >= 15 ? 7 : Mathf.Clamp(y - 4, 2, 7);
                for (int x = 16 - hw; x <= 16 + hw; x++)
                {
                    int dx = Mathf.Abs(x - 16);
                    bool border = dx == hw || y == 6 || y == 25;
                    bool rim = dx == hw - 1 || y == 24;
                    if (border) Set(x, y, dark);
                    else if (rim) Set(x, y, yellow);
                    else Set(x, y, orange);
                }
            }
            // Ünlem işareti ortada
            FillRect(15, 14, 17, 21, Color.white);
            FillRect(15, 10, 17, 12, Color.white);
            // Yan dalgalar
            DrawLine(5, 11, 3, 16, 0, yellow); DrawLine(3, 16, 5, 21, 0, yellow);
            DrawLine(8, 13, 6, 16, 0, orange); DrawLine(6, 16, 8, 19, 0, orange);
            DrawLine(26, 11, 28, 16, 0, yellow); DrawLine(28, 16, 26, 21, 0, yellow);
            DrawLine(23, 13, 25, 16, 0, orange); DrawLine(25, 16, 23, 19, 0, orange);
        }
        else if (key.Contains("P_CPU"))
        {
            // Parlayan işlemci (CPU) çipi
            for (int p = 9; p <= 22; p += 4)
            {
                FillRect(4, p, 7, p + 1, yellow);
                FillRect(24, p, 27, p + 1, yellow);
                FillRect(p, 4, p + 1, 7, yellow);
                FillRect(p, 24, p + 1, 27, yellow);
            }
            FillRect(7, 7, 24, 24, dark);
            FillRect(8, 8, 23, 23, cyanDark);
            FillRect(10, 10, 21, 21, new Color(0.06f, 0.22f, 0.34f, 1f));
            // Ortada + enerji
            FillRect(15, 12, 16, 19, cyan);
            FillRect(12, 15, 19, 16, cyan);
            FillRect(15, 15, 16, 16, Color.white);
        }
        else if (key.Contains("Sarj"))
        {
            // Enerji bataryası (+Şarj)
            FillRect(12, 25, 19, 27, steel);
            FillRect(8, 5, 23, 25, dark);
            FillRect(9, 6, 22, 24, new Color(0.08f, 0.20f, 0.16f, 1f));
            // Şarj dilimleri
            FillRect(11, 8, 20, 11, green);
            FillRect(11, 13, 20, 16, green);
            FillRect(11, 18, 20, 21, cyan);
            // Ortada parlak +
            FillRect(15, 11, 16, 19, Color.white);
            FillRect(12, 14, 19, 15, Color.white);
        }
        else if (key.Contains("Patlat"))
        {
            // Kendini patlat: radyoaktif patlama çekirdeği
            DrawLine(4, 4, 27, 27, 1, orange);
            DrawLine(4, 27, 27, 4, 1, orange);
            DrawLine(16, 2, 16, 29, 1, red);
            DrawLine(2, 16, 29, 16, 1, red);
            DrawCircle(16, 16, 6f, 10f, red);
            DrawCircle(16, 16, 3f, 6.5f, yellow);
            DrawCircle(16, 16, 0f, 3.2f, Color.white);
        }
        else
        {
            DrawCircle(16, 16, 4f, 11f, cyan);
        }

        t.SetPixels(px);
        t.Apply();
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 32f);
    }
}
