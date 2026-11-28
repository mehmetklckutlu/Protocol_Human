using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Oyundaki bütün kodla kurulan arayüzlerin ortak dili (hurda sayacı / hurda arama / ayarlar menüsüyle aynı):
/// kesik köşeli piksel panel (HudArt.Panel), sol üstte renkli şerit, piksel font, ortalanmış ve
/// kutuya sığacak kadar küçülen yazılar. Yeni bir UI parçası yaparken buradaki yardımcıları kullan.
/// </summary>
public static class HudUI
{
    // ------------------------------------------------------------------ canvas

    public static Canvas MakeCanvas(string name, int sortingOrder, Transform parent = null, bool raycaster = true)
    {
        GameObject go = raycaster
            ? new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            : new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        if (parent != null) go.transform.SetParent(parent, false);
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;
        CanvasScaler sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        return c;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ------------------------------------------------------------------ yerleşim

    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>anchor/pivot aynı noktada, pos o noktaya göre.</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>Ebeveynini kenarlardan verilen boşluklarla doldurur.</summary>
    public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // ------------------------------------------------------------------ parçalar

    /// <summary>Kesik köşeli HUD paneli (9-slice). Boyut/konumu çağıran ayarlar.</summary>
    public static Image Panel(Transform parent, string name, Color32 border, Color32? fill = null)
    {
        RectTransform rt = Rect(parent, name);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = fill.HasValue ? HudArt.Panel(border, fill.Value) : HudArt.Panel(border);
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Panelin sol üstündeki ince renkli şerit (HUD panellerindeki gibi).</summary>
    public static Image Accent(Transform panel, Color color, float width = 66f)
    {
        RectTransform rt = Rect(panel, "Accent");
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f * HudArt.Px, -1f * HudArt.Px);
        rt.sizeDelta = new Vector2(width, HudArt.Px);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = HudArt.Solid;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Piksel fontlu yazı; varsayılan olarak ebeveynini doldurur. Yazı kutuya sığmazsa minSize'a kadar küçülür,
    /// böylece panel dışına taşmaz.
    /// </summary>
    public static TMP_Text Text(Transform parent, string name, string text, float size, Color color,
                                TextAlignmentOptions align = TextAlignmentOptions.Center, float minSize = -1f)
    {
        RectTransform rt = Rect(parent, name);
        Stretch(rt);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.margin = Vector4.zero;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        if (minSize > 0f)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = minSize;
        }
        return t;
    }

    /// <summary>
    /// HUD temalı buton (ayarlar menüsündeki butonlarla aynı): kesik köşeli panel, üstüne gelince parlar,
    /// yazı ortada ve butona sığar. Boyut/konumu çağıran ayarlar.
    /// </summary>
    public static UnityEngine.UI.Button MakeButton(Transform parent, string name, string label, float fontSize,
                                Color32 border, Color32 fill, Color textColor, UnityAction onClick,
                                out TMP_Text labelText)
    {
        RectTransform rt = Rect(parent, name);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = HudArt.Panel(border, fill);
        bg.type = Image.Type.Sliced;

        UnityEngine.UI.Button b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = bg;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.06f;
        b.colors = cb;
        if (onClick != null) b.onClick.AddListener(onClick);

        labelText = Text(rt, "Label", label, fontSize, textColor, TextAlignmentOptions.Center, fontSize * 0.6f);
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.fontStyle = FontStyles.Bold;
        Stretch(labelText.rectTransform, 14f, 14f, 6f, 6f);

        EventTrigger et = rt.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        e.callback.AddListener(_ => Sfx.Play("ui_hover", 0.3f));
        et.triggers.Add(e);
        return b;
    }

    /// <summary>Var olan (sahnedeki) bir butonu HUD temasına çevirir ve etiketini düzgünce ortalar.</summary>
    public static void RestyleButton(GameObject buttonGo, Color32 border, Color32 fill)
    {
        if (buttonGo == null) return;
        Image img = buttonGo.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = HudArt.Panel(border, fill);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        UnityEngine.UI.Button btn = buttonGo.GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
        }
        foreach (Outline o in buttonGo.GetComponents<Outline>()) Object.Destroy(o);
    }
}

/// <summary>
/// Ekranın üst ortasında kısa süre görünen HUD bildirimi ("DÜŞMAN HAREKETİ", "TUR BİTTİ"...).
/// Sahnedeki eski düz yazıların yerini alır; her sahnede ilk kullanımda kendiliğinden kurulur.
/// </summary>
public class HudBanner : MonoBehaviour
{
    private static HudBanner instance;

    private CanvasGroup group;
    private RectTransform box;
    private Image bg, accent;
    private TMP_Text title, sub;
    private Coroutine routine;

    public static void Show(string text, Color32 color, float duration = 1.5f, string subText = null)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("[HudBanner]");
            instance = go.AddComponent<HudBanner>();
            instance.Build();
        }
        if (instance.routine != null) instance.StopCoroutine(instance.routine);
        instance.routine = instance.StartCoroutine(instance.Run(text, color, duration, subText));
    }

    private void Build()
    {
        Canvas c = HudUI.MakeCanvas("BannerCanvas", 420, transform, false);
        group = c.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        bg = HudUI.Panel(c.transform, "Banner", HudArt.CyanMid);
        box = bg.rectTransform;
        // Tur sırası çubuğunun hemen altında
        HudUI.Place(box, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(460f, 84f));
        accent = HudUI.Accent(box, Color.white, 80f);

        title = HudUI.Text(box, "Title", "", 34, Color.white, TextAlignmentOptions.Center, 20f);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.fontStyle = FontStyles.Bold;
        HudUI.Stretch(title.rectTransform, 18f, 18f, 10f, 10f);

        sub = HudUI.Text(box, "Sub", "", 18, (Color)HudArt.Dim, TextAlignmentOptions.Center, 12f);
        HudUI.Stretch(sub.rectTransform, 18f, 18f, 50f, 6f);
        sub.gameObject.SetActive(false);
    }

    private IEnumerator Run(string text, Color32 color, float duration, string subText)
    {
        bg.sprite = HudArt.Panel(Darken(color), HudArt.Fill);
        accent.color = color;
        title.text = text;
        title.color = color;
        bool hasSub = !string.IsNullOrEmpty(subText);
        sub.gameObject.SetActive(hasSub);
        if (hasSub) sub.text = subText;
        HudUI.Stretch(title.rectTransform, 18f, 18f, hasSub ? 8f : 10f, hasSub ? 34f : 10f);
        box.sizeDelta = new Vector2(460f, hasSub ? 104f : 84f);

        // Kısa "pop" ile açılır, sonra söner
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.15f;
            group.alpha = k;
            box.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        box.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration - 0.45f));

        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.3f;
            yield return null;
        }
        group.alpha = 0f;
        routine = null;
    }

    private static Color32 Darken(Color32 c)
    {
        return new Color32((byte)(c.r * 0.6f), (byte)(c.g * 0.6f), (byte)(c.b * 0.6f), 255);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
