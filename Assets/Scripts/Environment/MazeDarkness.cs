using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Labirent karanlığı: ana drone labirent bölgesine girince bütün ışıklar söner, drone'un
// kendi sarı ışığı açılır (duvarlarda kesilir). Harekete geçen (uyanan) düşman drone'ları da
// etraflarına ışık yayar. Drone bölgeden çıkınca sis sistemi eski haline döner.
// MainLevel sahnesinde kendiliğinden oluşur; başka sahnede kullanmak için bir objeye ekle.
public class MazeDarkness : MonoBehaviour
{
    [Header("Bölge")]
    [Tooltip("Karanlık bölge, GridManager karelerinde. x,y = sol alt köşe; w,h = genişlik, yükseklik.")]
    public RectInt zone = new RectInt(120, 6, 25, 41);

    [Header("Işık")]
    [Tooltip("Oyuncu drone'unun ışığının kaç kare uzağa ulaştığı.")]
    [Min(0)] public int playerLightRadius = 6;
    [Tooltip("Uyanmış düşman drone'larının ışığının kaç kare uzağa ulaştığı.")]
    [Min(0)] public int enemyLightRadius = 10;
    [Tooltip("Işıklar söndükten kaç saniye sonra drone'un ışığı açılsın.")]
    [Min(0f)] public float lightOnDelay = 0.9f;

    [Header("Giriş konuşması (sadece ilk girişte)")]
    public string speaker = "PH-01 · SİSTEM";
    [TextArea(2, 4)]
    public string[] lines =
    {
        "Ortam ışığı algılanmadı. Bölge tamamen karanlık.\n<color=#FFD27A>Harici aydınlatma devreye alındı.</color>",
        "Sensörler dar koridorlar ve çıkmaz yollar algılıyor...\nSanırım burası bir <color=#FFD27A>labirent</color>."
    };
    [SerializeField, Min(0f)] private float charDelay = 0.03f;
    [Tooltip("Satır yazıldıktan sonra ekranda kalma süresi (saniye).")]
    [SerializeField, Min(0f)] private float lineHold = 2.8f;
    [SerializeField, Range(0.5f, 2f)] private float voicePitch = 1f;

    private static readonly Color Amber = new Color(1f, 0.8f, 0.35f);

    private bool inside = false;
    private bool announced = false;
    private Coroutine lightRoutine;
    private Coroutine talkRoutine;

    private SubtitleBox subtitle;

    // ------------------------------------------------------------------ otomatik kurulum

    private const string AutoScene = "MainLevel";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) { TryCreate(s); }

    private static void TryCreate(Scene s)
    {
        if (s.name != AutoScene) return;
        if (FindFirstObjectByType<MazeDarkness>() != null) return;
        new GameObject("MazeDarkness").AddComponent<MazeDarkness>();
    }

    // ------------------------------------------------------------------ akış

    private bool hiddenSet = false;

    private void Awake()
    {
        // Sis ilk kez hesaplanmadan önce perdeyi kur (oyuncu labirentin yanında başlasa bile içi görünmesin).
        if (FogOfWar.Instance != null)
        {
            FogOfWar.Instance.SetHiddenZone(zone);
            hiddenSet = true;
        }
    }

    private void Update()
    {
        // Labirentin içi dışarıdan hiç görünmesin: görüş giriş kapısında kesilir.
        if (!hiddenSet && FogOfWar.Instance != null)
        {
            FogOfWar.Instance.SetHiddenZone(zone);
            hiddenSet = true;
        }

        Unit main = MainUnit();
        if (main != null && !main.IsDead && GridManager.Instance != null && FogOfWar.Instance != null)
        {
            bool now = zone.Contains(GridManager.Instance.GetGridPosition(main.transform.position));
            if (now != inside)
            {
                inside = now;
                if (inside) Enter(); else Exit();
            }
        }

#if UNITY_EDITOR
        // F6: test için ana drone'u labirent girişine ışınla.
        if (Input.GetKeyDown(KeyCode.F6)) TeleportToEntrance();
#endif
    }

    private void Enter()
    {
        // Işıklar söner: önce drone'un ışığı da kapalı, tam karanlık.
        FogOfWar.Instance.SetDarkMode(true, zone, 0, enemyLightRadius);
        Sfx.Play("drone_shutdown", 0.45f, 0f, 0.3f, 0.85f);

        if (lightRoutine != null) StopCoroutine(lightRoutine);
        lightRoutine = StartCoroutine(LightOn());

        if (!announced)
        {
            announced = true;
            talkRoutine = StartCoroutine(Announce());
        }
    }

    private void Exit()
    {
        if (lightRoutine != null) { StopCoroutine(lightRoutine); lightRoutine = null; }
        FogOfWar.Instance.SetDarkMode(false, zone, playerLightRadius, enemyLightRadius);
    }

    // Drone'un ışığı birkaç kez titreyip yanar.
    private IEnumerator LightOn()
    {
        yield return new WaitForSeconds(lightOnDelay);
        FogOfWar fog = FogOfWar.Instance;
        if (fog == null || !fog.DarkMode) yield break;

        int r = playerLightRadius;
        Sfx.Play("cpu_restore", 0.4f, 0f, 0.3f, 1.1f);
        fog.SetDarkPlayerRadius(Mathf.Max(1, r / 2)); yield return new WaitForSeconds(0.09f);
        fog.SetDarkPlayerRadius(0);                   yield return new WaitForSeconds(0.14f);
        fog.SetDarkPlayerRadius(Mathf.Max(1, r - 2)); yield return new WaitForSeconds(0.07f);
        fog.SetDarkPlayerRadius(1);                   yield return new WaitForSeconds(0.22f);
        fog.SetDarkPlayerRadius(r);
        lightRoutine = null;
    }

    // Konuşma ekranın altında alt yazı olarak akar; satırlar kendiliğinden geçer, ekrana tıklayınca hızlanır.
    private IEnumerator Announce()
    {
        BuildUI();
        yield return new WaitForSeconds(lightOnDelay + 0.7f);
        yield return subtitle.Play(speaker, Amber, lines, false, lineHold, charDelay, voicePitch);
        talkRoutine = null;
    }

    private static Unit MainUnit()
    {
        if (TurnManager.Instance == null) return null;
        foreach (Unit u in TurnManager.Instance.playerUnits)
            if (u != null && u.isMainDrone) return u;
        foreach (Unit u in TurnManager.Instance.playerUnits)
            if (u != null) return u;
        return null;
    }

    private void OnDisable()
    {
        if (inside && FogOfWar.Instance != null)
            FogOfWar.Instance.SetDarkMode(false, zone, playerLightRadius, enemyLightRadius);
        inside = false;
    }

    // ------------------------------------------------------------------ arayüz

    private void BuildUI()
    {
        if (subtitle != null) return;

        GameObject cgo = new GameObject("MazeDarknessCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 440;
        CanvasScaler scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Ekranın altında alt yazı kutusu (HUD temasında, tıklamayı engellemez).
        subtitle = SubtitleBox.Create(cgo.transform);
    }

    // ------------------------------------------------------------------ editör

    [Tooltip("Editörde F6 ile ışınlanılacak kare (labirentin hemen dışındaki giriş koridoru).")]
    public Vector2Int debugEntranceCell = new Vector2Int(116, 33);

#if UNITY_EDITOR
    private void TeleportToEntrance()
    {
        Unit main = MainUnit();
        if (main == null || GridManager.Instance == null) return;
        if (TurnManager.Instance != null && TurnManager.Instance.IsInCombat()) return;

        Vector3 p = GridManager.Instance.GetWorldPosition(debugEntranceCell);
        p.z = p.y * 0.01f;
        main.transform.position = p;
        UnitMovement mv = main.GetComponent<UnitMovement>();
        if (mv != null)
        {
            var prop = typeof(UnitMovement).GetProperty("CurrentGridPosition");
            if (prop != null) prop.GetSetMethod(true)?.Invoke(mv, new object[] { debugEntranceCell });
        }
        if (FogOfWar.Instance != null) FogOfWar.Instance.Refresh();
        Debug.Log("F6: ana drone labirent girişine ışınlandı " + debugEntranceCell);
    }

    private void OnDrawGizmos()
    {
        GridManager g = GridManager.Instance != null ? GridManager.Instance : FindFirstObjectByType<GridManager>();
        if (g == null) return;
        float cs = g.CellSize;
        Vector3 c = g.GridOrigin + new Vector3((zone.x + zone.width * 0.5f) * cs, (zone.y + zone.height * 0.5f) * cs, 0f);
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(c, new Vector3(zone.width * cs, zone.height * cs, 0f));
    }
#endif
}
