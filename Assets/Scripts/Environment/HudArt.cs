using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD (Hurda / CPU panelleri) için çalışma anında üretilen piksel art sprite'lar.
/// 1 sanat pikseli = 3 UI birimi (1920x1080 referans). Point filtre, her ölçekte keskin.
/// </summary>
public static class HudArt
{
    public const float Px = 3f;           // 1 sanat pikseli kaç UI birimi
    private const float Ppu = 100f / Px;  // Canvas referans PPU 100 → texel başına 3 birim

    public static readonly Color32 Outline = new Color32(6, 13, 20, 255);
    public static readonly Color32 Fill = new Color32(12, 34, 46, 235);
    public static readonly Color32 Socket = new Color32(18, 46, 60, 255);
    public static readonly Color32 Cyan = new Color32(89, 242, 255, 255);
    public static readonly Color32 CyanMid = new Color32(36, 150, 190, 255);
    public static readonly Color32 Amber = new Color32(255, 184, 51, 255);
    public static readonly Color32 AmberDark = new Color32(150, 92, 18, 255);
    public static readonly Color32 Orange = new Color32(255, 138, 36, 255);
    public static readonly Color32 Red = new Color32(255, 70, 60, 255);
    public static readonly Color32 Dim = new Color32(120, 160, 170, 255);
    public static readonly Color32 Green = new Color32(120, 255, 150, 255);
    public static readonly Color32 White = new Color32(225, 255, 255, 255);

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ------------------------------------------------------------ panel (9-slice)

    /// <summary>Kesik köşeli panel: sol üst ve sağ alt köşe 45° kesik. Sliced Image olarak kullanılır.</summary>
    public static Sprite Panel(Color32 border) => Panel(border, Fill);

    public static Sprite Panel(Color32 border, Color32 fill)
    {
        string key = "panel_" + border.r + "_" + border.g + "_" + border.b + "_" + fill.r + "_" + fill.g + "_" + fill.b + "_" + fill.a;
        if (cache.TryGetValue(key, out Sprite s) && s != null) return s;

        const int n = 16, cut = 5;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color32 c = new Color32(0, 0, 0, 0);
                if (Inside(x, y, n, cut, 0)) c = Outline;
                if (Inside(x, y, n, cut, 1)) c = border;
                if (Inside(x, y, n, cut, 2)) c = fill;
                px[(n - 1 - y) * n + x] = c; // y=0 üst satır
            }
        Texture2D t = Tex(px, n, n);
        s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect, new Vector4(7, 7, 7, 7));
        s.name = key;
        cache[key] = s;
        return s;
    }

    private static bool Inside(int x, int y, int n, int cut, int inset)
    {
        if (x < inset || y < inset || x > n - 1 - inset || y > n - 1 - inset) return false;
        if (x + y < cut + inset) return false;                         // sol üst kesik
        if ((n - 1 - x) + (n - 1 - y) < cut + inset) return false;     // sağ alt kesik
        return true;
    }

    // ------------------------------------------------------------ hücreler

    public enum CellState { Empty, On, Spend, Short }

    /// <summary>8x13 batarya hücresi.</summary>
    public static Sprite Cell(CellState st)
    {
        string key = "cell_" + st;
        if (cache.TryGetValue(key, out Sprite s) && s != null) return s;

        const int w = 8, h = 13;
        Color32 top, bottom, edge = Outline;
        switch (st)
        {
            case CellState.On: top = Cyan; bottom = CyanMid; break;
            case CellState.Spend: top = Orange; bottom = new Color32(170, 80, 20, 255); break;
            case CellState.Short: top = new Color32(60, 14, 16, 255); bottom = new Color32(40, 10, 12, 255); edge = Red; break;
            default: top = bottom = new Color32(14, 30, 38, 255); break;
        }
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? edge : (y < 6 ? top : bottom);
                if ((st == CellState.On || st == CellState.Spend) && x == 1 && y == 1) c = White;
                px[(h - 1 - y) * w + x] = c;
            }
        s = Sprite.Create(Tex(px, w, h), new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu);
        s.name = key;
        cache[key] = s;
        return s;
    }

    // ------------------------------------------------------------ ikonlar

    private static readonly string[] ScrapRows =
    {
        "....KKKK......",
        "...KGGGGK.....",
        ".KKGgggGKK....",
        "KGGgKKKgGGK...",
        "KGgK...KgGK...",
        "KGgK...KgGKKK.",
        ".KGgKKKgGKAAK.",
        ".KGGgggGGKAWAK",
        "..KKGGGGKKAAAK",
        "....KKKK.KAaAK",
        "..........KAK.",
        "...........K..",
        "..............",
        "..............",
    };

    private static readonly string[] ChipRows =
    {
        ".K.K.K.K.",
        "KKKKKKKKK",
        ".KcccccK.",
        "KKcCCCcKK",
        ".KcCWCcK.",
        "KKcCCCcKK",
        ".KcccccK.",
        "KKKKKKKKK",
        ".K.K.K.K.",
    };

    // Kart hakkı ikonu (CPU kullanmayan dost birimler için)
    private static readonly string[] CardRows =
    {
        ".KKKKKK..",
        ".KCCCCKK.",
        ".KCcccCK.",
        "KKCcWcCK.",
        "KCKcccCK.",
        "KCKCCCCK.",
        "KCKKKKKK.",
        "KCCCCCK..",
        "KKKKKKK..",
    };


    // ---- tur çubuğu ikonları (7x7) ----
    private static readonly string[] RangedRows = { "...K...", "..KCK..", "KKKCKKK", "KCCWCCK", "KKKCKKK", "..KCK..", "...K..." };
    private static readonly string[] MeleeRows  = { "....KKK", "...KAAK", "..KAAK.", "KKAAK..", "KAAK...", "KKK....", "......." };
    private static readonly string[] BossRows   = { "K.K.K.K", "KRKRKRK", "KRRRRRK", "KRWRWRK", "KRRRRRK", "KKKKKKK", "......." };
    private static readonly string[] StunRows   = { "..KKK..", ".KYYK..", ".KYK...", "KYYYYK.", "..KYK..", "..KK...", "......." };
    private static readonly string[] TauntRows  = { "KKKKKKK", "KAAAAAK", "KAKKKAK", "KAAAAAK", ".KAAAK.", "..KAK..", "...K..." };
    private static readonly string[] CheckRows  = { "......K", ".....KL", "K...KLK", "LK.KLK.", "KLKLK..", ".KLK...", "..K...." };
    private static readonly string[] StarRows   = { "...K...", "..KCK..", "KKKCKKK", "KCCWCCK", ".KCCCK.", "KCK.KCK", "KK...KK" };
    private static readonly string[] ArrowRows  = { "KKKKKKKKK", "KWWWWWWWK", ".KWWWWWK.", "..KWWWK..", "...KWK...", "....K...." };

    public static Sprite RangedIcon => Icon("i_ranged", RangedRows);
    public static Sprite MeleeIcon => Icon("i_melee", MeleeRows);
    public static Sprite BossIcon => Icon("i_boss", BossRows);
    public static Sprite StunIcon => Icon("i_stun", StunRows);
    public static Sprite TauntIcon => Icon("i_taunt", TauntRows);
    public static Sprite CheckIcon => Icon("i_check", CheckRows);
    public static Sprite StarIcon => Icon("i_star", StarRows);
    /// <summary>Beyaz üçgen; Image.color ile renklendirilir.</summary>
    public static Sprite ArrowDown => Icon("i_arrow", ArrowRows);


    // ---- kart ikonları (7x7) ----
    private static readonly string[] CardSwordRows  = { "....KKK", "...KGWK", "..KGWK.", "KKGWK..", "KgGK...", "KKK....", "......." };
    private static readonly string[] CardShieldRows = { "KKKKKKK", "KGGGGGK", "KGWWWGK", "KGWWWGK", ".KGWGK.", "..KGK..", "...K..." };
    private static readonly string[] CritRows       = { "...K...", "..KOK..", ".KOAOK.", "KOAWAOK", ".KOAOK.", "..KOK..", "...K..." };
    private static readonly string[] GoldStarRows   = { "...K...", "..KYK..", "KKKYKKK", "KYYWYYK", ".KYYYK.", "KYK.KYK", "KK...KK" };

    public static Sprite CardSwordIcon => Icon("i_csword", CardSwordRows);
    public static Sprite CardShieldIcon => Icon("i_cshield", CardShieldRows);
    public static Sprite CritIcon => Icon("i_crit", CritRows);
    public static Sprite GoldStarIcon => Icon("i_gstar", GoldStarRows);


    // ---- ayarlar menüsü ikonları (beyaz; Image.color ile renklenir) ----
    private static readonly string[] SpeakerRows = { "....KK...", "...KWK.W.", "KKKWWK..W", "KWWWWK.W.", "KWWWWK.W.", "KKKWWK..W", "...KWK.W.", "....KK..." };
    private static readonly string[] SpeakerMuteRows = { "....KK...", "...KGK...", "KKKGGKR.R", "KGGGGK.R.", "KGGGGKR.R", "KKKGGK...", "...KGK...", "....KK..." };
    private static readonly string[] NoteRows = { "...KKKK", "...KWWK", "...KW.K", "...KW.K", ".KKKW.K", "KWWWKKK", "KWWWK..", ".KKK..." };
    private static readonly string[] PlayRows = { "KK....", "KWK...", "KWWK..", "KWWWK.", "KWWK..", "KWK...", "KK...." };
    private static readonly string[] HomeRows = { "...K...", "..KWK..", ".KWWWK.", "KWWWWWK", ".KW.WK.", ".KW.WK.", ".KKKKK." };
    private static readonly string[] ExitRows = { "KKKK...", "KWWK.K.", "KW.KKWK", "KW.WWWK", "KW.KKWK", "KWWK.K.", "KKKK..." };

    public static Sprite SpeakerIcon => Icon("i_spk", SpeakerRows);
    public static Sprite SpeakerMuteIcon => Icon("i_spkm", SpeakerMuteRows);
    public static Sprite NoteIcon => Icon("i_note", NoteRows);
    public static Sprite PlayIcon => Icon("i_play", PlayRows);
    public static Sprite HomeIcon => Icon("i_home", HomeRows);
    public static Sprite ExitIcon => Icon("i_exit", ExitRows);

    /// <summary>Ayarlar sürgüsü hücresi (6x8): dolu camgöbeği ya da boş.</summary>
    public static Sprite SettingCell(bool on)
    {
        string key = on ? "scell_on" : "scell_off";
        if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
        const int w = 6, h = 8;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1) c = Outline;
                else if (!on) c = new Color32(16, 32, 40, 255);
                else c = y < 4 ? Cyan : CyanMid;
                if (on && x == 1 && y == 1) c = White;
                px[(h - 1 - y) * w + x] = c;
            }
        s = Sprite.Create(Tex(px, w, h), new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu);
        s.name = key;
        cache[key] = s;
        return s;
    }

    // Asıl ikonlar Assets/Prefabs/Kaynaklar içindeki Hurda.png ve CPU.png; bulunamazsa piksel çizim yedeği kullanılır.
    public static Sprite ScrapIcon => RuntimeArt.ScrapIcon != null ? RuntimeArt.ScrapIcon : Icon("scrap", ScrapRows);
    public static Sprite ChipIcon => RuntimeArt.CpuIcon != null ? RuntimeArt.CpuIcon : Icon("chip", ChipRows);
    public static Sprite CardIcon => Icon("card", CardRows);

    private static Sprite Icon(string key, string[] rows)
    {
        if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
        int h = rows.Length, w = rows[0].Length;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = Pal(rows[y][x]);
        s = Sprite.Create(Tex(px, w, h), new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu);
        s.name = key;
        cache[key] = s;
        return s;
    }

    private static Color32 Pal(char c)
    {
        switch (c)
        {
            case 'K': return Outline;
            case 'G': return new Color32(150, 165, 175, 255);
            case 'g': return new Color32(90, 105, 115, 255);
            case 'A': return Amber;
            case 'a': return AmberDark;
            case 'W': return White;
            case 'C': return Cyan;
            case 'c': return CyanMid;
            case 'R': return Red;
            case 'Y': return new Color32(255, 230, 80, 255);
            case 'L': return Green;
            case 'O': return Orange;
            default: return new Color32(0, 0, 0, 0);
        }
    }

    public static Sprite Solid
    {
        get
        {
            if (cache.TryGetValue("solid", out Sprite s) && s != null) return s;
            Color32[] px = { new Color32(255, 255, 255, 255) };
            s = Sprite.Create(Tex(px, 1, 1), new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), Ppu);
            cache["solid"] = s;
            return s;
        }
    }

    private static Texture2D Tex(Color32[] px, int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }
}
