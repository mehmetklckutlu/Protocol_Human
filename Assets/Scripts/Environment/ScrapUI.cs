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

        counterText = MakeText(canvasGo.transform, "Counter", 34, TextAlignmentOptions.Left);
        RectTransform cr = counterText.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
        cr.pivot = new Vector2(0f, 1f);
        cr.anchoredPosition = new Vector2(30f, -25f);
        cr.sizeDelta = new Vector2(400f, 50f);

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
}
