using UnityEngine;

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
}
