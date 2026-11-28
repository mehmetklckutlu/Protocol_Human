using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Karo vurguları: savaşta hareket menzili (camgöbeği alan, sadece dış kenarı çizgili)
/// ve kartla hedeflenebilir kareler (kırmızı, nabız gibi atan). Renderer'lar havuzlanır,
/// her çağrıda obje yaratılıp yok edilmez.
/// </summary>
public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    [Tooltip("Eski sistemden kalan alan; artık kullanılmıyor.")]
    [SerializeField] private GameObject highlightPrefab;

    [SerializeField] private int sortingOrder = 2;
    [SerializeField, Range(0f, 1f)] private float targetPulseMin = 0.55f;
    [SerializeField, Min(0f)] private float targetPulseSpeed = 4f;

    private enum Mode { None, Range, Targets }
    private Mode mode = Mode.None;

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    private int used;
    // Havuzun başındaki, nabız efekti almayan (menzil alanı) karo sayısı.
    private int staticCount;
    private readonly HashSet<Vector2Int> rangeCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> targetCells = new HashSet<Vector2Int>();

    /// <summary>Vurgular her değiştiğinde artar; GridCursor yeniden değerlendirmek için kullanır.</summary>
    public int Version { get; private set; }

    public bool IsInRange(Vector2Int cell) => rangeCells.Contains(cell);
    public bool IsTarget(Vector2Int cell) => targetCells.Contains(cell);
    public bool ShowingTargets => mode == Mode.Targets;

    private Transform root;

    private void Awake()
    {
        Instance = this;
        // Ayrı kök: HighlightManager objesinin ölçeği/konumu karoları etkilemesin.
        root = new GameObject("Highlights").transform;
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    /// <summary>Hareket menzilini gösterir.</summary>
    public void ShowRange(List<Node> nodes)
    {
        ClearHighlights();
        if (nodes == null) return;
        foreach (Node n in nodes) if (n != null) rangeCells.Add(n.gridPosition);

        foreach (Vector2Int c in rangeCells)
        {
            int mask = (rangeCells.Contains(c + Vector2Int.up) ? 1 : 0)
                     | (rangeCells.Contains(c + Vector2Int.right) ? 2 : 0)
                     | (rangeCells.Contains(c + Vector2Int.down) ? 4 : 0)
                     | (rangeCells.Contains(c + Vector2Int.left) ? 8 : 0);
            Place(c, CursorArt.GetRangeTile(mask), Color.white);
        }
        mode = Mode.Range;
    }

    /// <summary>Kartla vurulabilecek hedef karelerini gösterir.</summary>
    public void ShowTargets(List<Node> nodes) => ShowTargets(nodes, null);

    /// <summary>
    /// Kartla vurulabilecek hedefleri ve (verildiyse) kartın saldırı menzilini gösterir.
    /// Menzil turuncu alan + dış çizgi olarak çizilir: "en fazla buraya kadar vurabilirsin".
    /// </summary>
    public void ShowTargets(List<Node> nodes, ICollection<Vector2Int> attackRange)
    {
        ClearHighlights();

        if (attackRange != null)
        {
            foreach (Vector2Int c in attackRange)
            {
                int mask = (attackRange.Contains(c + Vector2Int.up) ? 1 : 0)
                         | (attackRange.Contains(c + Vector2Int.right) ? 2 : 0)
                         | (attackRange.Contains(c + Vector2Int.down) ? 4 : 0)
                         | (attackRange.Contains(c + Vector2Int.left) ? 8 : 0);
                Place(c, CursorArt.GetAttackRangeTile(mask), Color.white, sortingOrder);
            }
        }
        staticCount = used;

        Sprite s = CursorArt.GetTargetTile();
        if (nodes != null)
        {
            foreach (Node n in nodes)
            {
                if (n == null || !targetCells.Add(n.gridPosition)) continue;
                Place(n.gridPosition, s, Color.white, sortingOrder + 1);
            }
        }
        mode = Mode.Targets;
        Version++;
    }

    public void ClearHighlights()
    {
        for (int i = 0; i < used; i++) pool[i].enabled = false;
        used = 0;
        staticCount = 0;
        rangeCells.Clear();
        targetCells.Clear();
        mode = Mode.None;
        Version++;
    }

    private void Place(Vector2Int cell, Sprite sprite, Color color) => Place(cell, sprite, color, sortingOrder);

    private void Place(Vector2Int cell, Sprite sprite, Color color, int order)
    {
        SpriteRenderer sr;
        if (used < pool.Count) sr = pool[used];
        else
        {
            GameObject go = new GameObject("Highlight");
            go.transform.SetParent(root, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            pool.Add(sr);
        }
        used++;

        Vector3 p = GridManager.Instance != null ? GridManager.Instance.GetWorldPosition(cell) : new Vector3(cell.x, cell.y, 0f);
        p.z = -0.5f;
        sr.transform.position = p;
        float size = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        sr.transform.localScale = new Vector3(size, size, 1f);
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.enabled = true;
    }

    private void Update()
    {
        if (mode != Mode.Targets || used <= staticCount) return;
        float a = Mathf.Lerp(targetPulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * targetPulseSpeed));
        Color c = new Color(1f, 1f, 1f, a);
        // Sadece hedef kareleri nabız gibi atar; menzil alanı sabit kalır.
        for (int i = staticCount; i < used; i++) pool[i].color = c;
    }
}
