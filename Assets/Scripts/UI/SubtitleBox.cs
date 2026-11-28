using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ekranın altında, alt yazı gibi duran konuşma kutusu (tutorial, labirent girişi...).
/// HUD panelleriyle (hurda sayacı, hurda arama) aynı dil: kesik köşeli piksel panel, renkli şerit, piksel font.
/// Kutu tıklamaları engellemez: konuşma sürerken drone hareket ettirilebilir.
/// Ekrana tıklamak, Boşluk veya Enter: yazı bitmediyse tamamını gösterir, bittiyse sonraki sayfaya geçer.
/// </summary>
public class SubtitleBox : MonoBehaviour
{
    // Sanat pikseli cinsinden ölçüler (1 sanat pikseli = HudArt.Px UI birimi)
    private const float W = 360f;
    private const float PadX = 10f;
    private const float HeaderY = 4f, HeaderH = 8f;
    private const float BodyY = 15f;
    private const float MaxBodyH = 56f;
    private const float BottomPad = 5f;

    [Tooltip("Keşifte kutunun ekranın altından yüksekliği (UI birimi).")]
    public float exploreY = 36f;
    [Tooltip("Savaşta (kartlar ekrandayken) kutunun yüksekliği.")]
    public float combatY = 340f;

    private RectTransform root;
    private CanvasGroup group;
    private Image accent;
    private TMP_Text speakerText;
    private TMP_Text bodyText;
    private TMP_Text hintText;
    private bool advance;

    public bool IsOpen { get; private set; }

    /// <summary>Verilen canvas altında kutuyu kurar. Kök objenin adı "Dialog" (TurnManager, açıkken E ile tur bitirmeyi engeller).</summary>
    public static SubtitleBox Create(Transform canvas)
    {
        GameObject go = new GameObject("Dialog", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(canvas, false);
        SubtitleBox box = go.AddComponent<SubtitleBox>();
        box.Build();
        return box;
    }

    private void Build()
    {
        root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, exploreY);
        root.sizeDelta = new Vector2(W * HudArt.Px, 40f * HudArt.Px);

        Image bg = GetComponent<Image>();
        bg.sprite = HudArt.Panel(HudArt.CyanMid);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;   // haritaya / kartlara tıklama geçsin
        group.interactable = false;
        group.alpha = 0f;

        accent = ArtImage("Accent", HudArt.Solid, (Color)HudArt.Cyan, 8, 1, 22, 1);

        speakerText = ArtText("Speaker", 18, (Color)HudArt.Cyan, TextAlignmentOptions.MidlineLeft,
                              PadX, HeaderY, W - PadX * 2f - 90f, HeaderH);
        speakerText.fontStyle = FontStyles.Bold;

        hintText = ArtText("Hint", 14, (Color)HudArt.Dim, TextAlignmentOptions.MidlineRight,
                           W - PadX - 90f, HeaderY, 90f, HeaderH);
        hintText.text = "TIKLA / BOŞLUK >>";

        bodyText = ArtText("Body", 22, (Color)HudArt.White, TextAlignmentOptions.TopLeft,
                           PadX, BodyY, W - PadX * 2f, 20f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.richText = true;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 14f;
        bodyText.fontSizeMax = 22f;

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Savaşta kartların üstünde, keşifte ekranın en altında dursun.
        bool inCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
        float y = inCombat ? combatY : exploreY;
        Vector2 p = root.anchoredPosition;
        if (!Mathf.Approximately(p.y, y))
            root.anchoredPosition = new Vector2(0f, Mathf.Lerp(p.y, y, Mathf.Clamp01(Time.unscaledDeltaTime * 12f)));

        if (SettingsMenu.IsOpen) return;
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            advance = true;
    }

    /// <summary>
    /// Sayfaları sırayla yazar. waitForInput açıksa her sayfa tıklama/tuş bekler; kapalıysa holdTime sonra
    /// kendiliğinden geçer (yine tıklayarak hızlandırılabilir). onPage her sayfa başında çağrılır.
    /// </summary>
    public IEnumerator Play(string speaker, Color accentColor, IList<string> pages, bool waitForInput,
                            float holdTime, float charDelay, float voicePitch, Action<string> onPage = null)
    {
        DialogState.Push();
        IsOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        bool inCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
        root.anchoredPosition = new Vector2(0f, inCombat ? combatY : exploreY);
        speakerText.text = speaker;
        speakerText.color = accentColor;
        accent.color = accentColor;
        hintText.gameObject.SetActive(false);
        Sfx.Play("dialog_open", 0.5f);

        for (int i = 0; i < pages.Count; i++)
        {
            string page = pages[i];
            onPage?.Invoke(page);
            SetBody(page);
            if (i == 0) yield return Fade(group.alpha, 1f, 0.15f);

            // Daktilo efekti
            int total = bodyText.textInfo.characterCount;
            bodyText.maxVisibleCharacters = 0;
            advance = false;
            float t = 0f;
            int shown = 0;
            while (shown < total)
            {
                if (advance) { advance = false; break; }
                t += Time.unscaledDeltaTime;
                int prev = shown;
                shown = charDelay <= 0f ? total : Mathf.Min(total, Mathf.FloorToInt(t / charDelay));
                bodyText.maxVisibleCharacters = shown;
                if (shown > prev) PlayVoiceFor(bodyText.textInfo, prev, shown, voicePitch);
                yield return null;
            }
            bodyText.maxVisibleCharacters = 99999;

            yield return null;
            advance = false;

            if (waitForInput)
            {
                hintText.text = i < pages.Count - 1 ? "TIKLA / BOŞLUK >>" : "TIKLA / BOŞLUK  [ TAMAM ]";
                hintText.gameObject.SetActive(true);
                while (!advance)
                {
                    // Yanıp sönen "devam" ipucu
                    hintText.alpha = Mathf.Repeat(Time.unscaledTime * 1.6f, 1f) < 0.6f ? 1f : 0.35f;
                    yield return null;
                }
                hintText.gameObject.SetActive(false);
                Sfx.Play("ui_click", 0.45f);
            }
            else
            {
                float until = Time.unscaledTime + holdTime;
                while (Time.unscaledTime < until && !advance) yield return null;
            }
            advance = false;
        }

        yield return Fade(1f, 0f, 0.25f);
        IsOpen = false;
        DialogState.Pop();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Sahne kapanırken/obje yok olurken konuşma yarıda kaldıysa kilidi bırak.
        if (IsOpen)
        {
            IsOpen = false;
            DialogState.Pop();
        }
    }

    private void SetBody(string page)
    {
        float bodyW = (W - PadX * 2f) * HudArt.Px;
        bodyText.fontSize = 22f;
        float h = bodyText.GetPreferredValues(page, bodyW, 0f).y;
        float hArt = Mathf.Clamp(Mathf.Ceil(h / HudArt.Px) + 1f, 10f, MaxBodyH);

        bodyText.rectTransform.sizeDelta = new Vector2(bodyW, hArt * HudArt.Px);
        root.sizeDelta = new Vector2(W * HudArt.Px, (BodyY + hArt + BottomPad) * HudArt.Px);

        bodyText.text = page;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        group.alpha = to;
    }

    private static void PlayVoiceFor(TMP_TextInfo info, int from, int to, float pitch)
    {
        for (int c = from; c < to && c < info.characterCount; c++)
        {
            if (char.IsLetterOrDigit(info.characterInfo[c].character))
            {
                Sfx.Voice(pitch);
                return;
            }
        }
    }

    // ------------------------------------------------------------------ yerleşim yardımcıları

    private RectTransform ArtRect(string name, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * HudArt.Px, -y * HudArt.Px);
        rt.sizeDelta = new Vector2(w * HudArt.Px, h * HudArt.Px);
        return rt;
    }

    private Image ArtImage(string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        Image img = ArtRect(name, x, y, w, h).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text ArtText(string name, float size, Color color, TextAlignmentOptions align,
                             float x, float y, float w, float h)
    {
        RectTransform rt = ArtRect(name, x, y, w, h);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero;
        return t;
    }
}
