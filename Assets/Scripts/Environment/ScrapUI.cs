using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScrapUI : MonoBehaviour
{
    private static ScrapUI instance;
    public static ScrapUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ScrapUI>();
                if (instance == null) instance = new GameObject("ScrapUI").AddComponent<ScrapUI>();
            }
            return instance;
        }
    }

    private GameObject panel;
    private Image progressFill;
    private GameObject progressBar;
    private TMP_Text resultText;
    // Hurda paneli
    private TMP_Text counterText;
    private TMP_Text scrapDeltaText;
    private int lastScrap = int.MinValue;
    private float scrapDeltaUntil;

    // CPU paneli
    private GameObject cpuBox;
    private RectTransform cpuRect;
    private Image cpuIcon;
    private TMP_Text cpuLabel;
    private TMP_Text cpuValue;
    private TMP_Text cpuSub;
    private RectTransform cpuTextBlock;
    private readonly List<Image> cpuCells = new List<Image>();
    private int builtCellCount = -1;
    private const int MaxCells = 12;
    private bool busy;
    private float resultHideTime;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
        ScrapInventory.OnChanged += UpdateCounter;
        UpdateCounter();
    }

    private void OnDestroy() { ScrapInventory.OnChanged -= UpdateCounter; }

    private void Update()
    {
        if (resultText != null && resultText.gameObject.activeSelf && Time.time > resultHideTime && !busy)
            resultText.gameObject.SetActive(false);

        UpdateCpuDisplay();
        UpdateScrapDelta();
    }

    public void BeginSearch()
    {
        busy = true;
        panel.SetActive(true);
        progressBar.SetActive(true);
        progressFill.fillAmount = 0f;
        resultText.gameObject.SetActive(false);
    }

    public void SetProgress(float t) { progressFill.fillAmount = t; }

    public void EndSearch()
    {
        busy = false;
        progressBar.SetActive(false);
        panel.SetActive(false);
    }

    public void ShowResult(string text)
    {
        progressBar.SetActive(false);
        resultText.text = text;
        resultText.gameObject.SetActive(true);
        resultHideTime = Time.time + 3f;
    }

    private void Build()
    {
        GameObject canvasGo = new GameObject("ScrapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildScrapPanel(canvasGo.transform);
        BuildCpuPanel(canvasGo.transform);

        panel = new GameObject("SearchPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, 330f);
        pr.sizeDelta = new Vector2(460f, 130f);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.12f, 0.92f);

        TMP_Text title = MakeText(panel.transform, "Title", 30, TextAlignmentOptions.Center);
        title.text = "Hurda aranıyor...";
        Place(title.rectTransform, 0.05f, 0.5f, 0.95f, 0.95f);

        progressBar = new GameObject("Progress", typeof(RectTransform), typeof(Image));
        progressBar.transform.SetParent(panel.transform, false);
        Place(progressBar.GetComponent<RectTransform>(), 0.1f, 0.18f, 0.9f, 0.38f);
        progressBar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(progressBar.transform, false);
        Place(fill.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        progressFill = fill.GetComponent<Image>();
        progressFill.color = new Color(0.4f, 0.95f, 0.5f);
        progressFill.sprite = RuntimeArt.Square;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;
        progressBar.SetActive(false);

        resultText = MakeText(canvasGo.transform, "Result", 36, TextAlignmentOptions.Center);
        RectTransform rr = resultText.rectTransform;
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0f);
        rr.pivot = new Vector2(0.5f, 0f);
        rr.anchoredPosition = new Vector2(0f, 560f);
        rr.sizeDelta = new Vector2(900f, 120f);
        resultText.color = new Color(1f, 0.92f, 0.4f);
        resultText.gameObject.SetActive(false);

        panel.SetActive(false);
    }

    private static TMP_Text MakeText(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ================================================================== HUD panelleri

    /// <summary>Sanat pikseli cinsinden sol-üst köşeye göre yerleşim (1 px = HudArt.Px birim).</summary>
    private static RectTransform ArtRect(Transform parent, string name, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * HudArt.Px, -y * HudArt.Px);
        rt.sizeDelta = new Vector2(w * HudArt.Px, h * HudArt.Px);
        return rt;
    }

    private static Image ArtImage(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        RectTransform rt = ArtRect(parent, name, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text ArtText(Transform parent, string name, float size, Color color, TextAlignmentOptions align,
                                    float x, float y, float w, float h)
    {
        TMP_Text t = MakeText(parent, name, size, align);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * HudArt.Px, -y * HudArt.Px);
        rt.sizeDelta = new Vector2(w * HudArt.Px, h * HudArt.Px);
        t.color = color;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero;
        return t;
    }

    private void BuildScrapPanel(Transform canvas)
    {
        // 84x24 sanat pikseli: amber çerçeveli, kesik köşeli panel
        RectTransform box = ArtRect(canvas, "ScrapCounterBox", 0, 0, 84, 24);
        box.anchoredPosition = new Vector2(24f, -20f);
        Image bg = box.gameObject.AddComponent<Image>();
        bg.sprite = HudArt.Panel(HudArt.AmberDark);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        ArtImage(box, "Accent", HudArt.Solid, (Color)HudArt.Cyan, 8, 1, 14, 1);
        ArtImage(box, "SocketEdge", HudArt.Solid, (Color)HudArt.Outline, 4, 3, 18, 18);
        ArtImage(box, "Socket", HudArt.Solid, (Color)HudArt.Socket, 5, 4, 16, 16);
        ArtImage(box, "ScrapIcon", HudArt.ScrapIcon, Color.white, 6, 5, 14, 14);

        ArtText(box, "Label", 16, (Color)HudArt.Dim, TextAlignmentOptions.MidlineLeft, 25, 3, 40, 7).text = "HURDA";
        counterText = ArtText(box, "Counter", 34, (Color)HudArt.Amber, TextAlignmentOptions.MidlineLeft, 25, 9, 40, 13);
        scrapDeltaText = ArtText(box, "Delta", 22, (Color)HudArt.Green, TextAlignmentOptions.MidlineRight, 50, 9, 30, 13);
        scrapDeltaText.gameObject.SetActive(false);
    }

    private void BuildCpuPanel(Transform canvas)
    {
        cpuRect = ArtRect(canvas, "CpuCounterBox", 0, 0, 100, 28);
        cpuRect.anchorMin = cpuRect.anchorMax = new Vector2(0f, 0f);
        cpuRect.pivot = new Vector2(0f, 0f);
        cpuRect.anchoredPosition = new Vector2(24f, 24f);
        cpuBox = cpuRect.gameObject;
        Image bg = cpuBox.AddComponent<Image>();
        bg.sprite = HudArt.Panel(HudArt.CyanMid);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        ArtImage(cpuRect, "Accent", HudArt.Solid, (Color)HudArt.Cyan, 8, 1, 10, 1);
        cpuIcon = ArtImage(cpuRect, "CpuIcon", HudArt.ChipIcon, Color.white, 5, 5, 9, 9);
        cpuLabel = ArtText(cpuRect, "Label", 15, (Color)HudArt.Dim, TextAlignmentOptions.Center, 1, 16, 17, 8);
        cpuLabel.text = "CPU";

        cpuTextBlock = ArtRect(cpuRect, "Text", 0, 0, 40, 28);
        cpuValue = ArtText(cpuTextBlock, "Value", 30, (Color)HudArt.Cyan, TextAlignmentOptions.MidlineLeft, 0, 3, 40, 12);
        cpuSub = ArtText(cpuTextBlock, "Sub", 17, (Color)HudArt.Dim, TextAlignmentOptions.MidlineLeft, 0, 15, 40, 9);

        cpuBox.SetActive(false);
    }

    /// <summary>Hücre sayısı birimin maksimumuna göre değişince paneli yeniden boyutlandırır.</summary>
    private void LayoutCpuCells(int count)
    {
        if (count == builtCellCount) return;
        builtCellCount = count;

        while (cpuCells.Count < count)
            cpuCells.Add(ArtImage(cpuRect, "Cell" + cpuCells.Count, HudArt.Cell(HudArt.CellState.Empty), Color.white, 0, 0, 8, 13));

        const float x0 = 20f, pitch = 10f;
        for (int i = 0; i < cpuCells.Count; i++)
        {
            bool on = i < count;
            cpuCells[i].gameObject.SetActive(on);
            if (on) cpuCells[i].rectTransform.anchoredPosition = new Vector2((x0 + i * pitch) * HudArt.Px, -7f * HudArt.Px);
        }

        float textX = x0 + count * pitch + 3f;
        cpuTextBlock.anchoredPosition = new Vector2(textX * HudArt.Px, 0f);
        cpuRect.sizeDelta = new Vector2((textX + 40f) * HudArt.Px, 28f * HudArt.Px);
    }

    private void UpdateCounter()
    {
        int n = ScrapInventory.Count;
        if (counterText != null) counterText.text = n.ToString();

        // Artış olduysa kısa süre "+N" göster (ilk açılışta değil)
        if (lastScrap != int.MinValue && n > lastScrap && scrapDeltaText != null)
        {
            scrapDeltaText.text = "+" + (n - lastScrap);
            scrapDeltaText.gameObject.SetActive(true);
            scrapDeltaUntil = Time.unscaledTime + 1.6f;
        }
        lastScrap = n;
    }

    private void UpdateScrapDelta()
    {
        if (scrapDeltaText == null || !scrapDeltaText.gameObject.activeSelf) return;
        float left = scrapDeltaUntil - Time.unscaledTime;
        if (left <= 0f) { scrapDeltaText.gameObject.SetActive(false); return; }
        Color c = HudArt.Green;
        c.a = Mathf.Clamp01(left / 0.5f);
        scrapDeltaText.color = c;
    }

    private void UpdateCpuDisplay()
    {
        if (cpuBox == null) return;

        TurnManager tm = TurnManager.Instance;
        CardManager cm = CardManager.Instance;
        bool inCombat = tm != null && tm.IsInCombat() && !tm.IsGameOver;

        // Kartları oynayan birim: sırası gelen dost birim; yoksa ana drone.
        Unit u = null;
        if (inCombat)
        {
            if (cm != null) u = cm.GetActiveUnit();
            if (u == null || u.IsDead) u = FindPlayerCpuUnit(tm);
        }

        bool show = inCombat && u != null;
        if (cpuBox.activeSelf != show) cpuBox.SetActive(show);
        if (!show) return;

        // CPU kullanan birimde CPU, kullanmayanlarda tur başı kart hakkı gösterilir.
        bool cpuMode = u.usesCpu;
        int max = Mathf.Max(0, cpuMode ? u.maxCPU : u.cardsPerTurn);
        int cur = cpuMode ? u.currentCPU : u.cardsPerTurn - u.cardsPlayedThisTurn;
        cur = Mathf.Clamp(cur, 0, max);

        int cells = Mathf.Clamp(max, 1, MaxCells);
        LayoutCpuCells(cells);

        // Maksimum 12'yi aşarsa hücreler orantılı dolar; sayı her zaman gerçek değeri gösterir.
        int filled = max <= MaxCells ? cur : Mathf.RoundToInt(cur * (float)cells / max);

        // Önizleme: seçili (hedeflenen) kart ya da fareyle üstüne gelinen kart
        CardData preview = null;
        if (tm.CurrentState == TurnState.PlayerTurn && cm != null)
            preview = cm.PendingCard != null ? cm.PendingCard : CardView.HoveredCard;

        int cost = preview == null ? 0 : (cpuMode ? preview.cpuCost : 1);
        int costCells = max <= MaxCells ? cost : Mathf.CeilToInt(cost * (float)cells / max);
        bool shortfall = cost > cur;
        bool blinkOn = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.6f;

        for (int i = 0; i < cells; i++)
        {
            HudArt.CellState st;
            if (i < filled)
                st = (!shortfall && cost > 0 && i >= filled - costCells && blinkOn) ? HudArt.CellState.Spend : HudArt.CellState.On;
            else
                st = (shortfall && i < Mathf.Min(costCells, cells)) ? HudArt.CellState.Short : HudArt.CellState.Empty;

            Sprite sp = HudArt.Cell(st);
            if (cpuCells[i].sprite != sp) cpuCells[i].sprite = sp;
        }

        cpuIcon.sprite = cpuMode ? HudArt.ChipIcon : HudArt.CardIcon;
        cpuLabel.text = cpuMode ? "CPU" : "KART";
        cpuValue.text = cur + "/" + max;

        if (cost > 0 && shortfall)
        {
            cpuSub.text = "YETERSİZ";
            cpuSub.color = HudArt.Red;
        }
        else if (cost > 0)
        {
            cpuSub.text = "-" + cost + (cpuMode ? " CPU" : " KART");
            cpuSub.color = HudArt.Orange;
        }
        else
        {
            cpuSub.text = cpuMode ? "+" + u.cpuPerTurn + " TUR" : "HER TUR";
            cpuSub.color = HudArt.Dim;
        }
    }

    private static Unit FindPlayerCpuUnit(TurnManager tm)
    {
        if (tm == null) return null;
        Unit fallback = null;
        foreach (Unit u in tm.playerUnits)
        {
            if (u == null) continue;
            if (u.isMainDrone) return u;
            if (fallback == null && u.usesCpu) fallback = u;
        }
        return fallback;
    }
}
