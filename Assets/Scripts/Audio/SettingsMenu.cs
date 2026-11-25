using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Oyun içi Ayarlar menüsü. Her sahnede kendiliğinden oluşur (sahneye bir şey eklemen gerekmez).
// Sadece Esc tuşu ile açılır/kapanır (ekranda düğme yok); açıkken oyun durur.
// Görünüm HUD ile aynı dilde: kesik köşeli panel, hücreli ses sürgüleri, susturma ikonları.
public class SettingsMenu : MonoBehaviour
{
    private const string MainMenuScene = "AnaMenü";

    private static SettingsMenu instance;
    public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

    // ---- renkler (HUD paleti) ----
    private static readonly Color Cyan = new Color32(89, 242, 255, 255);
    private static readonly Color CyanMid = new Color32(36, 150, 190, 255);
    private static readonly Color Amber = new Color32(255, 184, 51, 255);
    private static readonly Color TextColor = new Color32(214, 236, 242, 255);
    private static readonly Color DimText = new Color32(120, 160, 170, 255);
    private static readonly Color HeaderBg = new Color32(8, 24, 32, 255);
    private static readonly Color RowHover = new Color32(18, 52, 66, 255);
    private static readonly Color Divider = new Color32(30, 70, 86, 255);
    private static readonly Color DangerText = new Color32(255, 140, 130, 255);

    private const float PanelW = 672f;
    private const float Pad = 18f;
    private const int Cells = 10;

    private Canvas canvas;
    private GameObject panelRoot;
    private RectTransform box;
    private GameObject mainMenuButton;
    private RectTransform quitButton;
    private float timeScaleBeforeOpen = 1f;
    private TMP_Text nowPlaying;
    private readonly List<RectTransform> eqBars = new List<RectTransform>();

    // Susturmadan önceki değerler (tekrar açınca geri gelsin)
    private readonly Dictionary<string, float> beforeMute = new Dictionary<string, float>();

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
        // Ana menüde ANA MENÜ düğmesi gizli; ÇIK düğmesi tüm genişliği alır.
        if (quitButton != null)
        {
            float full = PanelW - 2 * Pad - 12f;
            float half = (full - 14f) * 0.5f;
            quitButton.sizeDelta = new Vector2(inMenu ? full : half, quitButton.sizeDelta.y);
            quitButton.anchoredPosition = new Vector2(Pad + 6f + (inMenu ? 0f : half + 14f), quitButton.anchoredPosition.y);
        }
        EnsureEventSystem();
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (nowPlaying != null) nowPlaying.text = Music.CurrentDisplayName;
            float t = Time.unscaledTime;
            for (int i = 0; i < eqBars.Count; i++)
            {
                float h = 6f + 22f * (0.5f + 0.5f * Mathf.Sin(t * (5f + i * 1.3f) + i * 1.7f));
                if (Music.Volume <= 0.001f) h = 4f;
                eqBars[i].sizeDelta = new Vector2(8f, h);
            }
        }
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
        if (nowPlaying != null) nowPlaying.text = Music.CurrentDisplayName;
        panelRoot.SetActive(true);
        Sfx.Play("dialog_open", 0.6f);
    }

    private void Close()
    {
        if (!IsOpen) return;
        panelRoot.SetActive(false);
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

        // Tam ekran karartma (arkadaki oyuna tıklanmasın)
        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(transform, false);
        RectTransform pr = (RectTransform)panelRoot.transform;
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
        panelRoot.GetComponent<Image>().color = new Color(0f, 0.01f, 0.02f, 0.8f);

        // Pencere (yükseklik içerik dizildikten sonra ayarlanır)
        box = NewRect(panelRoot.transform, "Box");
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.sprite = HudArt.Panel(HudArt.CyanMid);
        boxImg.type = Image.Type.Sliced;
        Img(box, "Accent", HudArt.Solid, Cyan, 24f, -3f, 90f, 3f);

        float y = -Pad;

        // ---- başlık ----
        float hw = PanelW - 2 * Pad;
        Img(box, "HeaderBg", HudArt.Solid, HeaderBg, Pad, y, hw, 66f);
        Img(box, "HeaderLine", HudArt.Solid, CyanMid, Pad, y - 66f, hw, 3f);
        Image chip = Img(box, "Chip", RuntimeArt.CpuIcon, Color.white, Pad + 14f, y - 12f, 42f, 42f);
        chip.preserveAspect = true;
        Text(box, "SİSTEM AYARLARI", 34, Cyan, TextAlignmentOptions.MidlineLeft, Pad + 70f, y, 420f, 66f);

        // ESC tuş kapağı (tıklayınca da kapanır)
        RectTransform esc = NewRect(box, "Esc");
        Place(esc, Pad + hw - 86f, y - 16f, 70f, 36f);
        Image escImg = esc.gameObject.AddComponent<Image>();
        escImg.sprite = HudArt.Solid; escImg.color = new Color32(40, 60, 70, 255);
        Img(esc, "Shadow", HudArt.Solid, new Color32(20, 32, 40, 255), 0f, -30f, 70f, 6f).raycastTarget = false;
        Text(esc, "ESC", 20, TextColor, TextAlignmentOptions.Center, 0f, 0f, 70f, 30f);
        Button escBtn = esc.gameObject.AddComponent<Button>();
        escBtn.targetGraphic = escImg;
        escBtn.onClick.AddListener(Close);
        HoverSound(esc.gameObject);
        y -= 66f + 30f;

        // ---- ses ----
        Section(ref y, "SES", HudArt.SpeakerIcon);
        Row(ref y, "Ana Ses", "master", () => Sfx.MasterVolume, v => Sfx.MasterVolume = v, null);
        Row(ref y, "Efektler", "sfx", () => Sfx.Volume, v => Sfx.Volume = v, () => Sfx.Play("attack_ranged", 0.7f, 0f, 0.25f));
        Row(ref y, "Robot Konuşma", "voice", () => Sfx.VoiceVolume, v => Sfx.VoiceVolume = v, () => Sfx.Voice());
        Row(ref y, "Drone Motoru", "engine", () => Sfx.EngineVolume, v => Sfx.EngineVolume = v, null);
        y -= 8f;

        // ---- müzik: tüm müzikler (ana menü, tutorial, level) bu ayarı kullanır ----
        Section(ref y, "MÜZİK", HudArt.NoteIcon);
        Row(ref y, "Müzik", "music", () => Music.Volume, v => Music.Volume = v, null);

        Img(box, "NowEdge", HudArt.Solid, HudArt.Outline, Pad + 12f, y, hw - 24f, 44f);
        Img(box, "NowBg", HudArt.Solid, new Color32(8, 22, 30, 255), Pad + 14f, y - 2f, hw - 28f, 40f);
        Image note = Img(box, "Note", HudArt.NoteIcon, Cyan, Pad + 26f, y - 10f, 21f, 24f);
        note.preserveAspect = true;
        Text(box, "ÇALAN", 18, DimText, TextAlignmentOptions.MidlineLeft, Pad + 62f, y, 80f, 44f);
        nowPlaying = Text(box, "", 22, Amber, TextAlignmentOptions.MidlineLeft, Pad + 140f, y, hw - 290f, 44f);
        nowPlaying.overflowMode = TextOverflowModes.Ellipsis;
        for (int i = 0; i < 7; i++)
        {
            Image bar = Img(box, "Eq" + i, HudArt.Solid, i % 2 == 0 ? Cyan : CyanMid, Pad + hw - 120f + i * 14f, 0f, 8f, 10f);
            RectTransform br = bar.rectTransform;
            br.pivot = new Vector2(0f, 0f);                    // alttan büyür
            br.anchoredPosition = new Vector2(Pad + hw - 120f + i * 14f, y - 34f);
            eqBars.Add(br);
        }
        y -= 44f + 30f;

        // ---- butonlar ----
        float full = hw - 12f;
        Button resume = MakeButton("DEVAM ET", HudArt.PlayIcon, Pad + 6f, y, full, 66f, 26,
            HudArt.Cyan, new Color32(14, 70, 88, 255), new Color32(225, 255, 255, 255), Close);
        y -= 66f + 16f;
        float half = (full - 14f) * 0.5f;
        Button menu = MakeButton("ANA MENÜ", HudArt.HomeIcon, Pad + 6f, y, half, 57f, 22,
            HudArt.CyanMid, new Color32(18, 46, 60, 255), (Color32)TextColor, GoToMainMenu);
        mainMenuButton = menu.gameObject;
        Button quit = MakeButton("OYUNDAN ÇIK", HudArt.ExitIcon, Pad + 6f + half + 14f, y, half, 57f, 22,
            new Color32(150, 40, 40, 255), new Color32(40, 14, 16, 255), (Color32)DangerText, QuitGame);
        quitButton = (RectTransform)quit.transform;
        y -= 57f + Pad + 6f;

        box.sizeDelta = new Vector2(PanelW, -y);
        panelRoot.SetActive(false);
    }

    // ------------------------------------------------------------------ parçalar

    private void Section(ref float y, string title, Sprite icon)
    {
        float hw = PanelW - 2 * Pad;
        Image ic = Img(box, title + "Icon", icon, DimText, Pad + 6f, y + 8f, 18f, 16f);
        ic.preserveAspect = true;
        TMP_Text t = Text(box, title, 20, DimText, TextAlignmentOptions.MidlineLeft, Pad + 30f, y + 12f, 200f, 24f);
        float tw = t.GetPreferredValues(title).x;
        Img(box, title + "Line", HudArt.Solid, Divider, Pad + 40f + tw, y - 1f, hw - 46f - tw, 2f);
        y -= 30f;
    }

    private void Row(ref float y, string label, string key, Func<float> get, Action<float> set, Action preview)
    {
        float hw = PanelW - 2 * Pad;
        RectTransform row = NewRect(box, label + " Row");
        Place(row, Pad, y + 6f, hw, 48f);
        Image rowBg = row.gameObject.AddComponent<Image>();
        rowBg.sprite = HudArt.Solid;
        rowBg.color = new Color(RowHover.r, RowHover.g, RowHover.b, 0f);
        Image hoverBar = Img(row, "HoverBar", HudArt.Solid, Amber, 0f, 0f, 5f, 48f);
        hoverBar.gameObject.SetActive(false);

        // susturma ikonu
        RectTransform spk = NewRect(row, "Mute");
        Place(spk, 16f, -10f, 30f, 27f);
        Image spkImg = spk.gameObject.AddComponent<Image>();
        spkImg.preserveAspect = true;
        Button spkBtn = spk.gameObject.AddComponent<Button>();
        spkBtn.targetGraphic = spkImg;
        ColorBlock cb = spkBtn.colors; cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f); spkBtn.colors = cb;

        TMP_Text lbl = Text(row, label, 24, TextColor, TextAlignmentOptions.MidlineLeft, 58f, 0f, 240f, 48f);
        TMP_Text pct = Text(row, "", 24, Cyan, TextAlignmentOptions.MidlineRight, hw - 112f, 0f, 100f, 48f);

        // hücreli sürgü: görünmez Slider + 10 hücre
        const float cw = 18f, ch = 24f, gap = 5f;
        float barX = 300f, barW = Cells * cw + (Cells - 1) * gap;
        Image[] cells = new Image[Cells];
        for (int i = 0; i < Cells; i++)
            cells[i] = Img(row, "Cell" + i, HudArt.SettingCell(false), Color.white, barX + i * (cw + gap), -12f, cw, ch);

        RectTransform sRt = NewRect(row, "Slider");
        Place(sRt, barX - 4f, -6f, barW + 8f, 36f);
        Image hit = sRt.gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0); // tıklama alanı
        RectTransform fillArea = NewRect(sRt, "Fill Area");
        fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one; fillArea.offsetMin = new Vector2(4f, 0f); fillArea.offsetMax = new Vector2(-4f, 0f);
        RectTransform fill = NewRect(fillArea, "Fill");
        Image fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0, 0, 0, 0);
        fillImg.raycastTarget = false;
        Slider s = sRt.gameObject.AddComponent<Slider>();
        s.fillRect = fill;
        s.targetGraphic = hit;
        s.transition = Selectable.Transition.None;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;

        void Refresh(float v)
        {
            int on = Mathf.RoundToInt(v * Cells);
            for (int i = 0; i < Cells; i++)
            {
                Sprite sp = HudArt.SettingCell(i < on);
                if (cells[i].sprite != sp) cells[i].sprite = sp;
            }
            bool muted = v <= 0.001f;
            pct.text = Mathf.RoundToInt(v * 100f) + "%";
            pct.color = muted ? DimText : Cyan;
            lbl.color = muted ? DimText : TextColor;
            spkImg.sprite = muted ? HudArt.SpeakerMuteIcon : HudArt.SpeakerIcon;
            spkImg.color = muted ? Color.white : Cyan;
        }

        float lastPreview = -1f;
        s.SetValueWithoutNotify(get());
        Refresh(get());
        s.onValueChanged.AddListener(v =>
        {
            set(v);
            Refresh(v);
            if (preview != null && Time.unscaledTime - lastPreview > 0.2f)
            {
                lastPreview = Time.unscaledTime;
                preview();
            }
        });

        spkBtn.onClick.AddListener(() =>
        {
            float v = get();
            if (v > 0.001f) { beforeMute[key] = v; s.value = 0f; }
            else s.value = beforeMute.TryGetValue(key, out float old) && old > 0.001f ? old : 0.5f;
            Sfx.Play("ui_click", 0.5f);
        });

        // satır vurgusu
        EventTrigger et = row.gameObject.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerEnter, () =>
        {
            rowBg.color = RowHover;
            hoverBar.gameObject.SetActive(true);
            Sfx.Play("ui_hover", 0.25f, 0.06f, 0.06f);
        });
        AddTrigger(et, EventTriggerType.PointerExit, () =>
        {
            rowBg.color = new Color(RowHover.r, RowHover.g, RowHover.b, 0f);
            hoverBar.gameObject.SetActive(false);
        });

        y -= 56f;
    }

    private Button MakeButton(string label, Sprite icon, float x, float y, float w, float h, float fontSize,
                              Color32 border, Color32 fill, Color32 textCol, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = NewRect(box, label);
        Place(rt, x, y, w, h);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = HudArt.Panel(border, fill);
        bg.type = Image.Type.Sliced;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.06f;
        b.colors = cb;
        b.onClick.AddListener(onClick);

        // ikon + yazı ortada, yan yana
        RectTransform content = NewRect(rt, "Content");
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = content.offsetMax = Vector2.zero;
        HorizontalLayoutGroup hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.spacing = 12f;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

        RectTransform ic = NewRect(content, "Icon");
        Image icImg = ic.gameObject.AddComponent<Image>();
        icImg.sprite = icon; icImg.color = textCol; icImg.preserveAspect = true; icImg.raycastTarget = false;
        LayoutElement le = ic.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 20f; le.preferredHeight = 22f;

        TMP_Text t = Text(content, label, fontSize, (Color)textCol, TextAlignmentOptions.Center, 0f, 0f, 10f, 10f);
        t.textWrappingMode = TextWrappingModes.NoWrap;

        HoverSound(rt.gameObject);
        return b;
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

    // ------------------------------------------------------------------ yardımcılar

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        return rt;
    }

    /// <summary>Sol üst köşeye göre yerleşim (y aşağı doğru negatif).</summary>
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Image Img(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        RectTransform rt = NewRect(parent, name);
        Place(rt, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align,
                                 float x, float y, float w, float h)
    {
        RectTransform rt = NewRect(parent, "Text");
        Place(rt, x, y, w, h);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.margin = Vector4.zero;
        return t;
    }

    private static void HoverSound(GameObject go)
    {
        EventTrigger et = go.GetComponent<EventTrigger>();
        if (et == null) et = go.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerEnter, () => Sfx.Play("ui_hover", 0.3f));
    }

    private static void AddTrigger(EventTrigger et, EventTriggerType type, Action a)
    {
        EventTrigger.Entry e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => a());
        et.triggers.Add(e);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
