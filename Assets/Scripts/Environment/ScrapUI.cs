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
    private TMP_Text counterText;
    private Image counterIcon;
    private GameObject cpuBox;
    private TMP_Text cpuText;
    private Image cpuIcon;
    private int lastShownCpu = int.MinValue;
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

        GameObject counterBox = new GameObject("ScrapCounterBox", typeof(RectTransform), typeof(Image));
        counterBox.transform.SetParent(canvasGo.transform, false);
        RectTransform cbr = counterBox.GetComponent<RectTransform>();
        cbr.anchorMin = cbr.anchorMax = new Vector2(0f, 1f);
        cbr.pivot = new Vector2(0f, 1f);
        cbr.anchoredPosition = new Vector2(24f, -20f);
        cbr.sizeDelta = new Vector2(240f, 58f);
        Image cbImg = counterBox.GetComponent<Image>();
        cbImg.color = new Color(0.06f, 0.09f, 0.13f, 0.88f);
        cbImg.raycastTarget = false;

        GameObject iconGo = new GameObject("ScrapIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(counterBox.transform, false);
        RectTransform ir = iconGo.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
        ir.pivot = new Vector2(0f, 0.5f);
        ir.anchoredPosition = new Vector2(10f, 0f);
        ir.sizeDelta = new Vector2(44f, 44f);
        counterIcon = iconGo.GetComponent<Image>();
        counterIcon.sprite = RuntimeArt.ScrapIcon;
        counterIcon.preserveAspect = true;
        counterIcon.raycastTarget = false;

        counterText = MakeText(counterBox.transform, "Counter", 32, TextAlignmentOptions.Left);
        RectTransform cr = counterText.rectTransform;
        cr.anchorMin = new Vector2(0f, 0f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.offsetMin = new Vector2(62f, 0f);
        cr.offsetMax = new Vector2(-10f, 0f);

        cpuBox = new GameObject("CpuCounterBox", typeof(RectTransform), typeof(Image));
        cpuBox.transform.SetParent(canvasGo.transform, false);
        RectTransform cpbr = cpuBox.GetComponent<RectTransform>();
        cpbr.anchorMin = cpbr.anchorMax = new Vector2(0f, 0f);
        cpbr.pivot = new Vector2(0f, 0f);
        cpbr.anchoredPosition = new Vector2(24f, 24f);
        cpbr.sizeDelta = new Vector2(220f, 58f);
        Image cpbImg = cpuBox.GetComponent<Image>();
        cpbImg.color = new Color(0.05f, 0.11f, 0.16f, 0.90f);
        cpbImg.raycastTarget = false;

        GameObject cpuIconGo = new GameObject("CpuIcon", typeof(RectTransform), typeof(Image));
        cpuIconGo.transform.SetParent(cpuBox.transform, false);
        RectTransform cir = cpuIconGo.GetComponent<RectTransform>();
        cir.anchorMin = cir.anchorMax = new Vector2(0f, 0.5f);
        cir.pivot = new Vector2(0f, 0.5f);
        cir.anchoredPosition = new Vector2(10f, 0f);
        cir.sizeDelta = new Vector2(44f, 44f);
        cpuIcon = cpuIconGo.GetComponent<Image>();
        cpuIcon.sprite = RuntimeArt.CpuIcon;
        cpuIcon.preserveAspect = true;
        cpuIcon.raycastTarget = false;

        cpuText = MakeText(cpuBox.transform, "CpuCounter", 32, TextAlignmentOptions.Left);
        cpuText.color = new Color(0.55f, 0.98f, 1f);
        RectTransform cptr = cpuText.rectTransform;
        cptr.anchorMin = new Vector2(0f, 0f);
        cptr.anchorMax = new Vector2(1f, 1f);
        cptr.offsetMin = new Vector2(62f, 0f);
        cptr.offsetMax = new Vector2(-10f, 0f);
        cpuBox.SetActive(false);

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

    private void UpdateCounter()
    {
        if (counterText != null) counterText.text = "Hurda: " + ScrapInventory.Count;
    }

    private void UpdateCpuDisplay()
    {
        if (cpuBox == null) return;

        TurnManager tm = TurnManager.Instance;
        bool inCombat = tm != null && tm.IsInCombat();
        if (cpuBox.activeSelf != inCombat)
        {
            cpuBox.SetActive(inCombat);
            lastShownCpu = int.MinValue;
        }

        if (!inCombat) return;

        Unit cpuUnit = FindPlayerCpuUnit(tm);
        int currentCpu = cpuUnit != null ? cpuUnit.currentCPU : 0;
        if (currentCpu != lastShownCpu && cpuText != null)
        {
            lastShownCpu = currentCpu;
            cpuText.text = "Cpu: " + currentCpu;
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
