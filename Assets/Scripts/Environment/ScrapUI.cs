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

    // Arama paneli ("HURDA ARANIYOR")
    private GameObject panel;
    private Image progressFill;
    private GameObject progressBar;
    private TMP_Text searchTitle;
    private TMP_Text searchPercent;
    private RectTransform searchIcon;
    private int lastDots = -1;
    private int lastPercent = -1;

    // Sonuç paneli ("HURDA BULUNDU")
    private RectTransform resultBox;
    private CanvasGroup resultGroup;
    private TMP_Text resultLabel;
    private TMP_Text resultAmount;
    private TMP_Text resultExtra;
    private float resultShownAt;
    private const float ResultDuration = 3f;
    private const float ResultFade = 0.5f;
    private const float PanelW = 150f;   // sanat pikseli
    private const float PanelH = 28f;
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
        UpdateSearchPanel();
        UpdateResultPanel();
        UpdateCpuDisplay();
        UpdateScrapDelta();
    }

    public void BeginSearch()
    {
        busy = true;
        panel.SetActive(true);
        progressBar.SetActive(true);
        lastDots = -1;
        lastPercent = -1;
        SetProgress(0f);
        resultBox.gameObject.SetActive(false);
    }

    public void SetProgress(float t)
    {
        progressFill.fillAmount = t;
        int pct = Mathf.Clamp(Mathf.RoundToInt(t * 100f), 0, 100);
        if (pct != lastPercent)
        {
            lastPercent = pct;
            searchPercent.text = "%" + pct;
        }
    }

    public void EndSearch()
    {
        busy = false;
        progressBar.SetActive(false);
        panel.SetActive(false);
    }

    /// <summary>Arama sonucu: bulunan hurda miktarı ve (varsa) ek satırlar (blueprint, el dolu uyarısı...).</summary>
    public void ShowResult(int scrapAmount, string extraLines)
    {
        ShowResult("HURDA BULUNDU", "+" + scrapAmount, (Color)HudArt.Amber, extraLines);
    }

    /// <summary>
    /// Yerden ganimet toplandı (düşman drone'larının bıraktığı hurda/kartlar). Arama sonucuyla aynı panel.
    /// </summary>
    public void ShowLoot(int scrapAmount, int cardCount, string extraLines)
    {
        string headline;
        Color color;
        if (scrapAmount > 0) { headline = "+" + scrapAmount; color = (Color)HudArt.Amber; }
        else if (cardCount > 0) { headline = "+" + cardCount + " KART"; color = (Color)HudArt.Cyan; }
        else { headline = "EL DOLU"; color = (Color)HudArt.Orange; }
        ShowResult("GANİMET TOPLANDI", headline, color, extraLines);
    }

    public void ShowResult(string label, string headline, Color headlineColor, string extraLines)
    {
        if (busy) return;
        resultLabel.text = label;
        resultAmount.text = headline;
        resultAmount.color = headlineColor;

        string extra = string.IsNullOrEmpty(extraLines) ? "" : extraLines.Trim('\n');
        float baseH = PanelH;
        if (extra.Length > 0)
        {
            resultExtra.gameObject.SetActive(true);
            resultExtra.text = extra;
            float textW = (PanelW - 16f) * HudArt.Px;
            float h = resultExtra.GetPreferredValues(extra, textW, 0f).y;
            float hArt = Mathf.Ceil(h / HudArt.Px) + 2f;
            resultExtra.rectTransform.sizeDelta = new Vector2(textW, hArt * HudArt.Px);
            baseH += hArt + 2f;
        }
        else
        {
            resultExtra.gameObject.SetActive(false);
        }
        resultBox.sizeDelta = new Vector2(PanelW * HudArt.Px, baseH * HudArt.Px);

        resultBox.gameObject.SetActive(true);
        resultGroup.alpha = 1f;
        resultBox.localScale = Vector3.one;
        resultShownAt = Time.unscaledTime;
    }

    /// <summary>Eski çağrılar için: düz metin sonucu.</summary>
    public void ShowResult(string text) { ShowResult("HURDA BULUNDU", "", (Color)HudArt.Amber, text); }

    private void UpdateSearchPanel()
    {
        if (panel == null || !panel.activeSelf) return;

        // Başlıkta dönen noktalar ve hafif zıplayan ikon: arama sürüyor hissi.
        int dots = Mathf.FloorToInt(Time.unscaledTime * 3f) % 4;
        if (dots != lastDots)
        {
            lastDots = dots;
            searchTitle.text = "HURDA ARANIYOR" + new string('.', dots);
        }
        if (searchIcon != null)
            searchIcon.anchoredPosition = new Vector2(6f * HudArt.Px,
                -(7f + (Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f ? 0f : 1f)) * HudArt.Px);
    }

    private void UpdateResultPanel()
    {
        if (resultBox == null || !resultBox.gameObject.activeSelf) return;
        if (busy) { resultBox.gameObject.SetActive(false); return; }

        float t = Time.unscaledTime - resultShownAt;
        // Kısa bir "pop" ile açılır, sürenin sonunda yavaşça kaybolur.
        float pop = Mathf.Clamp01(t / 0.15f);
        resultBox.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, pop);

        float left = ResultDuration - t;
        if (left <= 0f) { resultBox.gameObject.SetActive(false); return; }
        resultGroup.alpha = Mathf.Clamp01(left / ResultFade);
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
        BuildSearchPanel(canvasGo.transform);
        BuildResultPanel(canvasGo.transform);
    }

    /// <summary>Hurda sayacıyla aynı dil: amber çerçeveli kesik köşeli panel, ikon yuvası, piksel ilerleme çubuğu.</summary>
    private void BuildSearchPanel(Transform canvas)
    {
        RectTransform pr = ArtRect(canvas, "SearchPanel", 0, 0, PanelW, PanelH);
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, 330f);
        panel = pr.gameObject;
        Image bg = panel.AddComponent<Image>();
        bg.sprite = HudArt.Panel(HudArt.AmberDark);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        ArtImage(pr, "Accent", HudArt.Solid, (Color)HudArt.Cyan, 8, 1, 14, 1);
        ArtImage(pr, "SocketEdge", HudArt.Solid, (Color)HudArt.Outline, 4, 5, 18, 18);
        ArtImage(pr, "Socket", HudArt.Solid, (Color)HudArt.Socket, 5, 6, 16, 16);
        searchIcon = ArtImage(pr, "ScrapIcon", HudArt.ScrapIcon, Color.white, 6, 7, 14, 14).rectTransform;

        searchTitle = ArtText(pr, "Title", 18, (Color)HudArt.Amber, TextAlignmentOptions.MidlineLeft, 26, 4, 90, 9);
        searchTitle.text = "HURDA ARANIYOR";
        searchPercent = ArtText(pr, "Percent", 16, (Color)HudArt.Dim, TextAlignmentOptions.MidlineRight, 110, 4, 34, 9);

        // Çubuk: koyu çerçeve + yuva + amber dolgu
        RectTransform bar = ArtRect(pr, "Progress", 26, 15, 118, 9);
        progressBar = bar.gameObject;
        ArtImage(bar, "Edge", HudArt.Solid, (Color)HudArt.Outline, 0, 0, 118, 9);
        ArtImage(bar, "Back", HudArt.Solid, (Color)HudArt.Socket, 1, 1, 116, 7);
        progressFill = ArtImage(bar, "Fill", HudArt.Solid, (Color)HudArt.Amber, 1, 1, 116, 7);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        ArtImage(bar, "Shine", HudArt.Solid, new Color(1f, 1f, 1f, 0.18f), 1, 1, 116, 2);
        progressBar.SetActive(false);

        panel.SetActive(false);
    }

    private void BuildResultPanel(Transform canvas)
    {
        resultBox = ArtRect(canvas, "ResultPanel", 0, 0, PanelW, PanelH);
        resultBox.anchorMin = resultBox.anchorMax = new Vector2(0.5f, 0f);
        resultBox.pivot = new Vector2(0.5f, 0f);
        resultBox.anchoredPosition = new Vector2(0f, 330f);
        resultGroup = resultBox.gameObject.AddComponent<CanvasGroup>();
        resultGroup.blocksRaycasts = false;
        resultGroup.interactable = false;
        Image bg = resultBox.gameObject.AddComponent<Image>();
        bg.sprite = HudArt.Panel(HudArt.AmberDark);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        ArtImage(resultBox, "Accent", HudArt.Solid, (Color)HudArt.Green, 8, 1, 14, 1);
        ArtImage(resultBox, "SocketEdge", HudArt.Solid, (Color)HudArt.Outline, 4, 5, 18, 18);
        ArtImage(resultBox, "Socket", HudArt.Solid, (Color)HudArt.Socket, 5, 6, 16, 16);
        ArtImage(resultBox, "ScrapIcon", HudArt.ScrapIcon, Color.white, 6, 7, 14, 14);

        resultLabel = ArtText(resultBox, "Label", 16, (Color)HudArt.Dim, TextAlignmentOptions.MidlineLeft, 26, 4, 110, 7);
        resultLabel.text = "HURDA BULUNDU";
        resultAmount = ArtText(resultBox, "Amount", 34, (Color)HudArt.Amber, TextAlignmentOptions.MidlineLeft, 26, 11, 110, 13);

        resultExtra = ArtText(resultBox, "Extra", 18, (Color)HudArt.White, TextAlignmentOptions.TopLeft, 8, PanelH, PanelW - 16f, 9);
        resultExtra.textWrappingMode = TextWrappingModes.Normal;
        resultExtra.richText = true;

        resultBox.gameObject.SetActive(false);
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
        cpuIcon = ArtImage(cpuRect, "CpuIcon", HudArt.ChipIcon, Color.white, 4, 3, 11, 11);
        cpuIcon.preserveAspect = true;
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
