using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Oyun içi Ayarlar menüsü. Her sahnede kendiliğinden oluşur (sahneye bir şey eklemen gerekmez).
// Esc tuşu ya da sol alttaki AYARLAR düğmesi ile açılır; açıkken oyun durur.
public class SettingsMenu : MonoBehaviour
{
    private const string MainMenuScene = "AnaMenü";

    private static SettingsMenu instance;
    public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.11f, 0.97f);
    private static readonly Color Accent = new Color(0.35f, 0.95f, 0.75f, 1f);
    private static readonly Color TextColor = new Color(0.85f, 0.95f, 0.92f, 1f);
    private static readonly Color DimText = new Color(0.55f, 0.65f, 0.62f, 1f);
    private static readonly Color ButtonColor = new Color(0.14f, 0.18f, 0.2f, 1f);

    private Canvas canvas;
    private GameObject panelRoot;
    private GameObject gearButton;
    private GameObject mainMenuButton;
    private RectTransform quitButton;
    private float menuButtonY;
    private float timeScaleBeforeOpen = 1f;
    private TMP_Text nowPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("[SettingsMenu]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SettingsMenu>();
        instance.Build();
        SceneManager.sceneLoaded += (s, m) => instance?.OnSceneLoaded(s);
        instance.OnSceneLoaded(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene)
    {
        if (IsOpen) Close();
        bool inMenu = scene.name == MainMenuScene;
        if (mainMenuButton != null) mainMenuButton.SetActive(!inMenu);
        // Ana menüdeyken ANA MENÜ düğmesi gizli; ÇIK düğmesi onun yerine kayar.
        if (quitButton != null) quitButton.anchoredPosition = new Vector2(0, inMenu ? menuButtonY : menuButtonY - 72);
        EnsureEventSystem();
    }

    private void Update()
    {
        if (nowPlaying != null && IsOpen) nowPlaying.text = "Çalan: " + Music.CurrentDisplayName;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsOpen) Close();
            else Open();
        }
    }

    public static void Toggle()
    {
        if (instance == null) return;
        if (IsOpen) instance.Close(); else instance.Open();
    }

    private void Open()
    {
        if (IsOpen) return;
        EnsureEventSystem();
        timeScaleBeforeOpen = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        if (nowPlaying != null) nowPlaying.text = "Çalan: " + Music.CurrentDisplayName;
        panelRoot.SetActive(true);
        gearButton.SetActive(false);
        Sfx.Play("dialog_open", 0.6f);
    }

    private void Close()
    {
        if (!IsOpen) return;
        panelRoot.SetActive(false);
        gearButton.SetActive(true);
        Time.timeScale = timeScaleBeforeOpen;
        PlayerPrefs.Save();
        Sfx.Play("ui_click", 0.7f);
    }

    // ------------------------------------------------------------------ arayüz

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Sol alt köşedeki küçük AYARLAR düğmesi
        gearButton = MakeButton(transform, "AYARLAR", new Vector2(150, 46), Toggle).gameObject;
        RectTransform g = (RectTransform)gearButton.transform;
        g.anchorMin = g.anchorMax = g.pivot = new Vector2(0, 0);
        g.anchoredPosition = new Vector2(18, 18);
        gearButton.GetComponentInChildren<TMP_Text>().fontSize = 20;

        // Tam ekran karartma (arkadaki oyuna tıklanmasın)
        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(transform, false);
        RectTransform pr = (RectTransform)panelRoot.transform;
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
        panelRoot.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        // Ortadaki pencere
        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Outline));
        box.transform.SetParent(panelRoot.transform, false);
        RectTransform br = (RectTransform)box.transform;
        br.sizeDelta = new Vector2(720, 860);
        box.GetComponent<Image>().color = PanelColor;
        Outline o = box.GetComponent<Outline>();
        o.effectColor = Accent; o.effectDistance = new Vector2(3, -3);

        float y = 380;
        MakeText(box.transform, "AYARLAR", 46, Accent, new Vector2(0, y), new Vector2(660, 60), TextAlignmentOptions.Center);
        y -= 55;
        MakeText(box.transform, "SES", 24, DimText, new Vector2(0, y), new Vector2(620, 34), TextAlignmentOptions.Left);
        y -= 60;

        MakeSlider(box.transform, "Ana Ses", y, () => Sfx.MasterVolume, v => Sfx.MasterVolume = v, null); y -= 75;
        MakeSlider(box.transform, "Efektler", y, () => Sfx.Volume, v => Sfx.Volume = v, () => Sfx.Play("attack_ranged", 0.7f, 0f, 0.25f)); y -= 75;
        MakeSlider(box.transform, "Robot Konuşma", y, () => Sfx.VoiceVolume, v => Sfx.VoiceVolume = v, () => Sfx.Voice()); y -= 75;
        MakeSlider(box.transform, "Drone Motoru", y, () => Sfx.EngineVolume, v => Sfx.EngineVolume = v, null); y -= 60;

        // Müzik: tüm müzikler (ana menü, tutorial, level) bu ayarı kullanır
        MakeText(box.transform, "MÜZİK", 24, DimText, new Vector2(0, y), new Vector2(620, 34), TextAlignmentOptions.Left);
        y -= 60;
        MakeSlider(box.transform, "Müzik", y, () => Music.Volume, v => Music.Volume = v, null); y -= 45;
        nowPlaying = MakeText(box.transform, "", 20, DimText, new Vector2(0, y), new Vector2(620, 30), TextAlignmentOptions.Left);
        y -= 70;

        Button resume = MakeButton(box.transform, "DEVAM ET", new Vector2(300, 60), Close);
        ((RectTransform)resume.transform).anchoredPosition = new Vector2(0, y); y -= 72;

        Button menu = MakeButton(box.transform, "ANA MENÜ", new Vector2(300, 60), GoToMainMenu);
        ((RectTransform)menu.transform).anchoredPosition = new Vector2(0, y);
        menuButtonY = y; y -= 72;
        mainMenuButton = menu.gameObject;

        Button quit = MakeButton(box.transform, "OYUNDAN ÇIK", new Vector2(300, 60), QuitGame);
        quitButton = (RectTransform)quit.transform;
        quitButton.anchoredPosition = new Vector2(0, y);

        MakeText(box.transform, "Esc: kapat", 18, DimText, new Vector2(0, -405), new Vector2(660, 28), TextAlignmentOptions.Center);

        panelRoot.SetActive(false);
    }

    private void GoToMainMenu()
    {
        Close();
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(MainMenuScene)) SceneManager.LoadScene(MainMenuScene);
        else SceneManager.LoadScene(0);
    }

    private void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void MakeSlider(Transform parent, string label, float y, System.Func<float> get, System.Action<float> set, System.Action preview)
    {
        MakeText(parent, label, 26, TextColor, new Vector2(-160, y), new Vector2(280, 40), TextAlignmentOptions.Left);
        TMP_Text pct = MakeText(parent, "", 24, Accent, new Vector2(285, y), new Vector2(80, 40), TextAlignmentOptions.Right);

        GameObject sGo = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
        sGo.transform.SetParent(parent, false);
        RectTransform sr = (RectTransform)sGo.transform;
        sr.sizeDelta = new Vector2(250, 22);
        sr.anchoredPosition = new Vector2(115, y);

        Image bg = NewImage(sGo.transform, "Background", new Color(0.2f, 0.24f, 0.26f, 1f));
        Stretch(bg.rectTransform, 0, 4);

        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(sGo.transform, false);
        Stretch(fillArea, 0, 4);
        Image fill = NewImage(fillArea, "Fill", Accent);
        Stretch(fill.rectTransform, 0, 0);

        RectTransform handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(sGo.transform, false);
        Stretch(handleArea, 10, 0);
        Image handle = NewImage(handleArea, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(20, 0);
        handle.rectTransform.anchorMin = new Vector2(0, 0);
        handle.rectTransform.anchorMax = new Vector2(0, 1);

        Slider s = sGo.GetComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;
        s.SetValueWithoutNotify(get());
        pct.text = Mathf.RoundToInt(get() * 100f) + "%";

        float lastPreview = -1f;
        s.onValueChanged.AddListener(v =>
        {
            set(v);
            pct.text = Mathf.RoundToInt(v * 100f) + "%";
            if (preview != null && Time.unscaledTime - lastPreview > 0.2f)
            {
                lastPreview = Time.unscaledTime;
                preview();
            }
        });
    }

    private static Image NewImage(Transform parent, string name, Color c)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    private static void Stretch(RectTransform r, float padX, float padY)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(padX, padY); r.offsetMax = new Vector2(-padX, -padY);
    }

    private static TMP_Text MakeText(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.sizeDelta = box;
        r.anchoredPosition = pos;
        return t;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.color = ButtonColor;
        Outline o = go.GetComponent<Outline>();
        o.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
        o.effectDistance = new Vector2(2, -2);
        Button b = go.GetComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);

        TMP_Text t = MakeText(go.transform, label, 26, TextColor, Vector2.zero, size, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 0, 0);

        EventTrigger et = go.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => Sfx.Play("ui_hover", 0.3f));
        et.triggers.Add(enter);
        return b;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
