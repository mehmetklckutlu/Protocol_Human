using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Bağlantılar")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Renkler")]
    [SerializeField] private Color attackColor = new Color(0.72f, 0.20f, 0.22f);
    [SerializeField] private Color defenseColor = new Color(0.18f, 0.42f, 0.72f);
    [SerializeField] private Color summonColor = new Color(0.24f, 0.13f, 0.06f);
    [SerializeField] private Color utilityColor = new Color(0.48f, 0.26f, 0.68f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f);

    private static readonly Color ScrapOrangeBorder = new Color(0.96f, 0.54f, 0.14f, 1f);
    private static readonly Color ScrapOrangeFrame = new Color(0.55f, 0.28f, 0.06f, 1f);
    private static readonly Color DefaultDarkFrame = new Color(0.06f, 0.08f, 0.12f, 1f);

    [Header("Animasyon Ayarları")]
    [SerializeField] private float hoverLift = 38f;
    [SerializeField] private float hoverScale = 1.18f;
    [SerializeField] private float selectedLift = 44f;
    [SerializeField] private float selectedScale = 1.14f;
    [SerializeField] private float animSpeed = 14f;

    private RectTransform rootRect;
    private RectTransform visualRoot;
    private CanvasGroup visualGroup;
    private Canvas overrideCanvas;
    private Image borderGlowImage;
    private Image darkFrameImage;
    private Image headerBarImage;
    private RectTransform descBoxRect;
    private Image cpuIconImage;
    private GameObject cpuBadgeObj;
    private GameObject scrapBadgeObj;
    private Image scrapIconImage;
    private TMP_Text scrapCostText;

    private bool hovered;

    // ---- yeni kart görseli ----
    private Image artGlowImage, scanImage, holoImage, typeIconImage, starImage;
    private RectTransform descRowsRoot;
    private readonly List<DescRow> descRows = new List<DescRow>();
    private int usedRows;
    private Color32 typeBase;
    private bool cardStrong, cardShort;
    private float scanT, nextFlicker, flickerUntil;
    private int flickerDir = 1;

    private class DescRow
    {
        public RectTransform rt;
        public Image icon;
        public LayoutElement iconLe;
        public TMP_Text text;
    }

    private static readonly Color NumCol = new Color(1f, 0.925f, 0.667f);
    private const string NumHex = "#FFECAA";
    private const string CritHex = "#FFA046";
    private const string ShieldHex = "#8CDCFF";
    private const string CpuHex = "#96FAFF";
    private const string StunHex = "#FFE650";

    /// <summary>Fare şu an üstünde olan kart (HUD'da harcanacak CPU önizlemesi için).</summary>
    public static CardData HoveredCard { get; private set; }
    private static CardView hoveredView;
    private CardData boundCard;
    private bool pressed;
    private bool isSelected;
    private bool isInteractable = true;
    private bool isSummonCard;
    private float baseArcY;
    private float baseArcAngle;
    // Balatro tarzı asimetri: her kart elde biraz farklı açı/yükseklikte durur ve hafifçe salınır.
    private float jitterY, jitterAngle, swayPhase;
    private int slotIndex;
    private RectTransform hoverHitArea;

    private const float FanMaxAngle = 8f;      // yelpazenin uçlarındaki kartların açısı
    private const float FanMaxDrop = 26f;      // uçlardaki kartların ne kadar aşağıda durduğu (kavis)
    private const float SwayAngle = 0.8f;      // boştayken salınım açısı
    private const float SwayLift = 2f;         // boştayken salınım yüksekliği
    private const float NeighborPush = 34f;    // hover'daki kartın komşuları bu kadar yana açılır
    private const float MinHoverLift = 64f;    // hover'da kartın yükselişi (en az)
    private const float MinHoverScale = 1.22f; // hover'da kartın büyümesi (en az)

    private float introTimer;
    private float introDelay;
    private bool builtVisuals;

    private void Awake()
    {
        rootRect = GetComponent<RectTransform>();
        EnsureEnhancedVisuals();
    }

    private void EnsureEnhancedVisuals()
    {
        if (builtVisuals) return;
        builtVisuals = true;

        if (button != null)
            button.transition = Selectable.Transition.None;

        GameObject vrGo = new GameObject("VisualRoot", typeof(RectTransform), typeof(CanvasGroup));
        visualRoot = vrGo.GetComponent<RectTransform>();
        visualGroup = vrGo.GetComponent<CanvasGroup>();
        visualRoot.SetParent(transform, false);
        visualRoot.anchorMin = Vector2.zero;
        visualRoot.anchorMax = Vector2.one;
        visualRoot.offsetMin = Vector2.zero;
        visualRoot.offsetMax = Vector2.zero;
        visualRoot.pivot = new Vector2(0.5f, 0.15f);

        GameObject glowGo = new GameObject("BorderGlow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(visualRoot, false);
        RectTransform glowRt = glowGo.GetComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = new Vector2(-4f, -4f);
        glowRt.offsetMax = new Vector2(4f, 4f);
        borderGlowImage = glowGo.GetComponent<Image>();
        borderGlowImage.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
        borderGlowImage.raycastTarget = false;

        GameObject darkFrameGo = new GameObject("DarkFrame", typeof(RectTransform), typeof(Image));
        darkFrameGo.transform.SetParent(visualRoot, false);
        RectTransform dfRt = darkFrameGo.GetComponent<RectTransform>();
        dfRt.anchorMin = Vector2.zero;
        dfRt.anchorMax = Vector2.one;
        dfRt.offsetMin = new Vector2(-2f, -2f);
        dfRt.offsetMax = new Vector2(2f, 2f);
        darkFrameImage = darkFrameGo.GetComponent<Image>();
        darkFrameImage.color = DefaultDarkFrame;
        darkFrameImage.raycastTarget = false;

        GameObject bodyGo = new GameObject("CardBody", typeof(RectTransform), typeof(Image));
        bodyGo.transform.SetParent(visualRoot, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(3f, 3f);
        bodyRt.offsetMax = new Vector2(-3f, -3f);
        Image newBodyImg = bodyGo.GetComponent<Image>();
        newBodyImg.raycastTarget = false;

        if (background != null)
        {
            background.color = new Color(1f, 1f, 1f, 0.002f);
            background = newBodyImg;
        }

        GameObject headerGo = new GameObject("HeaderBand", typeof(RectTransform), typeof(Image));
        headerGo.transform.SetParent(visualRoot, false);
        RectTransform hdrRt = headerGo.GetComponent<RectTransform>();
        hdrRt.anchorMin = new Vector2(0.03f, 0.80f);
        hdrRt.anchorMax = new Vector2(0.97f, 0.97f);
        hdrRt.offsetMin = Vector2.zero;
        hdrRt.offsetMax = Vector2.zero;
        headerBarImage = headerGo.GetComponent<Image>();
        headerBarImage.color = new Color(0.05f, 0.07f, 0.11f, 0.72f);
        headerBarImage.raycastTarget = false;

        GameObject descBgGo = new GameObject("DescBox", typeof(RectTransform), typeof(Image));
        descBgGo.transform.SetParent(visualRoot, false);
        descBoxRect = descBgGo.GetComponent<RectTransform>();
        descBoxRect.anchorMin = new Vector2(0.05f, 0.03f);
        descBoxRect.anchorMax = new Vector2(0.95f, 0.36f);
        descBoxRect.offsetMin = Vector2.zero;
        descBoxRect.offsetMax = Vector2.zero;
        Image dbImg = descBgGo.GetComponent<Image>();
        dbImg.color = new Color(0.04f, 0.06f, 0.09f, 0.68f);
        dbImg.raycastTarget = false;

        if (iconImage != null) iconImage.transform.SetParent(visualRoot, false);
        if (nameText != null) { nameText.transform.SetParent(visualRoot, false); RuntimeArt.ApplyPixelFont(nameText); }
        if (valueText != null) { valueText.transform.SetParent(visualRoot, false); RuntimeArt.ApplyPixelFont(valueText); }
        if (descriptionText != null) { descriptionText.transform.SetParent(visualRoot, false); RuntimeArt.ApplyPixelFont(descriptionText); }
        if (costText != null) { costText.transform.SetParent(visualRoot, false); RuntimeArt.ApplyPixelFont(costText); }

        if (iconImage != null)
        {
            iconImage.preserveAspect = true;
            RectTransform ir = iconImage.rectTransform;
            ir.anchorMin = new Vector2(0.24f, 0.54f);
            ir.anchorMax = new Vector2(0.76f, 0.78f);
            ir.offsetMin = Vector2.zero;
            ir.offsetMax = Vector2.zero;
        }

        if (nameText != null)
        {
            RectTransform nr = nameText.rectTransform;
            nr.anchorMin = new Vector2(0.24f, 0.80f);
            nr.anchorMax = new Vector2(0.95f, 0.97f);
            nr.offsetMin = Vector2.zero;
            nr.offsetMax = Vector2.zero;
            nameText.fontSize = 14f;
            nameText.fontStyle = FontStyles.Bold;
        }

        if (valueText != null)
        {
            RectTransform vr = valueText.rectTransform;
            vr.anchorMin = new Vector2(0.15f, 0.36f);
            vr.anchorMax = new Vector2(0.85f, 0.56f);
            vr.offsetMin = Vector2.zero;
            vr.offsetMax = Vector2.zero;
            valueText.fontSize = 32f;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 16f;
            valueText.fontSizeMax = 32f;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.fontStyle = FontStyles.Bold;
        }

        if (descriptionText != null)
        {
            RectTransform dr = descriptionText.rectTransform;
            dr.anchorMin = new Vector2(0.08f, 0.04f);
            dr.anchorMax = new Vector2(0.92f, 0.35f);
            dr.offsetMin = Vector2.zero;
            dr.offsetMax = Vector2.zero;
            descriptionText.fontSize = 12.5f;
        }

        cpuBadgeObj = new GameObject("CpuBadge", typeof(RectTransform), typeof(Image));
        cpuBadgeObj.transform.SetParent(visualRoot, false);
        RectTransform cbr = cpuBadgeObj.GetComponent<RectTransform>();
        cbr.anchorMin = new Vector2(0f, 1f);
        cbr.anchorMax = new Vector2(0f, 1f);
        cbr.pivot = new Vector2(0f, 1f);
        cbr.anchoredPosition = new Vector2(-4f, 4f);
        cbr.sizeDelta = new Vector2(50f, 28f);
        Image cbBg = cpuBadgeObj.GetComponent<Image>();
        cbBg.color = new Color(0.05f, 0.14f, 0.20f, 0.96f);
        cbBg.raycastTarget = false;

        GameObject cpuIconGo = new GameObject("CpuIcon", typeof(RectTransform), typeof(Image));
        cpuIconGo.transform.SetParent(cpuBadgeObj.transform, false);
        RectTransform cir = cpuIconGo.GetComponent<RectTransform>();
        cir.anchorMin = new Vector2(0f, 0.5f);
        cir.anchorMax = new Vector2(0f, 0.5f);
        cir.pivot = new Vector2(0f, 0.5f);
        cir.anchoredPosition = new Vector2(3f, 0f);
        cir.sizeDelta = new Vector2(22f, 22f);
        cpuIconImage = cpuIconGo.GetComponent<Image>();
        cpuIconImage.sprite = RuntimeArt.CpuIcon;
        cpuIconImage.preserveAspect = true;
        cpuIconImage.raycastTarget = false;

        if (costText != null)
        {
            costText.transform.SetParent(cpuBadgeObj.transform, false);
            RectTransform ctr = costText.rectTransform;
            ctr.anchorMin = new Vector2(0f, 0f);
            ctr.anchorMax = new Vector2(1f, 1f);
            ctr.offsetMin = new Vector2(24f, 0f);
            ctr.offsetMax = new Vector2(-2f, 0f);
            costText.fontSize = 18f;
            costText.fontStyle = FontStyles.Bold;
            costText.alignment = TextAlignmentOptions.Center;
        }

        scrapBadgeObj = new GameObject("ScrapBadge", typeof(RectTransform), typeof(Image));
        scrapBadgeObj.transform.SetParent(visualRoot, false);
        RectTransform sbr = scrapBadgeObj.GetComponent<RectTransform>();
        sbr.anchorMin = new Vector2(1f, 1f);
        sbr.anchorMax = new Vector2(1f, 1f);
        sbr.pivot = new Vector2(1f, 1f);
        sbr.anchoredPosition = new Vector2(4f, -34f);
        sbr.sizeDelta = new Vector2(54f, 26f);
        Image sbBg = scrapBadgeObj.GetComponent<Image>();
        sbBg.color = new Color(0.18f, 0.11f, 0.06f, 0.96f);
        sbBg.raycastTarget = false;

        GameObject scrapIconGo = new GameObject("ScrapIcon", typeof(RectTransform), typeof(Image));
        scrapIconGo.transform.SetParent(scrapBadgeObj.transform, false);
        RectTransform sir = scrapIconGo.GetComponent<RectTransform>();
        sir.anchorMin = new Vector2(0f, 0.5f);
        sir.anchorMax = new Vector2(0f, 0.5f);
        sir.pivot = new Vector2(0f, 0.5f);
        sir.anchoredPosition = new Vector2(3f, 0f);
        sir.sizeDelta = new Vector2(20f, 20f);
        scrapIconImage = scrapIconGo.GetComponent<Image>();
        scrapIconImage.sprite = RuntimeArt.ScrapIcon;
        scrapIconImage.preserveAspect = true;
        scrapIconImage.raycastTarget = false;

        GameObject stGo = new GameObject("ScrapCostText", typeof(RectTransform), typeof(TextMeshProUGUI));
        stGo.transform.SetParent(scrapBadgeObj.transform, false);
        RectTransform str = stGo.GetComponent<RectTransform>();
        str.anchorMin = Vector2.zero;
        str.anchorMax = Vector2.one;
        str.offsetMin = new Vector2(22f, 0f);
        str.offsetMax = new Vector2(-2f, 0f);
        scrapCostText = stGo.GetComponent<TextMeshProUGUI>();
        if (costText != null) scrapCostText.font = costText.font;
        RuntimeArt.ApplyPixelFont(scrapCostText);
        scrapCostText.fontSize = 15f;
        scrapCostText.fontStyle = FontStyles.Bold;
        scrapCostText.alignment = TextAlignmentOptions.Center;
        scrapCostText.color = new Color(1f, 0.82f, 0.45f);
        scrapCostText.raycastTarget = false;

        BuildNewVisuals();

        overrideCanvas = visualRoot.gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = false;

        // Kart yukarı kalkınca fare kartın üst kısmına çıktığında hover kopmasın diye
        // sadece hover sırasında yukarı doğru uzayan görünmez tıklama alanı.
        GameObject hitGo = new GameObject("HoverHitArea", typeof(RectTransform), typeof(Image));
        hitGo.transform.SetParent(transform, false);
        hoverHitArea = hitGo.GetComponent<RectTransform>();
        hoverHitArea.anchorMin = Vector2.zero;
        hoverHitArea.anchorMax = Vector2.one;
        hoverHitArea.offsetMin = Vector2.zero;
        hoverHitArea.offsetMax = Vector2.zero;
        Image hitImg = hitGo.GetComponent<Image>();
        hitImg.color = new Color(0f, 0f, 0f, 0f);
        hitImg.raycastTarget = true;
        hitGo.SetActive(false);
    }

    /// <summary>Yeni kart görseli için ek katmanlar: ikon ışıması, tarama çizgisi, hologram, tip ikonu, açıklama satırları.</summary>
    private void BuildNewVisuals()
    {
        // Eski düz kutular yerine tek parça üretilmiş gövde kullanılıyor
        if (headerBarImage != null) headerBarImage.enabled = false;
        if (descBoxRect != null) { Image db = descBoxRect.GetComponent<Image>(); if (db != null) db.enabled = false; }
        if (darkFrameImage != null) darkFrameImage.enabled = false;
        if (borderGlowImage != null) borderGlowImage.color = new Color(1f, 1f, 1f, 0f);

        int iconIndex = iconImage != null ? iconImage.transform.GetSiblingIndex() : visualRoot.childCount;

        artGlowImage = NewImage("ArtGlow", CardArt.Glow);
        CardArt.Anchor(artGlowImage.rectTransform, CardArt.ArtWin, 1f);
        artGlowImage.color = new Color(1f, 1f, 1f, 0f);
        artGlowImage.transform.SetSiblingIndex(iconIndex);

        int after = iconImage != null ? iconImage.transform.GetSiblingIndex() + 1 : visualRoot.childCount;
        holoImage = NewImage("Holo", CardArt.HoloLines);
        CardArt.Anchor(holoImage.rectTransform, CardArt.ArtWin, 1f);
        holoImage.transform.SetSiblingIndex(after);
        holoImage.gameObject.SetActive(false);

        scanImage = NewImage("Scan", HudArt.Solid);
        scanImage.color = new Color(1f, 0.94f, 0.7f, 0.85f);
        scanImage.transform.SetSiblingIndex(after + 1);
        scanImage.gameObject.SetActive(false);

        typeIconImage = NewImage("TypeIcon", null);
        typeIconImage.preserveAspect = true;
        CardArt.Anchor(typeIconImage.rectTransform, new RectInt(4, 55, 7, 7));

        starImage = NewImage("Star", HudArt.GoldStarIcon);
        starImage.preserveAspect = true;
        CardArt.Anchor(starImage.rectTransform, new RectInt(37, 4, 7, 7));
        starImage.gameObject.SetActive(false);

        GameObject rows = new GameObject("DescRows", typeof(RectTransform));
        rows.transform.SetParent(visualRoot, false);
        descRowsRoot = (RectTransform)rows.transform;
        CardArt.Anchor(descRowsRoot, CardArt.Stats, 1.5f);
        if (descriptionText != null) descriptionText.gameObject.SetActive(false);
    }

    private Image NewImage(string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(visualRoot, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private DescRow GetRow(int i)
    {
        while (descRows.Count <= i)
        {
            DescRow r = new DescRow();
            GameObject go = new GameObject("Row" + descRows.Count, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(descRowsRoot, false);
            r.rt = (RectTransform)go.transform;
            HorizontalLayoutGroup h = go.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 2f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            GameObject ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            ig.transform.SetParent(r.rt, false);
            r.icon = ig.GetComponent<Image>();
            r.icon.preserveAspect = true;
            r.icon.raycastTarget = false;
            r.iconLe = ig.GetComponent<LayoutElement>();

            GameObject tg = new GameObject("Text", typeof(RectTransform));
            tg.transform.SetParent(r.rt, false);
            TextMeshProUGUI t = tg.AddComponent<TextMeshProUGUI>();
            if (descriptionText != null) t.font = descriptionText.font;
            RuntimeArt.ApplyPixelFont(t);
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.richText = true;
            t.raycastTarget = false;
            r.text = t;
            descRows.Add(r);
        }
        return descRows[i];
    }

    /// <summary>Açıklamayı satır satır, renkli sayılar ve ikonlarla yazar.</summary>
    private void FillDescription(CardData card, bool dim)
    {
        List<(Sprite icon, string text)> lines = BuildLines(card);
        usedRows = lines.Count;
        float n = Mathf.Max(1, lines.Count);
        // Kartın kendi boyutundan hesapla (ilk kurulumda layout henüz oluşmamış olabilir)
        Rect cardRect = rootRect != null ? rootRect.rect : new Rect(0, 0, 130, 175);
        float maxW = cardRect.width * (CardArt.Stats.width - 3f) / CardArt.W;
        float rowH = cardRect.height * (CardArt.Stats.height - 3f) / CardArt.H / n;

        for (int i = 0; i < descRows.Count || i < lines.Count; i++)
        {
            if (i >= lines.Count) { descRows[i].rt.gameObject.SetActive(false); continue; }
            DescRow r = GetRow(i);
            r.rt.gameObject.SetActive(true);
            r.rt.anchorMin = new Vector2(0f, 1f - (i + 1) / n);
            r.rt.anchorMax = new Vector2(1f, 1f - i / n);
            r.rt.offsetMin = r.rt.offsetMax = Vector2.zero;

            float size = Mathf.Min(i == 0 ? 14f : 12f, rowH * 0.9f);
            string txt = lines[i].text;
            bool hasIcon = lines[i].icon != null;
            // sığdır
            for (int guard = 0; guard < 12; guard++)
            {
                r.text.fontSize = size;
                float w = r.text.GetPreferredValues(txt).x + (hasIcon ? size + 2f : 0f);
                if (w <= maxW - 2f || size <= 7f) break;
                size -= 0.5f;
            }
            r.text.text = txt;
            r.text.color = dim ? new Color(0.62f, 0.64f, 0.66f) : (i == 0 ? Color.white : new Color(0.87f, 0.9f, 0.92f));
            r.icon.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                r.icon.sprite = lines[i].icon;
                r.icon.color = dim ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : Color.white;
                r.iconLe.preferredWidth = r.iconLe.preferredHeight = size * 0.85f;
            }
        }
    }

    private static string N(string v, string hex) => "<color=" + hex + ">" + v + "</color>";

    private static List<(Sprite, string)> BuildLines(CardData c)
    {
        var L = new List<(Sprite, string)>();
        if (!string.IsNullOrEmpty(c.description))
        {
            foreach (string part in c.description.Split('\n')) L.Add((null, part));
            return L;
        }
        switch (c.cardType)
        {
            case CardType.Attack:
                L.Add((null, N(c.damage.ToString(), NumHex) + " Hasar"));
                L.Add((HudArt.RangedIcon, "Menzil " + N(c.range.ToString(), NumHex)));
                if (c.hitChance < 1f) L.Add((null, N("%" + Mathf.RoundToInt(c.hitChance * 100), NumHex) + " İsabet"));
                if (c.stunChance > 0f) L.Add((HudArt.StunIcon, N("%" + Mathf.RoundToInt(c.stunChance * 100) + " Stun", StunHex) + " (" + c.stunTurns + " Tur)"));
                if (c.critChance > 0f) L.Add((HudArt.CritIcon, N("%" + Mathf.RoundToInt(c.critChance * 100) + " Kritik", CritHex)));
                break;
            case CardType.Defense:
                L.Add((null, N(c.shield.ToString(), ShieldHex) + " Kalkan"));
                if (c.tauntTurns > 0) { L.Add((HudArt.TauntIcon, N(c.tauntTurns.ToString(), NumHex) + " Tur Taunt")); L.Add((null, "Hedef çeker")); }
                else { L.Add((null, "Hasarı canından")); L.Add((null, "önce emer")); }
                break;
            case CardType.Utility:
                if (c.selfDestruct)
                {
                    L.Add((null, N(c.blastDamage.ToString(), NumHex) + " Hasar"));
                    L.Add((null, "Kendini imha eder"));
                    L.Add((null, "Alan " + N(c.blastRadius.ToString(), NumHex)));
                }
                else if (c.cpuRestore > 0) { L.Add((null, N("+" + c.cpuRestore, CpuHex) + " CPU")); L.Add((null, "Ana drone'a")); }
                else if (c.chargeAll > 0) { L.Add((null, N("+" + c.chargeAll, NumHex) + " Tur Şarj")); L.Add((null, "Dost dronelara")); }
                break;
            case CardType.Summon:
                L.Add((null, "Dost drone çağırır"));
                if (c.summonPrefab != null)
                {
                    Unit u = c.summonPrefab.GetComponent<Unit>();
                    DroneUnit d = c.summonPrefab.GetComponent<DroneUnit>();
                    if (u != null && d != null)
                        L.Add((null, N(u.maxHealth.ToString(), NumHex) + " Can · " + N(d.charge.ToString(), NumHex) + " Tur Şarj"));
                }
                break;
        }
        return L;
    }

    public void SetHandSlot(int index, int totalCount, bool playIntro)
    {
        EnsureEnhancedVisuals();

        slotIndex = index;
        if (totalCount > 1)
        {
            // Slay the Spire tarzı yelpaze: kartlar kavis üzerinde dizilir, uçlara doğru eğilip alçalır.
            // Az kartta yelpaze daha dar açılır.
            float norm = (index - (totalCount - 1) * 0.5f) / Mathf.Max(1f, (totalCount - 1) * 0.5f);
            float spread = Mathf.Clamp01((totalCount - 1) / 6f);
            baseArcY = -norm * norm * FanMaxDrop * spread;
            baseArcAngle = -norm * FanMaxAngle * spread;
        }
        else
        {
            baseArcY = 0f;
            baseArcAngle = 0f;
        }

        // Kart başına sabit küçük sapma (her yeniden çizimde aynı kalsın diye sıra numarasından üretilir).
        System.Random rng = new System.Random(index * 7919 + totalCount * 131 + 17);
        jitterAngle = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.6f;
        jitterY = (float)(rng.NextDouble() * 2.0 - 1.0) * 4f;
        swayPhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

        if (playIntro)
        {
            introDelay = index * 0.04f;
            introTimer = 0f;
            visualRoot.anchoredPosition = new Vector2(0f, -90f);
            visualRoot.localScale = Vector3.one * 0.65f;
        }
        else
        {
            introDelay = 0f;
            introTimer = 1f;
        }
    }

    private void ApplyCardLayout(bool summon)
    {
        if (nameText != null)
        {
            CardArt.Anchor(nameText.rectTransform, new RectInt(4, 3, 40, 9));
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 7f;
            nameText.fontSizeMax = 15f;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.margin = Vector4.zero;
        }

        if (iconImage != null) CardArt.Anchor(iconImage.rectTransform, CardArt.ArtWin, 3f);
        if (valueText != null) valueText.gameObject.SetActive(false);

        if (cpuBadgeObj != null)
        {
            CardArt.Anchor((RectTransform)cpuBadgeObj.transform, CardArt.CpuBadge);
            LayoutBadge(cpuIconImage, costText);
        }
        if (scrapBadgeObj != null)
        {
            CardArt.Anchor((RectTransform)scrapBadgeObj.transform, CardArt.LeftBadge);
            LayoutBadge(scrapIconImage, scrapCostText);
        }
    }

    /// <summary>Rozet içi: solda ikon (kare), sağda sayı. Rozet 14x9 sanat pikseli.</summary>
    private static void LayoutBadge(Image icon, TMP_Text text)
    {
        if (icon != null)
        {
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = new Vector2(1.2f / 14f, 1.2f / 9f);
            ir.anchorMax = new Vector2(7.8f / 14f, 7.8f / 9f);
            ir.offsetMin = ir.offsetMax = Vector2.zero;
            ir.pivot = new Vector2(0.5f, 0.5f);
            icon.preserveAspect = true;
        }
        if (text != null)
        {
            RectTransform tr = text.rectTransform;
            tr.anchorMin = new Vector2(7.6f / 14f, 0f);
            tr.anchorMax = new Vector2(13f / 14f, 1f);
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 20f;
            text.margin = Vector4.zero;
        }
    }

    public void Setup(CardData card, bool interactable, bool selected, int handIndex = -1)
    {
        boundCard = card;
        if (hoveredView == this) HoveredCard = card;
        EnsureEnhancedVisuals();

        isInteractable = interactable;
        isSelected = selected;
        isSummonCard = card != null && card.cardType == CardType.Summon;

        ApplyCardLayout(isSummonCard);

        if (nameText != null) nameText.text = card.cardName;
        if (valueText != null)
        {
            valueText.text = card.MainValueText;
        }

        Unit activeUnit = CardManager.Instance != null ? CardManager.Instance.GetActiveUnit() : null;
        bool usesCpu = activeUnit == null || activeUnit.usesCpu;

        if (cpuBadgeObj != null)
            cpuBadgeObj.SetActive(usesCpu);

        bool enoughCpu = activeUnit == null || !activeUnit.usesCpu || activeUnit.currentCPU >= card.cpuCost;
        bool enoughScrapAll = !(usesCpu && card.scrapCost > 0) || ScrapInventory.Count >= card.scrapCost;
        if (costText != null)
        {
            costText.text = card.cpuCost.ToString();
            costText.color = enoughCpu ? new Color(0.59f, 0.98f, 1f) : new Color(1f, 0.43f, 0.39f);
        }

        if (scrapBadgeObj != null)
        {
            bool hasScrap = usesCpu && card.scrapCost > 0;
            scrapBadgeObj.SetActive(hasScrap);
            if (hasScrap && scrapCostText != null)
            {
                scrapCostText.text = card.scrapCost.ToString();
                bool enoughScrap = ScrapInventory.Count >= card.scrapCost;
                scrapCostText.color = enoughScrap ? new Color(1f, 0.84f, 0.45f) : new Color(1f, 0.35f, 0.35f);
            }
        }

        if (descriptionText != null) descriptionText.text = card.GetDescription();

        if (iconImage != null)
        {
            Sprite sp = null;
            if (isSummonCard)
            {
                if (card.summonPrefab != null)
                {
                    SpriteRenderer sr = card.summonPrefab.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && sr.sprite != null) sp = sr.sprite;
                }
                if (sp == null) sp = card.icon;
            }
            else
            {
                sp = RuntimeArt.GetCardArt(card);
                if (sp == null) sp = card.icon;
            }
            iconImage.sprite = sp;
            iconImage.enabled = sp != null;
        }

        // ---- yeni kart görseli ----
        Color baseCol;
        switch (card.cardType)
        {
            case CardType.Attack: baseCol = attackColor; break;
            case CardType.Defense: baseCol = defenseColor; break;
            case CardType.Summon: baseCol = (Color)CardArt.SummonBase; break;
            default: baseCol = utilityColor; break;
        }
        typeBase = baseCol;
        cardStrong = card.strongCard || (card.cardType == CardType.Attack && card.cpuCost >= 2);
        cardShort = !enoughCpu || !enoughScrapAll;

        if (background != null)
        {
            background.sprite = CardArt.Body(typeBase, isSummonCard, cardStrong, cardShort);
            background.type = Image.Type.Simple;
            background.color = Color.white;
        }
        if (cpuBadgeObj != null)
        {
            Image bg = cpuBadgeObj.GetComponent<Image>();
            if (bg != null) { bg.sprite = CardArt.Badge(typeBase, isSummonCard, !enoughCpu); bg.color = Color.white; }
        }
        if (scrapBadgeObj != null)
        {
            Image bg = scrapBadgeObj.GetComponent<Image>();
            if (bg != null) { bg.sprite = CardArt.Badge(typeBase, isSummonCard, ScrapInventory.Count < card.scrapCost); bg.color = Color.white; }
        }

        if (nameText != null)
        {
            CardArt.Anchor(nameText.rectTransform, cardStrong ? new RectInt(4, 3, 33, 9) : new RectInt(4, 3, 40, 9));
            nameText.color = cardShort ? new Color(0.62f, 0.64f, 0.66f) : (cardStrong ? new Color(1f, 0.925f, 0.667f) : new Color(0.96f, 0.98f, 0.99f));
        }
        if (starImage != null)
        {
            starImage.gameObject.SetActive(cardStrong);
            starImage.color = cardShort ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
        }

        if (typeIconImage != null)
        {
            Sprite ti = null;
            switch (card.cardType)
            {
                case CardType.Attack: ti = HudArt.CardSwordIcon; break;
                case CardType.Defense: ti = HudArt.CardShieldIcon; break;
                case CardType.Utility: ti = RuntimeArt.CpuIcon; break;
            }
            // Blueprint'te sol altta hurda rozeti var; hurda bedeli yoksa da ikon gösterme
            typeIconImage.gameObject.SetActive(ti != null && !isSummonCard);
            typeIconImage.sprite = ti;
            typeIconImage.color = cardShort ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : Color.white;
        }

        if (iconImage != null) iconImage.color = cardShort ? new Color(0.55f, 0.57f, 0.6f, 0.85f) : Color.white;
        if (holoImage != null) holoImage.gameObject.SetActive(isSummonCard);
        if (artGlowImage != null)
        {
            Color g = isSummonCard ? (Color)CardArt.Orange : (Color)CardArt.Lt(typeBase, 0.45f);
            g.a = artGlowImage.color.a;
            artGlowImage.color = g;
        }

        FillDescription(card, cardShort);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => CardManager.Instance.OnCardClicked(card, handIndex));
            button.interactable = interactable;
        }

        UpdateSortingOrder();
    }

    private void Update()
    {
        if (visualRoot == null) return;

        float dt = Time.unscaledDeltaTime;
        if (introDelay > 0f)
        {
            introDelay -= dt;
            return;
        }
        introTimer = Mathf.Min(1f, introTimer + dt * 5f);

        float sway = Mathf.Sin(Time.unscaledTime * 1.3f + swayPhase);
        float targetX = 0f;
        float targetY = baseArcY + jitterY + sway * SwayLift;
        float targetScale = 1f;
        float targetAngle = baseArcAngle + jitterAngle + sway * SwayAngle;

        // Elde başka bir kartın üstüne gelinince komşular yana açılır (yakın olan daha çok).
        if (!hovered && !isSelected && hoveredView != null && hoveredView != this &&
            hoveredView.transform.parent == transform.parent)
        {
            int d = slotIndex - hoveredView.slotIndex;
            if (d != 0) targetX = Mathf.Sign(d) * NeighborPush / Mathf.Abs(d);
        }
        Color targetBorder = new Color(0.03f, 0.05f, 0.08f, 0.55f);

        if (!isInteractable && !isSelected)
        {
            targetY -= 6f;
            targetScale = 0.95f;
        }

        if (isSelected)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 5.5f);
            targetY = selectedLift + pulse * 4f;
            targetScale = selectedScale;
            targetAngle = 0f;
            targetBorder = Color.Lerp((Color)CardArt.Gold, new Color(1f, 0.97f, 0.75f, 1f), 0.5f + 0.5f * pulse);
        }
        else if (hovered)
        {
            // Üstüne gelinen kart dikleşir, yukarı çıkar ve hafifçe büyür.
            float lift = Mathf.Max(hoverLift, MinHoverLift);
            float scale = Mathf.Max(hoverScale, MinHoverScale);
            targetY = lift;
            targetScale = pressed ? scale * 0.94f : scale;
            targetAngle = 0f;
            targetBorder = (isInteractable && !cardShort)
                ? (isSummonCard ? (Color)CardArt.Orange : (Color)CardArt.Lt(typeBase, 0.6f))
                : new Color(0.95f, 0.35f, 0.35f, 0.9f);
        }

        visualRoot.anchoredPosition = Vector2.Lerp(visualRoot.anchoredPosition, new Vector2(targetX, targetY), dt * animSpeed);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, Vector3.one * targetScale, dt * animSpeed);
        visualRoot.localRotation = Quaternion.Lerp(visualRoot.localRotation, Quaternion.Euler(0f, 0f, targetAngle), dt * animSpeed);

        if (borderGlowImage != null)
            borderGlowImage.color = Color.Lerp(borderGlowImage.color, targetBorder, dt * animSpeed);

        UpdateCardEffects(dt);

        if (visualGroup != null)
        {
            float targetAlpha = (isInteractable || hovered || isSelected) ? 1f : 0.65f;
            visualGroup.alpha = Mathf.Lerp(visualGroup.alpha, targetAlpha, dt * animSpeed);
        }
    }

    /// <summary>Hover/seçili ışıması, seçili tarama çizgisi, blueprint hologram titreşimi.</summary>
    private void UpdateCardEffects(float dt)
    {
        float pulseT = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);

        if (artGlowImage != null)
        {
            Color g = artGlowImage.color;
            float ta = (hovered || isSelected) && !cardShort ? 0.22f + 0.22f * pulseT : 0f;
            g.a = Mathf.Lerp(g.a, ta, dt * animSpeed);
            artGlowImage.color = g;
        }

        if (scanImage != null)
        {
            bool on = isSelected;
            if (scanImage.gameObject.activeSelf != on) scanImage.gameObject.SetActive(on);
            if (on)
            {
                scanT = Mathf.Repeat(scanT + dt * 0.9f, 1f);
                RectInt a = CardArt.ArtWin;
                float y = Mathf.Lerp(a.y + 1.5f, a.yMax - 2.5f, scanT);
                RectTransform rt = scanImage.rectTransform;
                rt.anchorMin = new Vector2((a.x + 1f) / CardArt.W, 1f - (y + 0.7f) / CardArt.H);
                rt.anchorMax = new Vector2((a.xMax - 1f) / CardArt.W, 1f - y / CardArt.H);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else scanT = 0f;
        }

        if (isSummonCard && iconImage != null)
        {
            float now = Time.unscaledTime;
            if (now >= nextFlicker)
            {
                flickerUntil = now + Random.Range(0.06f, 0.14f);
                nextFlicker = now + Random.Range(1.8f, 4.5f);
                flickerDir = Random.value < 0.5f ? -1 : 1;
            }
            bool fl = now < flickerUntil;
            iconImage.rectTransform.anchoredPosition = new Vector2(fl ? flickerDir * 2.5f : 0f, 0f);
            Color ic = iconImage.color;
            ic.a = fl ? 0.5f : (cardShort ? 0.85f : 1f);
            iconImage.color = ic;
        }
    }

    private void UpdateSortingOrder()
    {
        if (hoverHitArea != null)
        {
            bool extend = hovered || isSelected;
            hoverHitArea.gameObject.SetActive(extend);
            if (extend) hoverHitArea.offsetMax = new Vector2(0f, Mathf.Max(hoverLift, MinHoverLift) + 20f);
        }
        if (overrideCanvas == null) return;
        bool bringToFront = hovered || isSelected;
        overrideCanvas.overrideSorting = bringToFront;
        if (bringToFront)
            overrideCanvas.sortingOrder = isSelected ? 110 : 120;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        hoveredView = this;
        HoveredCard = boundCard;
        Sfx.Play("ui_hover", 0.3f, 0.06f, 0.06f);
        UpdateSortingOrder();
    }

    private void OnDisable()
    {
        ClearHoverPreview();
    }

    private void ClearHoverPreview()
    {
        if (hoveredView != this) return;
        hoveredView = null;
        HoveredCard = null;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        ClearHoverPreview();
        pressed = false;
        UpdateSortingOrder();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }
}
