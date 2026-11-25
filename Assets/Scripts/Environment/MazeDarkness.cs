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

    private GameObject boxRoot;
    private CanvasGroup boxGroup;
    private TextMeshProUGUI bodyText;

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

    private IEnumerator Announce()
    {
        BuildUI();
        yield return new WaitForSeconds(lightOnDelay + 0.7f);

        boxRoot.SetActive(true);
        Sfx.Play("dialog_open", 0.5f);
        yield return Fade(0f, 1f, 0.2f);

        foreach (string line in lines)
        {
            bodyText.text = line;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            bodyText.maxVisibleCharacters = 0;

            float t = 0f;
            int shown = 0;
            while (shown < total)
            {
                t += Time.deltaTime;
                int prev = shown;
                shown = charDelay <= 0f ? total : Mathf.Min(total, Mathf.FloorToInt(t / charDelay));
                bodyText.maxVisibleCharacters = shown;
                if (shown > prev) PlayVoiceFor(bodyText.textInfo, prev, shown);
                yield return null;
            }
            bodyText.maxVisibleCharacters = 99999;
            yield return new WaitForSeconds(lineHold);
        }

        yield return Fade(1f, 0f, 0.4f);
        boxRoot.SetActive(false);
        talkRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            boxGroup.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        boxGroup.alpha = to;
    }

    private void PlayVoiceFor(TMP_TextInfo info, int from, int to)
    {
        for (int c = from; c < to && c < info.characterCount; c++)
        {
            if (char.IsLetterOrDigit(info.characterInfo[c].character))
            {
                Sfx.Voice(voicePitch);
                return;
            }
        }
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
        if (boxRoot != null) return;

        GameObject cgo = new GameObject("MazeDarknessCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 440;
        CanvasScaler scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Haritaya tıklamayı engellemeyen, ekranın üst-ortasında duran konuşma kutusu.
        boxRoot = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        boxRoot.transform.SetParent(cgo.transform, false);
        Image bg = boxRoot.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.05f, 0.02f, 0.92f);
        bg.raycastTarget = false;
        Outline o = boxRoot.AddComponent<Outline>();
        o.effectColor = new Color(Amber.r, Amber.g, Amber.b, 0.9f);
        o.effectDistance = new Vector2(3f, -3f);
        boxGroup = boxRoot.GetComponent<CanvasGroup>();
        boxGroup.blocksRaycasts = false;
        boxGroup.interactable = false;
        boxGroup.alpha = 0f;

        RectTransform br = boxRoot.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 1f);
        br.pivot = new Vector2(0.5f, 1f);
        br.anchoredPosition = new Vector2(0f, -150f);
        br.sizeDelta = new Vector2(980f, 150f);

        TextMeshProUGUI sp = MakeText(boxRoot.transform, "Speaker", speaker, 22, Amber, TextAlignmentOptions.Left);
        sp.fontStyle = FontStyles.Bold;
        Place(sp.rectTransform, new Vector2(0f, 48f), new Vector2(920f, 34f));

        bodyText = MakeText(boxRoot.transform, "Body", "", 26, Color.white, TextAlignmentOptions.TopLeft);
        Place(bodyText.rectTransform, new Vector2(0f, -18f), new Vector2(920f, 90f));

        boxRoot.SetActive(false);
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
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
