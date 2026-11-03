using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

public static class GameOverUI
{
    public const string MainMenuScene = "AnaMenü";

    public static void Show()
    {
        if (GameObject.Find("GameOverCanvas") != null) return;

        GameObject canvasGo = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bg = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        RectTransform r = bg.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        MakeText(canvasGo.transform, "KAYBETTİN", 120, new Color(1f, 0.25f, 0.25f), 110f);
        MakeButton(canvasGo.transform, "ANA MENÜYE DÖN", -60f, GoToMainMenu);

        EnsureEventSystem();

        Debug.Log("<color=red>OYUN KAYBEDİLDİ</color>");
        Time.timeScale = 0f;
    }

    public static void GoToMainMenu()
    {
        Time.timeScale = 1f;
        ScrapInventory.Reset();
        if (Application.CanStreamedLevelBeLoaded(MainMenuScene)) SceneManager.LoadScene(MainMenuScene);
        else SceneManager.LoadScene(0);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void MakeButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("MainMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 110f);
        rt.anchoredPosition = new Vector2(0f, y);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.05f, 0.12f, 0.16f, 0.95f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.6f, 1f, 1f);
        cb.pressedColor = new Color(0.4f, 0.8f, 0.8f);
        cb.fadeDuration = 0.05f;
        b.colors = cb;
        b.onClick.AddListener(onClick);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.95f, 1f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        TextMeshProUGUI t = textGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 44;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.4f, 1f, 1f);
        t.raycastTarget = false;
    }

    private static void MakeText(Transform parent, string text, float size, Color color, float y)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1600f, 200f);
        rt.anchoredPosition = new Vector2(0f, y);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
    }
}
