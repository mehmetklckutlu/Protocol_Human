using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Fare altındaki kareyi bağlama göre işaretler ve işletim sistemi imlecini değiştirir.
/// Durum sadece kare / tur / hedefleme / vurgu değişince (ve emniyet için 4 kez/sn) yeniden hesaplanır.
///
/// Kare imleci:  move (camgöbeği) · idle (gri) · target (kırmızı) · interact (amber) · ally (yeşil) · blocked (kırmızı X)
/// Fare imleci:  default · interact · scan · blocked · target · target_invalid · wait (dönen halka)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GridCursor : MonoBehaviour
{
    [Tooltip("Kare imlecinin çizim sırası (birimler 10, fog 100).")]
    [SerializeField] private int sortingOrder = 12;
    [Tooltip("Fare imleci büyütmesi. 0 = ekran yüksekliğine göre otomatik (1080p→2x, 1440p→3x, 4K→4x).")]
    [SerializeField, Range(0, 6)] private int osCursorScale = 0;
    [SerializeField, Min(1f)] private float waitFps = 12f;
    [SerializeField, Range(0f, 0.2f)] private float pulseAmount = 0.05f;
    [SerializeField, Min(0f)] private float pulseSpeed = 5f;

    private SpriteRenderer sr;
    private Camera cam;

    // Değerlendirme önbelleği
    private Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    private int lastKey;
    private float nextForcedEval;

    private string gridStyle;      // null = gizli
    private string osStyle = "default";
    private bool pulse;

    // Fare imleci durumu
    private string appliedOs;
    private int appliedScale;
    private int appliedWaitFrame = -1;

    // ---------------- Menü dahil her sahnede varsayılan imleç ----------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallDefaultCursor()
    {
        ApplyDefault();
        SceneManager.sceneLoaded += (s, m) => ApplyDefault();
    }

    private static void ApplyDefault()
    {
        Texture2D t = CursorArt.GetOsTexture("default", CursorArt.AutoScale(), out Vector2 hot);
        Cursor.SetCursor(t, hot, CursorMode.Auto);
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;
    }

    private void OnDisable()
    {
        if (sr != null) sr.enabled = false;
        appliedOs = null;
        ApplyDefault();
    }

    private void Update()
    {
        GridManager gm = GridManager.Instance;
        if (gm == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 mp = Input.mousePosition;
        mp.z = Mathf.Abs(cam.transform.position.z);
        Vector2Int cell = gm.GetGridPosition(cam.ScreenToWorldPoint(mp));

        int key = BuildKey();
        if (cell != lastCell || key != lastKey || Time.unscaledTime >= nextForcedEval)
        {
            lastCell = cell;
            lastKey = key;
            nextForcedEval = Time.unscaledTime + 0.25f;
            Evaluate(gm, cell);
        }

        // Kare imleci
        if (gridStyle == null)
        {
            sr.enabled = false;
        }
        else
        {
            sr.sprite = CursorArt.GetGridSprite(gridStyle);
            sr.enabled = true;
            Vector3 p = gm.GetWorldPosition(cell);
            p.z = -1f;
            transform.position = p;
            float s = gm.CellSize * (pulse ? 1f + pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed)) : 1f);
            transform.localScale = new Vector3(s, s, 1f);
        }

        ApplyOsCursor();
    }

    /// <summary>Kare dışındaki durum değişikliklerini yakalayan kısa özet.</summary>
    private int BuildKey()
    {
        int k = 17;
        TurnManager tm = TurnManager.Instance;
        if (tm != null)
        {
            k = k * 31 + (int)tm.CurrentState;
            k = k * 31 + (tm.IsInCombat() ? 1 : 0);
            k = k * 31 + (tm.IsGameOver ? 1 : 0);
        }
        CardManager cm = CardManager.Instance;
        if (cm != null) k = k * 31 + (cm.IsTargeting ? 1 : 0);
        if (HighlightManager.Instance != null) k = k * 31 + HighlightManager.Instance.Version;
        k = k * 31 + (PointerOverUI() ? 1 : 0);
        k = k * 31 + (SettingsMenu.IsOpen ? 1 : 0);
        k = k * 31 + (DialogState.Active ? 1 : 0);
        return k;
    }

    private static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void Set(string grid, string os, bool doPulse = false)
    {
        gridStyle = grid;
        osStyle = os;
        pulse = doPulse;
    }

    private void Evaluate(GridManager gm, Vector2Int cell)
    {
        TurnManager tm = TurnManager.Instance;
        CardManager cm = CardManager.Instance;
        HighlightManager hm = HighlightManager.Instance;

        // Menü, ayarlar, oyun sonu, UI üstü
        if (SettingsMenu.IsOpen || DialogState.Active || PointerOverUI() || (tm != null && tm.IsGameOver))
        {
            Set(null, "default");
            return;
        }

        // Düşman turu veya bir işlem sürüyor (hareket, arama, animasyon)
        if (tm != null && tm.CurrentState != TurnState.PlayerTurn)
        {
            Set(null, "wait");
            return;
        }

        if (!gm.IsInsideGrid(cell))
        {
            Set(null, "default");
            return;
        }

        // Fog: hiç görülmemiş kare işaretlenmez (labirent perdesinin girişi hariç)
        FogOfWar fog = FogOfWar.Instance;
        bool hiddenEntrance = false;
        if (fog != null && !fog.IsVisible(cell) && !fog.IsExplored(cell))
        {
            hiddenEntrance = fog.IsHiddenEntrance(cell);
            if (!hiddenEntrance)
            {
                Set(null, "default");
                return;
            }
        }

        bool inCombat = tm != null && tm.IsInCombat();

        // Kartla hedefleme
        if (cm != null && cm.IsTargeting)
        {
            if (hm != null && hm.IsTarget(cell)) Set("target", "target", true);
            else Set("idle", "target_invalid");
            return;
        }

        // Kareyi kim/ne dolduruyor?
        Unit unit = UnitAt(gm, cell);
        if (unit != null)
        {
            if (unit.isPlayerOwned) Set("ally", "interact");
            else Set("target", "default");   // düşman: bilgi amaçlı kırmızı köşe, tıklama hedefleme değil
            return;
        }

        ExplosiveBarrel barrel = BarrelAt(cell);
        if (barrel != null)
        {
            Set("interact", "default");
            return;
        }

        if (ScrapAt(cell))
        {
            if (!inCombat) Set("interact", "scan", true);
            else Set("idle", "blocked");      // savaşta hurda aranamaz
            return;
        }

        Node node = gm.GetNode(cell);
        if (!hiddenEntrance && (node == null || !node.isWalkable))
        {
            Set("blocked", "blocked");
            return;
        }

        if (inCombat)
        {
            // Menzil karoları gösteriliyorsa ona göre; gösterilmiyorsa (hareket hakkı bitti / başka birimin turu) gri.
            if (hm != null && hm.IsInRange(cell)) Set("move", "default", true);
            else Set("idle", "default");
            return;
        }

        Set("move", "default");
    }

    private static Unit UnitAt(GridManager gm, Vector2Int cell)
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null) return null;
        foreach (Unit u in tm.playerUnits)
            if (u != null && !u.IsDead && gm.GetGridPosition(u.transform.position) == cell) return u;
        foreach (Unit u in tm.enemyUnits)
            if (u != null && !u.IsDead && gm.GetGridPosition(u.transform.position) == cell)
            {
                // Görünmeyen düşmanı imleçle ele verme
                if (FogOfWar.Instance != null && !FogOfWar.Instance.IsVisible(cell)) return null;
                return u;
            }
        return null;
    }

    private static ExplosiveBarrel BarrelAt(Vector2Int cell)
    {
        foreach (ExplosiveBarrel b in ExplosiveBarrel.All)
            if (b != null && b.Cell == cell) return b;
        return null;
    }

    private static ScrapPile[] scrapCache;
    private static float scrapCacheTime = -10f;

    private static bool ScrapAt(Vector2Int cell)
    {
        // Sadece yeniden değerlendirmede ve en fazla saniyede bir aranır.
        if (scrapCache == null || Time.unscaledTime - scrapCacheTime > 1f)
        {
            scrapCache = Object.FindObjectsByType<ScrapPile>(FindObjectsSortMode.None);
            scrapCacheTime = Time.unscaledTime;
        }
        foreach (ScrapPile p in scrapCache)
            if (p != null && p.isActiveAndEnabled && p.PileCell == cell) return true;
        return false;
    }

    private void ApplyOsCursor()
    {
        int scale = osCursorScale > 0 ? osCursorScale : CursorArt.AutoScale();
        string name = osStyle;
        int waitFrame = -1;
        if (name == "wait")
        {
            waitFrame = Mathf.FloorToInt(Time.unscaledTime * waitFps) % CursorArt.WaitFrames;
            name = "wait_" + waitFrame;
        }

        if (name == appliedOs && scale == appliedScale && waitFrame == appliedWaitFrame) return;
        appliedOs = name;
        appliedScale = scale;
        appliedWaitFrame = waitFrame;

        Texture2D t = CursorArt.GetOsTexture(name, scale, out Vector2 hot);
        Cursor.SetCursor(t, hot, CursorMode.Auto);
    }
}
