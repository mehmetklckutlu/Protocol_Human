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

        // Diğer HUD pencereleriyle aynı dil: karartılmış arka plan üstünde kesik köşeli kırmızı çerçeveli pencere.
        Canvas canvas = HudUI.MakeCanvas("GameOverCanvas", 500);
        GameObject canvasGo = canvas.gameObject;

        GameObject bg = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        HudUI.Stretch((RectTransform)bg.transform);
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        Color32 red = HudArt.Red;
        Image frame = HudUI.Panel(canvasGo.transform, "Frame", new Color32(150, 40, 40, 255), new Color32(30, 8, 10, 240));
        frame.raycastTarget = true;
        HudUI.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 380f));
        HudUI.Accent(frame.transform, (Color)red, 120f);

        TMPro.TMP_Text status = HudUI.Text(frame.transform, "Status", "SİSTEM ÇÖKTÜ · PH-01 ÇEVRİMDIŞI", 20,
                                           (Color)HudArt.Dim, TextAlignmentOptions.Center, 12f);
        status.textWrappingMode = TextWrappingModes.NoWrap;
        HudUI.Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(680f, 30f));

        TMPro.TMP_Text title = HudUI.Text(frame.transform, "Title", "KAYBETTİN", 96, (Color)red,
                                          TextAlignmentOptions.Center, 48f);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        HudUI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(680f, 120f));

        Button b = HudUI.MakeButton(frame.transform, "MainMenuButton", "ANA MENÜYE DÖN", 32,
            HudArt.CyanMid, new Color32(14, 70, 88, 255), (Color)HudArt.White, GoToMainMenu, out _);
        HudUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(460f, 80f));

        HudUI.EnsureEventSystem();

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

}
