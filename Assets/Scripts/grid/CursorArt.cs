using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Protocol_Human imleç sanatı. Tüm imleç ve kare göstergeleri çalışma anında,
/// piksel haritalarından üretilir (asset import ayarı gerekmez, her zaman keskin).
/// Fare imleçleri 16x16 çizilip ekran yüksekliğine göre 2x/3x/4x büyütülür.
/// Bu dosya cursor_art.py ile üretildi; çizimi değiştirmek için piksel haritalarını düzenle.
/// </summary>
public static class CursorArt
{
    public struct CursorDef
    {
        public int hotX, hotY;
        public string[] rows;
        public CursorDef(int hx, int hy, string[] r) { hotX = hx; hotY = hy; rows = r; }
    }

    private static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { '.', new Color32(0, 0, 0, 0) },
        { 'K', new Color32(6, 13, 20, 255) },
        { 'D', new Color32(12, 34, 46, 255) },
        { 'C', new Color32(89, 242, 255, 255) },
        { 'c', new Color32(36, 150, 190, 255) },
        { 'W', new Color32(225, 255, 255, 255) },
        { 'A', new Color32(255, 184, 51, 255) },
        { 'a', new Color32(190, 112, 20, 255) },
        { 'R', new Color32(255, 70, 60, 255) },
        { 'r', new Color32(150, 28, 30, 255) },
        { 'G', new Color32(142, 155, 168, 255) },
        { 'g', new Color32(80, 92, 104, 255) },
        { 'L', new Color32(120, 255, 150, 255) },
    };

    public static readonly Dictionary<string, CursorDef> Os = new Dictionary<string, CursorDef>
    {
        { "default", new CursorDef(0, 0, new[] {
            "KKK.............",
            "KWKK............",
            "KWWKK...........",
            "KCDCKK..........",
            "KCDDCKK.........",
            "KCDDDCKK........",
            "KCDDDDCKK.......",
            "KCDDDDDCKK......",
            "KCDDDDDDCKK.....",
            "KCDDDDDDCCK.....",
            "KCDDDDDCKKK.....",
            "KCDDCCDCKK......",
            "KCCCKKCDCK......",
            "KKKKKKKCCKK.....",
            "......KCCCK.....",
            "......KKKKK.....",
        }) },
        { "interact", new CursorDef(0, 0, new[] {
            "KKK.............",
            "KWKK............",
            "KWWKK...........",
            "KADAKK..........",
            "KADDAKK.........",
            "KADDDAKK........",
            "KADDDDAKK.......",
            "KADDDDDAKK......",
            "KADDDDDDAKK.....",
            "KADDDDDDAAK.....",
            "KADDDDDAKKK.....",
            "KADDAADAKK.KKKKK",
            "KAAAKKADAK.KAAAK",
            "KKKKKKKAAKKKAWAK",
            "......KAAAKKAAAK",
            "......KKKKKKKKKK",
        }) },
        { "scan", new CursorDef(6, 6, new[] {
            "................",
            "....KKKKK.......",
            "...KAAAAAK......",
            "..KAKKKKKAK.....",
            ".KAKDDDDDKAK....",
            ".KAKDDCDDKAK....",
            ".KAKDCWCDKAK....",
            ".KAKDDCDDKAK....",
            ".KAKDDDDDKAK....",
            "..KAKKKKKAAK....",
            "...KAAAAAKAAK...",
            "....KKKKKKKAAK..",
            "...........KAAK.",
            "............KAK.",
            ".............K..",
            "................",
        }) },
        { "blocked", new CursorDef(0, 0, new[] {
            "KKK.............",
            "KGKK............",
            "KGGKK...........",
            "KGDGKK..........",
            "KGDDGKK.........",
            "KGDDDGKK........",
            "KGDDDDGKK.......",
            "KGDDDDDGKK......",
            "KGDDDDDDGKK.....",
            "KGDDDDDDGGK.....",
            "KGDDDDDGKKKKKKKK",
            "KGDDGGDGKKKRKKRK",
            "KGGGKKGDGKKKRRKK",
            "KKKKKKKGGKKKRRKK",
            "......KGGGKRKKRK",
            "......KKKKKKKKKK",
        }) },
        { "target", new CursorDef(7, 7, new[] {
            "......KRRK......",
            "......KRRK......",
            ".....KKRRKK.....",
            "....KRRRRRRK....",
            "...KRKKKKKKRK...",
            "..KRK......KRK..",
            "KKKRK..KK..KRKKK",
            "RRRRK.KWWK.KRRRR",
            "RRRRK.KWWK.KRRRR",
            "KKKRK..KK..KRKKK",
            "..KRK......KRK..",
            "...KRKKKKKKRK...",
            "....KRRRRRRK....",
            ".....KKRRKK.....",
            "......KRRK......",
            "......KRRK......",
        }) },
        { "target_invalid", new CursorDef(7, 7, new[] {
            "......KGGK......",
            "......KGGK......",
            ".....KKGGKK.....",
            "....KGGGGGGK....",
            "...KGKKKKKKGK...",
            "..KGK......KGK..",
            "KKKGK..KK..KGKKK",
            "GGGGK.KggK.KGGGG",
            "GGGGK.KggK.KGGGG",
            "KKKGK..KK..KGKKK",
            "..KGK......KGK..",
            "...KGKKKKKKGK...",
            "....KGGGGGGK....",
            ".....KKGGKK.....",
            "......KGGK......",
            "......KGGK......",
        }) },
        { "wait_0", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KWWK......",
            "...KK.KWWK.KK...",
            "..KCCK.KK.KccK..",
            "..KCCK....KccK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KCCK........KccK",
            "KCCK........KccK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KccK....KccK..",
            "..KccK.KK.KccK..",
            "...KK.KccK.KK...",
            "......KccK......",
            ".......KK.......",
        }) },
        { "wait_1", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KCCK......",
            "...KK.KCCK.KK...",
            "..KCCK.KK.KWWK..",
            "..KCCK....KWWK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KccK........KccK",
            "KccK........KccK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KccK....KccK..",
            "..KccK.KK.KccK..",
            "...KK.KccK.KK...",
            "......KccK......",
            ".......KK.......",
        }) },
        { "wait_2", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KCCK......",
            "...KK.KCCK.KK...",
            "..KccK.KK.KCCK..",
            "..KccK....KCCK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KccK........KWWK",
            "KccK........KWWK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KccK....KccK..",
            "..KccK.KK.KccK..",
            "...KK.KccK.KK...",
            "......KccK......",
            ".......KK.......",
        }) },
        { "wait_3", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KccK......",
            "...KK.KccK.KK...",
            "..KccK.KK.KCCK..",
            "..KccK....KCCK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KccK........KCCK",
            "KccK........KCCK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KccK....KWWK..",
            "..KccK.KK.KWWK..",
            "...KK.KccK.KK...",
            "......KccK......",
            ".......KK.......",
        }) },
        { "wait_4", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KccK......",
            "...KK.KccK.KK...",
            "..KccK.KK.KccK..",
            "..KccK....KccK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KccK........KCCK",
            "KccK........KCCK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KccK....KCCK..",
            "..KccK.KK.KCCK..",
            "...KK.KWWK.KK...",
            "......KWWK......",
            ".......KK.......",
        }) },
        { "wait_5", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KccK......",
            "...KK.KccK.KK...",
            "..KccK.KK.KccK..",
            "..KccK....KccK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KccK........KccK",
            "KccK........KccK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KWWK....KCCK..",
            "..KWWK.KK.KCCK..",
            "...KK.KCCK.KK...",
            "......KCCK......",
            ".......KK.......",
        }) },
        { "wait_6", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KccK......",
            "...KK.KccK.KK...",
            "..KccK.KK.KccK..",
            "..KccK....KccK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KWWK........KccK",
            "KWWK........KccK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KCCK....KccK..",
            "..KCCK.KK.KccK..",
            "...KK.KCCK.KK...",
            "......KCCK......",
            ".......KK.......",
        }) },
        { "wait_7", new CursorDef(7, 7, new[] {
            ".......KK.......",
            "......KccK......",
            "...KK.KccK.KK...",
            "..KWWK.KK.KccK..",
            "..KWWK....KccK..",
            "...KK......KK...",
            ".KK..........KK.",
            "KCCK........KccK",
            "KCCK........KccK",
            ".KK..........KK.",
            "...KK......KK...",
            "..KCCK....KccK..",
            "..KCCK.KK.KccK..",
            "...KK.KccK.KK...",
            "......KccK......",
            ".......KK.......",
        }) },
    };

    private static readonly Dictionary<string, string[]> Grid = new Dictionary<string, string[]>
    {
        { "move", new[] {
            "KKKKKKKKK..............KKKKKKKKK",
            "KCCCCCCCK..............KCCCCCCCK",
            "KCCCCCCCK..............KCCCCCCCK",
            "KCCKKKKKK..............KKKKKKCCK",
            "KCCK........................KCCK",
            "KCCK........................KCCK",
            "KCCK........................KCCK",
            "KCCK........................KCCK",
            "KKKK........................KKKK",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "KKKK........................KKKK",
            "KCCK........................KCCK",
            "KCCK........................KCCK",
            "KCCK........................KCCK",
            "KCCK........................KCCK",
            "KCCKKKKKK..............KKKKKKCCK",
            "KCCCCCCCK..............KCCCCCCCK",
            "KCCCCCCCK..............KCCCCCCCK",
            "KKKKKKKKK..............KKKKKKKKK",
        } },
        { "idle", new[] {
            "KKKKKKK..................KKKKKKK",
            "KGGGGGK..................KGGGGGK",
            "KGKKKKK..................KKKKKGK",
            "KGK..........................KGK",
            "KGK..........................KGK",
            "KGK..........................KGK",
            "KKK..........................KKK",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "KKK..........................KKK",
            "KGK..........................KGK",
            "KGK..........................KGK",
            "KGK..........................KGK",
            "KGKKKKK..................KKKKKGK",
            "KGGGGGK..................KGGGGGK",
            "KKKKKKK..................KKKKKKK",
        } },
        { "target", new[] {
            "KKKKKKKKKK...KKKKKK...KKKKKKKKKK",
            "KRRRRRRRRK...KRRRRK...KRRRRRRRRK",
            "KRRRRRRRRK...KRRRRK...KRRRRRRRRK",
            "KRRKKKKKKK...KKKKKK...KKKKKKKRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KKKK........................KKKK",
            "................................",
            "................................",
            "................................",
            "KKKK........................KKKK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KKKK........................KKKK",
            "................................",
            "................................",
            "................................",
            "KKKK........................KKKK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRK........................KRRK",
            "KRRKKKKKKK...KKKKKK...KKKKKKKRRK",
            "KRRRRRRRRK...KRRRRK...KRRRRRRRRK",
            "KRRRRRRRRK...KRRRRK...KRRRRRRRRK",
            "KKKKKKKKKK...KKKKKK...KKKKKKKKKK",
        } },
        { "interact", new[] {
            "KKKKKKKKK..............KKKKKKKKK",
            "KAAAAAAAK..............KAAAAAAAK",
            "KAAAAAAAK..............KAAAAAAAK",
            "KAAKKKKKK..............KKKKKKAAK",
            "KAAK........................KAAK",
            "KAAK........................KAAK",
            "KAAK........................KAAK",
            "KAAK........................KAAK",
            "KKKK........................KKKK",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "KKKK........................KKKK",
            "KAAK........................KAAK",
            "KAAK........................KAAK",
            "KAAK........................KAAK",
            "KAAK........................KAAK",
            "KAAKKKKKK..............KKKKKKAAK",
            "KAAAAAAAK..............KAAAAAAAK",
            "KAAAAAAAK..............KAAAAAAAK",
            "KKKKKKKKK..............KKKKKKKKK",
        } },
        { "ally", new[] {
            "KKKKKKKKK..............KKKKKKKKK",
            "KLLLLLLLK..............KLLLLLLLK",
            "KLLLLLLLK..............KLLLLLLLK",
            "KLLKKKKKK..............KKKKKKLLK",
            "KLLK........................KLLK",
            "KLLK........................KLLK",
            "KLLK........................KLLK",
            "KLLK........................KLLK",
            "KKKK........................KKKK",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "KKKK........................KKKK",
            "KLLK........................KLLK",
            "KLLK........................KLLK",
            "KLLK........................KLLK",
            "KLLK........................KLLK",
            "KLLKKKKKK..............KKKKKKLLK",
            "KLLLLLLLK..............KLLLLLLLK",
            "KLLLLLLLK..............KLLLLLLLK",
            "KKKKKKKKK..............KKKKKKKKK",
        } },
        { "blocked", new[] {
            "KKKKKK....................KKKKKK",
            "KRRRRK....................KRRRRK",
            "KRKKKK....................KKKKRK",
            "KRK..........................KRK",
            "KRK..........................KRK",
            "KKK..........................KKK",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "............KKKKKKKK............",
            "............KRRKKRRK............",
            "............KKRRRRKK............",
            ".............KKRRKK.............",
            "............KKRRRRKK............",
            "............KRRKKRRK............",
            "............KKKKKKKK............",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "KKK..........................KKK",
            "KRK..........................KRK",
            "KRK..........................KRK",
            "KRKKKK....................KKKKRK",
            "KRRRRK....................KRRRRK",
            "KKKKKK....................KKKKKK",
        } },
    };

    public const int WaitFrames = 8;

    // ---------------- Fare imleçleri ----------------
    private static readonly Dictionary<string, Texture2D> osCache = new Dictionary<string, Texture2D>();

    /// <summary>Ekran yüksekliğine göre büyütme: 1080p ve altı 2x (32px), 1440p 3x (48px), 4K 4x (64px).</summary>
    public static int AutoScale()
    {
        int h = Screen.height;
        if (h >= 2000) return 4;
        if (h >= 1300) return 3;
        return 2;
    }

    public static Texture2D GetOsTexture(string name, int scale, out Vector2 hotspot)
    {
        CursorDef def = Os[name];
        hotspot = new Vector2(def.hotX * scale + scale * 0.5f, def.hotY * scale + scale * 0.5f);
        if (def.hotX == 0 && def.hotY == 0) hotspot = Vector2.zero;
        string key = name + "@" + scale;
        if (osCache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
        Texture2D t = Build(def.rows, scale);
        t.name = "cursor_" + key;
        osCache[key] = t;
        return t;
    }

    // ---------------- Dünya sprite'ları ----------------
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    public static Sprite GetGridSprite(string name)
    {
        string key = "grid_" + name;
        if (spriteCache.TryGetValue(key, out Sprite s) && s != null) return s;
        s = ToSprite(Build(Grid[name], 1), key);
        spriteCache[key] = s;
        return s;
    }

    public static readonly Color32 RangeFill = new Color32(89, 242, 255, 22);
    public static readonly Color32 RangeEdge = new Color32(89, 242, 255, 200);
    public static readonly Color32 TargetFill = new Color32(255, 70, 60, 46);
    public static readonly Color32 TargetEdge = new Color32(255, 70, 60, 230);

    /// <summary>Hareket menzili karosu. mask: 1=yukarıda komşu, 2=sağda, 4=aşağıda, 8=solda. Komşusu olmayan kenara çizgi çekilir.</summary>
    public static Sprite GetRangeTile(int mask) => EdgeTile("range_", mask, RangeFill, RangeEdge);

    public static readonly Color32 AttackRangeFill = new Color32(255, 120, 50, 30);
    public static readonly Color32 AttackRangeEdge = new Color32(255, 120, 50, 210);

    /// <summary>Saldırı kartının menzil alanı (turuncu). mask, GetRangeTile ile aynı.</summary>
    public static Sprite GetAttackRangeTile(int mask) => EdgeTile("atkrange_", mask, AttackRangeFill, AttackRangeEdge);

    private static Sprite EdgeTile(string prefix, int mask, Color32 fill, Color32 edge)
    {
        string key = prefix + mask;
        if (spriteCache.TryGetValue(key, out Sprite s) && s != null) return s;
        Color32[] px = new Color32[32 * 32];
        for (int i = 0; i < px.Length; i++) px[i] = fill;
        for (int i = 0; i < 32; i++)
        {
            // Unity dokusunda y=0 alt satırdır
            if ((mask & 1) == 0) { px[31 * 32 + i] = edge; px[30 * 32 + i] = edge; }
            if ((mask & 4) == 0) { px[0 * 32 + i] = edge; px[1 * 32 + i] = edge; }
            if ((mask & 8) == 0) { px[i * 32 + 0] = edge; px[i * 32 + 1] = edge; }
            if ((mask & 2) == 0) { px[i * 32 + 31] = edge; px[i * 32 + 30] = edge; }
        }
        s = ToSprite(FromPixels(px, 32), key);
        spriteCache[key] = s;
        return s;
    }

    /// <summary>Kartla hedeflenebilir kare: hafif kırmızı dolgu + kalın kırmızı köşeler.</summary>
    public static Sprite GetTargetTile()
    {
        const string key = "target_tile";
        if (spriteCache.TryGetValue(key, out Sprite s) && s != null) return s;
        Color32[] px = new Color32[32 * 32];
        for (int i = 0; i < px.Length; i++) px[i] = TargetFill;
        for (int i = 0; i < 9; i++)
            for (int t = 0; t < 2; t++)
            {
                int a = i, b = t, c = 31 - i, d = 31 - t;
                px[b * 32 + a] = TargetEdge; px[a * 32 + b] = TargetEdge;
                px[b * 32 + c] = TargetEdge; px[c * 32 + b] = TargetEdge;
                px[d * 32 + a] = TargetEdge; px[a * 32 + d] = TargetEdge;
                px[d * 32 + c] = TargetEdge; px[c * 32 + d] = TargetEdge;
            }
        s = ToSprite(FromPixels(px, 32), key);
        spriteCache[key] = s;
        return s;
    }

    // ---------------- yardımcılar ----------------
    private static Texture2D Build(string[] rows, int scale)
    {
        int h = rows.Length, w = rows[0].Length;
        int W = w * scale, H = h * scale;
        Color32[] px = new Color32[W * H];
        for (int y = 0; y < h; y++)
        {
            string r = rows[y];
            for (int x = 0; x < w; x++)
            {
                Color32 c = Pal.TryGetValue(r[x], out Color32 col) ? col : new Color32(0, 0, 0, 0);
                for (int sy = 0; sy < scale; sy++)
                {
                    int ty = (h - 1 - y) * scale + sy; // satır 0 = üst
                    for (int sx = 0; sx < scale; sx++)
                        px[ty * W + x * scale + sx] = c;
                }
            }
        }
        return FromPixels(px, W, H);
    }

    private static Texture2D FromPixels(Color32[] px, int w, int h = -1)
    {
        if (h < 0) h = w;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels32(px);
        t.Apply(false, false);
        return t;
    }

    private static Sprite ToSprite(Texture2D t, string name)
    {
        t.name = name;
        Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        s.name = name;
        return s;
    }
}
