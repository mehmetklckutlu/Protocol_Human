using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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
    [SerializeField] private Color summonColor = new Color(0.18f, 0.58f, 0.34f);
    [SerializeField] private Color utilityColor = new Color(0.48f, 0.26f, 0.68f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f);

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
    private Image headerBarImage;
    private Image cpuIconImage;
    private GameObject cpuBadgeObj;
    private GameObject scrapBadgeObj;
    private Image scrapIconImage;
    private TMP_Text scrapCostText;

    private bool hovered;
    private bool pressed;
    private bool isSelected;
    private bool isInteractable = true;
    private float baseArcY;
    private float baseArcAngle;
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
        dfRt.offsetMin = new Vector2(-1f, -1f);
        dfRt.offsetMax = new Vector2(1f, 1f);
        Image dfImg = darkFrameGo.GetComponent<Image>();
        dfImg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
        dfImg.raycastTarget = false;

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
        RectTransform dbRt = descBgGo.GetComponent<RectTransform>();
        dbRt.anchorMin = new Vector2(0.05f, 0.03f);
        dbRt.anchorMax = new Vector2(0.95f, 0.36f);
        dbRt.offsetMin = Vector2.zero;
        dbRt.offsetMax = Vector2.zero;
        Image dbImg = descBgGo.GetComponent<Image>();
        dbImg.color = new Color(0.04f, 0.06f, 0.09f, 0.68f);
        dbImg.raycastTarget = false;

        if (iconImage != null) iconImage.transform.SetParent(visualRoot, false);
        if (nameText != null) nameText.transform.SetParent(visualRoot, false);
        if (valueText != null) valueText.transform.SetParent(visualRoot, false);
        if (descriptionText != null) descriptionText.transform.SetParent(visualRoot, false);
        if (costText != null) costText.transform.SetParent(visualRoot, false);

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
        scrapCostText.fontSize = 15f;
        scrapCostText.fontStyle = FontStyles.Bold;
        scrapCostText.alignment = TextAlignmentOptions.Center;
        scrapCostText.color = new Color(1f, 0.82f, 0.45f);
        scrapCostText.raycastTarget = false;

        overrideCanvas = visualRoot.gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = false;
    }

    public void SetHandSlot(int index, int totalCount, bool playIntro)
    {
        EnsureEnhancedVisuals();

        if (totalCount > 1)
        {
            float norm = (index - (totalCount - 1) * 0.5f) / Mathf.Max(1f, (totalCount - 1) * 0.5f);
            baseArcY = -Mathf.Abs(norm) * 8f;
            baseArcAngle = -norm * 3.5f;
        }
        else
        {
            baseArcY = 0f;
            baseArcAngle = 0f;
        }

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

    public void Setup(CardData card, bool interactable, bool selected)
    {
        EnsureEnhancedVisuals();

        isInteractable = interactable;
        isSelected = selected;

        if (nameText != null) nameText.text = card.cardName;
        if (valueText != null)
        {
            int mv = card.MainValue;
            valueText.text = mv > 0 ? mv.ToString() : "";
        }

        Unit activeUnit = CardManager.Instance != null ? CardManager.Instance.GetActiveUnit() : null;
        bool usesCpu = activeUnit == null || activeUnit.usesCpu;

        if (cpuBadgeObj != null)
            cpuBadgeObj.SetActive(usesCpu);

        if (costText != null)
        {
            costText.text = card.cpuCost.ToString();
            bool enoughCpu = activeUnit == null || !activeUnit.usesCpu || activeUnit.currentCPU >= card.cpuCost;
            costText.color = enoughCpu ? new Color(0.55f, 0.98f, 1f) : new Color(1f, 0.35f, 0.35f);
        }

        if (scrapBadgeObj != null)
        {
            bool hasScrap = card.scrapCost > 0;
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
            Sprite sp = card.icon;
            if (sp == null)
            {
                if (card.cpuRestore > 0 || card.chargeAll > 0) sp = RuntimeArt.CpuIcon;
                else if (card.cardType == CardType.Summon || card.scrapCost > 0) sp = RuntimeArt.ScrapIcon;
            }
            iconImage.sprite = sp;
            iconImage.enabled = sp != null;
        }

        if (background != null)
        {
            Color typeColor;
            switch (card.cardType)
            {
                case CardType.Attack: typeColor = attackColor; break;
                case CardType.Defense: typeColor = defenseColor; break;
                case CardType.Summon: typeColor = summonColor; break;
                default: typeColor = utilityColor; break;
            }
            background.color = selected ? Color.Lerp(typeColor, selectedColor, 0.35f) : typeColor;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => CardManager.Instance.OnCardClicked(card));
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

        float targetY = baseArcY;
        float targetScale = 1f;
        float targetAngle = baseArcAngle;
        Color targetBorder = new Color(0.14f, 0.22f, 0.32f, 0.95f);

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
            targetBorder = Color.Lerp(selectedColor, new Color(0.3f, 1f, 1f, 1f), 0.5f + 0.5f * pulse);
        }
        else if (hovered)
        {
            targetY = hoverLift;
            targetScale = pressed ? hoverScale * 0.94f : hoverScale;
            targetAngle = 0f;
            targetBorder = isInteractable
                ? new Color(0.2f, 0.95f, 1f, 1f)
                : new Color(0.95f, 0.35f, 0.35f, 0.9f);
        }

        visualRoot.anchoredPosition = Vector2.Lerp(visualRoot.anchoredPosition, new Vector2(0f, targetY), dt * animSpeed);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, Vector3.one * targetScale, dt * animSpeed);
        visualRoot.localRotation = Quaternion.Lerp(visualRoot.localRotation, Quaternion.Euler(0f, 0f, targetAngle), dt * animSpeed);

        if (borderGlowImage != null)
            borderGlowImage.color = Color.Lerp(borderGlowImage.color, targetBorder, dt * animSpeed);

        if (visualGroup != null)
        {
            float targetAlpha = (isInteractable || hovered || isSelected) ? 1f : 0.65f;
            visualGroup.alpha = Mathf.Lerp(visualGroup.alpha, targetAlpha, dt * animSpeed);
        }
    }

    private void UpdateSortingOrder()
    {
        if (overrideCanvas == null) return;
        bool bringToFront = hovered || isSelected;
        overrideCanvas.overrideSorting = bringToFront;
        if (bringToFront)
            overrideCanvas.sortingOrder = isSelected ? 110 : 120;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        UpdateSortingOrder();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
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
