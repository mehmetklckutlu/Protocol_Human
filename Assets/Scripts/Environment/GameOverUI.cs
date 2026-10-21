using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Oyun kaybedilince tam ekran "KAYBETTİN" yazısı gösterir ve oyunu durdurur.
public static class GameOverUI
{
    public static void Show()
    {
        GameObject canvasGo = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject bg = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        RectTransform r = bg.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        MakeText(canvasGo.transform, "KAYBETTİN", 120, new Color(1f, 0.25f, 0.25f), 80f);
        MakeText(canvasGo.transform, "Yeniden başlatmak için oyunu durdurup tekrar başlat", 36, Color.white, -80f);

        Debug.Log("<color=red>OYUN KAYBEDİLDİ</color>");
        Time.timeScale = 0f;
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
