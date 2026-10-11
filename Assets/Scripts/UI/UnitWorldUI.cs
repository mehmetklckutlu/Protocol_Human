using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UnitWorldUI : MonoBehaviour
{
    private Unit targetUnit;

    [Header("Sağlık Barı")]
    [SerializeField] private Image healthFillImage; // İçini dolduracağımız bar

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

    private void Awake()
    {
        targetUnit = GetComponentInParent<Unit>(); // Kendini karakterin üstündeki Unit koduna bağlar
    }

    private void Start()
    {
        if (targetUnit != null)
        {
            InitializePips();
            targetUnit.OnStatsChanged += UpdateVisuals;
            UpdateVisuals(); // İlk değerleri yazdır
        }
    }

    private void OnDestroy()
    {
        if (targetUnit != null)
        {
            targetUnit.OnStatsChanged -= UpdateVisuals;
        }
    }

    private void InitializePips()
    {
        // Hareket çizgilerini oluştur
        for (int i = 0; i < targetUnit.maxMovement; i++)
        {
            GameObject pip = Instantiate(movePipPrefab, moveContainer);
            spawnedMovePips.Add(pip.GetComponent<Image>());
        }

        // CPU çizgilerini oluştur
        for (int i = 0; i < targetUnit.maxCPU; i++)
        {
            GameObject pip = Instantiate(cpuPipPrefab, cpuContainer);
            spawnedCpuPips.Add(pip.GetComponent<Image>());
        }
    }

    private void UpdateVisuals()
    {

        // YENİ: Savaşta değilsek CPU konteynerini tamamen kapat
        if (cpuContainer != null && TurnManager.Instance != null)
        {
            cpuContainer.gameObject.SetActive(TurnManager.Instance.IsInCombat());
        }

        // Eski kodlar aşağıda aynen kalacak...
        if (healthFillImage != null) healthFillImage.fillAmount = (float)targetUnit.currentHealth / targetUnit.maxHealth;
        // ...
        // 1. Sağlık Barı Güncellemesi (Örn: 560 / 1120 = %50 doluluk)
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = (float)targetUnit.currentHealth / targetUnit.maxHealth;
        }

        // 2. Hareket Çizgilerini Güncelle
        for (int i = 0; i < spawnedMovePips.Count; i++)
        {
            spawnedMovePips[i].color = (i < targetUnit.currentMovement) ? moveActiveColor : moveUsedColor;
        }

        // 3. CPU Çizgilerini Güncelle
        for (int i = 0; i < spawnedCpuPips.Count; i++)
        {
            spawnedCpuPips[i].color = (i < targetUnit.currentCPU) ? cpuActiveColor : cpuUsedColor;
        }
    }
}