using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kart görselleri (çalışma anında üretilir, Point filtre).
/// Izgara 48x64 sanat pikseli:
///   isim şeridi (3,3)-(44,11) · ikon penceresi (5,14)-(42,33) · açıklama kutusu (4,36)-(43,52)
///   CPU rozeti (32,54)-(45,62) · hurda rozeti / tip ikonu (2,54)-(15,62)
/// CardView bu oranlara göre yerleşir.
/// </summary>
public static class CardArt
{
    public const int W = 48, H = 64;

    public static readonly RectInt Header = new RectInt(3, 3, 42, 9);
    public static readonly RectInt ArtWin = new RectInt(5, 14, 38, 20);
    public static readonly RectInt Stats = new RectInt(4, 36, 40, 17);
    public static readonly RectInt CpuBadge = new RectInt(32, 54, 14, 9);
    public static readonly RectInt LeftBadge = new RectInt(2, 54, 14, 9);

    public static readonly Color32 SummonBase = new Color32(150, 76, 20, 255);
    public static readonly Color32 Orange = new Color32(245, 138, 36, 255);
    public static readonly Color32 Gold = new Color32(255, 205, 80, 255);
    private static readonly Color32 K = new Color32(6, 13, 20, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Black = new Color32(0, 0, 0, 255);

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Sanat pikseli dikdörtgenini (sol üst kökenli) kartın anchor değerlerine çevirir.</summary>
    public static void Anchor(RectTransform rt, RectInt r, float inset = 0f)
    {
        rt.anchorMin = new Vector2((r.x + inset) / W, 1f - (r.y + r.height - inset) / H);
        rt.anchorMax = new Vector2((r.x + r.width - inset) / W, 1f - (r.y + inset) / H);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // ------------------------------------------------------------------ kart gövdesi

    public static Sprite Body(Color32 baseC, bool bp, bool strong, bool disabled)
    {
        string key = "body_" + baseC.r + "_" + baseC.g + "_" + baseC.b + (bp ? "_bp" : "") + (strong ? "_s" : "") + (disabled ? "_d" : "");
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Color32[] px = new Color32[W * H];
        void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) px[(H - 1 - y) * W + x] = c; }

        Color32 top = bp ? new Color32(66, 35, 13, 255) : Lt(baseC, 0.06f);
        Color32 bot = bp ? new Color32(38, 20, 8, 255) : Dk(baseC, 0.18f);
        Color32 rimLight = bp ? Orange : Lt(baseC, 0.45f);
        Color32 rimDark = bp ? Dk(Orange, 0.4f) : Dk(baseC, 0.45f);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color32 c;
                if (x == 0 || y == 0 || x == W - 1 || y == H - 1) c = K;
                else if (x == 1 || y == 1) c = rimLight;
                else if (x == W - 2 || y == H - 2) c = rimDark;
                else
                {
                    c = Mix(top, bot, y / (H - 1f));
                    if (bp)
                    {
                        if (x % 8 == 4 || y % 8 == 4) c = Mix(c, Orange, 0.26f);
                        else if (x % 4 == 0 || y % 4 == 0) c = Mix(c, Orange, 0.10f);
                    }
                    else if ((x + y) % 5 == 0) c = Lt(c, 0.07f);
                }
                Set(x, y, c);
            }

        // isim şeridi
        Fill(Set, Header, bp ? new Color32(26, 13, 5, 255) : Dk(baseC, 0.38f));
        for (int x = Header.x; x < Header.xMax; x++) Set(x, Header.yMax, bp ? Orange : Lt(baseC, 0.35f));

        // ikon penceresi: gömülü panel + ortadan ışıma
        RectInt a = ArtWin;
        Fill(Set, a, bp ? new Color32(20, 10, 4, 255) : Dk(baseC, 0.5f));
        Color32 inner = bp ? new Color32(26, 13, 5, 255) : Dk(baseC, 0.32f);
        Color32 glow = bp ? Orange : Lt(baseC, 0.15f);
        float cx = a.x + (a.width - 1) * 0.5f, cy = a.y + (a.height - 1) * 0.5f;
        for (int y = a.y + 1; y < a.yMax - 1; y++)
            for (int x = a.x + 1; x < a.xMax - 1; x++)
            {
                float dx = (x - cx) / (a.width - 1) * 2f, dy = (y - cy) / (a.height - 1) * 2f;
                float g = Mathf.Max(0f, 1f - Mathf.Sqrt(dx * dx + dy * dy));
                Color32 c = Mix(inner, glow, (bp ? 0.45f : 0.5f) * g * g);
                if (bp && (x % 8 == 4 || y % 8 == 4)) c = Mix(c, Orange, 0.14f);
                Set(x, y, c);
            }
        Color32 hl = bp ? Dk(Orange, 0.2f) : Lt(baseC, 0.25f);
        for (int x = a.x + 1; x < a.xMax; x++) Set(x, a.yMax - 1, hl);
        for (int y = a.y + 1; y < a.yMax; y++) Set(a.xMax - 1, y, hl);
        Color32 acc = bp ? new Color32(255, 184, 51, 255) : Lt(baseC, 0.6f);
        int ax0 = a.x + 2, ay0 = a.y + 2, ax1 = a.xMax - 3, ay1 = a.yMax - 3;
        for (int i = 0; i < 4; i++)
        {
            Set(ax0 + i, ay0, acc); Set(ax0, ay0 + i, acc);
            Set(ax1 - i, ay0, acc); Set(ax1, ay0 + i, acc);
            Set(ax0 + i, ay1, acc); Set(ax0, ay1 - i, acc);
            Set(ax1 - i, ay1, acc); Set(ax1, ay1 - i, acc);
        }

        // açıklama kutusu
        RectInt s = Stats;
        Fill(Set, s, bp ? Orange : Lt(baseC, 0.28f));
        Fill(Set, new RectInt(s.x + 1, s.y + 1, s.width - 2, s.height - 2), bp ? new Color32(24, 12, 4, 255) : Dk(baseC, 0.42f));

        // güçlü kart: altın iç çerçeve
        if (strong)
        {
            for (int x = 2; x < W - 2; x++) { Set(x, 2, Gold); Set(x, H - 3, Gold); }
            for (int y = 2; y < H - 2; y++) { Set(2, y, Gold); Set(W - 3, y, Gold); }
        }

        if (disabled)
        {
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c = px[i];
                byte l = (byte)(c.r * 0.3f + c.g * 0.59f + c.b * 0.11f);
                Color32 g = new Color32(l, l, l, c.a);
                Color32 m = Mix(g, c, 0.35f);
                px[i] = new Color32((byte)(m.r * 0.62f), (byte)(m.g * 0.62f), (byte)(m.b * 0.62f), c.a);
            }
        }

        Sprite sp = Make(px, W, H, key);
        cache[key] = sp;
        return sp;
    }

    // ------------------------------------------------------------------ rozet (CPU / hurda)

    public static Sprite Badge(Color32 baseC, bool bp, bool red)
    {
        string key = "badge_" + baseC.r + "_" + baseC.g + "_" + baseC.b + (bp ? "_bp" : "") + (red ? "_r" : "");
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        int w = CpuBadge.width, h = CpuBadge.height;
        Color32 rim = red ? new Color32(255, 70, 60, 255) : (bp ? Orange : Lt(baseC, 0.45f));
        Color32 fill = red ? new Color32(60, 12, 14, 255) : (bp ? new Color32(24, 12, 4, 255) : Dk(baseC, 0.5f));
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? rim : fill;
        Sprite sp = Make(px, w, h, key);
        cache[key] = sp;
        return sp;
    }

    // ------------------------------------------------------------------ efektler

    /// <summary>Beyaz, ortası dolu yumuşak oval ışıma (Image.color ile renklenir).</summary>
    public static Sprite Glow
    {
        get
        {
            if (cache.TryGetValue("glow", out Sprite cached) && cached != null) return cached;
            const int n = 32;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - 15.5f) / 15.5f, dy = (y - 15.5f) / 15.5f;
                    float g = Mathf.Max(0f, 1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * g * g));
                }
            Sprite sp = Make(px, n, n, "glow", FilterMode.Bilinear);
            cache["glow"] = sp;
            return sp;
        }
    }

    /// <summary>Hologram tarama çizgileri (ikon penceresi boyutunda).</summary>
    public static Sprite HoloLines
    {
        get
        {
            if (cache.TryGetValue("holo", out Sprite cached) && cached != null) return cached;
            int w = ArtWin.width - 2, h = (ArtWin.height - 2) * 2;
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = (y % 2 == 0) ? new Color32(255, 170, 60, 40) : new Color32(0, 0, 0, 0);
            Sprite sp = Make(px, w, h, "holo");
            cache["holo"] = sp;
            return sp;
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    private delegate void Setter(int x, int y, Color32 c);

    private static void Fill(Setter set, RectInt r, Color32 c)
    {
        for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
                set(x, y, c);
    }

    private static Sprite Make(Color32[] px, int w, int h, string name, FilterMode filter = FilterMode.Point)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = filter;
        t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels32(px);
        t.Apply(false, true);
        t.name = name;
        Sprite s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        s.name = name;
        return s;
    }

    public static Color32 Mix(Color32 a, Color32 b, float t)
    {
        return new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);
    }
    public static Color32 Lt(Color32 c, float t) => Mix(c, White, t);
    public static Color32 Dk(Color32 c, float t) => Mix(c, Black, t);
}
