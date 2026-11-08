using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UnitWorldUI : MonoBehaviour
{
    private Unit targetUnit;

    [Header("Sağlık Barı")]
    [SerializeField] private Image healthFillImage;

    [Header("Kalkan Göstergesi (isteğe bağlı)")]
    [SerializeField] private TMP_Text shieldText;

    [Header("Hareket Göstergesi")]
    [SerializeField] private GameObject movePipPrefab;
    [SerializeField] private Transform moveContainer;
    [SerializeField] private Color moveActiveColor = new Color(0.98f, 0.68f, 0.12f, 1f);
    [SerializeField] private Color moveUsedColor = new Color(0.16f, 0.13f, 0.08f, 0.85f);
    private List<Image> spawnedMovePips = new List<Image>();

    [Header("CPU Göstergesi")]
    [SerializeField] private GameObject cpuPipPrefab;
    [SerializeField] private Transform cpuContainer;
    [SerializeField] private Color cpuActiveColor = Color.cyan;
    [SerializeField] private Color cpuUsedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    private List<Image> spawnedCpuPips = new List<Image>();

    private SpriteRenderer activeTurnRing;
    private Image hpBgImage;
    private GameObject shieldBadgeObject;

    private void Awake()
    {
        targetUnit = GetComponentInParent<Unit>();
        if (cpuContainer != null)
            cpuContainer.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (targetUnit != null)
        {
            ApplyHealthBarStyle();
            EnsureShieldBadge();
            InitializePips();
            CreateActiveTurnRing();
            targetUnit.OnStatsChanged += UpdateVisuals;
            UpdateVisuals();
        }
    }

    private void OnDestroy()
    {
        if (targetUnit != null)
        {
            targetUnit.OnStatsChanged -= UpdateVisuals;
        }
    }

    private void Update()
    {
        if (activeTurnRing == null || targetUnit == null || targetUnit.IsDead)
        {
            if (activeTurnRing != null) activeTurnRing.enabled = false;
            return;
        }

        TurnManager tm = TurnManager.Instance;
        CardManager cm = CardManager.Instance;
        bool isActive = tm != null
            && tm.IsInCombat()
            && tm.CurrentState == TurnState.PlayerTurn
            && cm != null
            && cm.GetActiveUnit() == targetUnit;

        activeTurnRing.enabled = isActive;
        if (isActive)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
            float scale = cellSize * Mathf.Lerp(0.85f, 1.02f, pulse);
            activeTurnRing.transform.localScale = new Vector3(scale, scale, 1f);
            activeTurnRing.color = new Color(0.2f, 0.95f, 1f, Mathf.Lerp(0.4f, 0.85f, pulse));
        }
    }

    private void ApplyHealthBarStyle()
    {
        if (healthFillImage == null || targetUnit == null) return;

        bool isAlly = targetUnit.isPlayerOwned;
        Transform bgTransform = healthFillImage.transform.parent;
        if (bgTransform != null)
        {
            RectTransform bgRect = bgTransform as RectTransform;
            if (bgRect != null)
            {
                bgRect.localScale = Vector3.one;
                bgRect.anchoredPosition = new Vector2(100f, 21f);
                bgRect.sizeDelta = new Vector2(104f, 18f);
            }

            hpBgImage = bgTransform.GetComponent<Image>();
            if (hpBgImage != null)
            {
                hpBgImage.sprite = isAlly ? RuntimeArt.AllyHpFrame : RuntimeArt.EnemyHpFrame;
                hpBgImage.type = Image.Type.Simple;
                hpBgImage.color = Color.white;
                hpBgImage.raycastTarget = false;
            }
        }

        RectTransform fillRect = healthFillImage.rectTransform;
        if (fillRect != null)
        {
            fillRect.localScale = Vector3.one;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.offsetMin = new Vector2(isAlly ? 4f : 5f, 3f);
            fillRect.offsetMax = new Vector2(isAlly ? -4f : -5f, -3f);
        }

        healthFillImage.sprite = isAlly ? RuntimeArt.AllyHpFill : RuntimeArt.EnemyHpFill;
        healthFillImage.color = Color.white;
        healthFillImage.type = Image.Type.Filled;
        healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFillImage.raycastTarget = false;
    }

    private void EnsureShieldBadge()
    {
        if (healthFillImage == null || shieldBadgeObject != null) return;

        Transform bgTransform = healthFillImage.transform.parent;
        if (bgTransform == null) return;

        shieldBadgeObject = new GameObject("ShieldBadge", typeof(RectTransform), typeof(Image));
        shieldBadgeObject.transform.SetParent(bgTransform, false);

        RectTransform badgeRect = shieldBadgeObject.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 0.5f);
        badgeRect.anchorMax = new Vector2(1f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(16f, 0f);
        badgeRect.sizeDelta = new Vector2(26f, 29f);
        badgeRect.localScale = Vector3.one;

        Image badgeImg = shieldBadgeObject.GetComponent<Image>();
        badgeImg.sprite = RuntimeArt.ShieldIcon;
        badgeImg.type = Image.Type.Simple;
        badgeImg.color = Color.white;
        badgeImg.raycastTarget = false;

        GameObject textGo = new GameObject("ShieldValueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(shieldBadgeObject.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(-4f, -2f);
        textRect.offsetMax = new Vector2(4f, 2f);
        textRect.localScale = Vector3.one;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 13f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(5, 18, 36, 255);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        shieldText = tmp;
        shieldBadgeObject.SetActive(false);
    }

    private void CreateActiveTurnRing()
    {
        if (!targetUnit.isPlayerOwned) return;
        GameObject ringGo = new GameObject("ActiveTurnRing");
        ringGo.transform.SetParent(targetUnit.transform, false);
        ringGo.transform.localPosition = Vector3.zero;
        activeTurnRing = ringGo.AddComponent<SpriteRenderer>();
        activeTurnRing.sprite = RuntimeArt.Ring;
        activeTurnRing.sortingOrder = 1;
        activeTurnRing.enabled = false;
    }

    private void InitializePips()
    {
        if (moveContainer != null && movePipPrefab != null)
        {
            RectTransform moveRect = moveContainer as RectTransform;
            if (moveRect != null)
            {
                moveRect.localScale = Vector3.one;
                moveRect.anchoredPosition = new Vector2(100f, 7f);
                moveRect.sizeDelta = new Vector2(98f, 9f);
            }

            HorizontalLayoutGroup hlg = moveContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 3f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
            }

            for (int i = 0; i < targetUnit.maxMovement; i++)
            {
                GameObject pip = Instantiate(movePipPrefab, moveContainer);
                Image pipImg = pip.GetComponent<Image>();
                if (pipImg != null)
                {
                    pipImg.sprite = RuntimeArt.StaminaPipActive;
                    pipImg.type = Image.Type.Simple;
                    pipImg.color = Color.white;
                    pipImg.raycastTarget = false;
                    spawnedMovePips.Add(pipImg);
                }
            }
        }

        if (cpuContainer != null)
        {
            cpuContainer.gameObject.SetActive(false);
        }
    }

    private void UpdateVisuals()
    {
        if (cpuContainer != null)
        {
            cpuContainer.gameObject.SetActive(false);
        }

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = (float)targetUnit.currentHealth / targetUnit.maxHealth;
        }

        bool hasShield = targetUnit.currentShield > 0;
        if (hpBgImage != null)
        {
            if (hasShield)
                hpBgImage.sprite = RuntimeArt.ShieldedHpFrame;
            else
                hpBgImage.sprite = targetUnit.isPlayerOwned ? RuntimeArt.AllyHpFrame : RuntimeArt.EnemyHpFrame;
        }

        if (shieldBadgeObject != null)
        {
            shieldBadgeObject.SetActive(hasShield);
        }

        if (shieldText != null)
        {
            shieldText.gameObject.SetActive(hasShield);
            if (hasShield) shieldText.text = targetUnit.currentShield.ToString();
        }

        for (int i = 0; i < spawnedMovePips.Count; i++)
        {
            if (spawnedMovePips[i] == null) continue;
            bool active = i < targetUnit.currentMovement;
            spawnedMovePips[i].sprite = active ? RuntimeArt.StaminaPipActive : RuntimeArt.StaminaPipEmpty;
            spawnedMovePips[i].color = Color.white;
        }
    }
}
