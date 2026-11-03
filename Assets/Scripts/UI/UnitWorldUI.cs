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
    [SerializeField] private Color moveActiveColor = Color.green;
    [SerializeField] private Color moveUsedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    private List<Image> spawnedMovePips = new List<Image>();

    [Header("CPU Göstergesi")]
    [SerializeField] private GameObject cpuPipPrefab;
    [SerializeField] private Transform cpuContainer;
    [SerializeField] private Color cpuActiveColor = Color.cyan;
    [SerializeField] private Color cpuUsedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    private List<Image> spawnedCpuPips = new List<Image>();

    private SpriteRenderer activeTurnRing;

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
            for (int i = 0; i < targetUnit.maxMovement; i++)
            {
                GameObject pip = Instantiate(movePipPrefab, moveContainer);
                spawnedMovePips.Add(pip.GetComponent<Image>());
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

        if (shieldText != null)
        {
            shieldText.gameObject.SetActive(targetUnit.currentShield > 0);
            shieldText.text = targetUnit.currentShield.ToString();
        }

        for (int i = 0; i < spawnedMovePips.Count; i++)
        {
            spawnedMovePips[i].color = (i < targetUnit.currentMovement) ? moveActiveColor : moveUsedColor;
        }
    }
}
