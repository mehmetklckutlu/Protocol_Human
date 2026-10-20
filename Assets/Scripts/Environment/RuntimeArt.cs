using UnityEngine;

// Geçici "kutu" görselleri için kod ile üretilen sprite'lar.
// Gerçek sprite'lar gelince objelerdeki SpriteRenderer'a atanır; bu dosya gerekmez.
public static class RuntimeArt
{
    private static Sprite square;
    private static Sprite star;

    // 1x1 birim boyunda beyaz kare (renk SpriteRenderer.color ile verilir)
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

    // 1x1 birim boyunda beş köşeli yıldız (beyaz, arkası şeffaf)
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
