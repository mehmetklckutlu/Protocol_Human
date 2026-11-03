using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TurnOrderUI : MonoBehaviour
{
    public static TurnOrderUI Instance { get; private set; }

    [Header("Arayüz Ayarları")]
    public GameObject portraitPrefab;
    public Transform container;

    [Header("Aktif Karakter (Seçili Karo)")]
    public Image activeUnitPortrait;
    public GameObject activeUnitFrame;

    [Header("Renkler")]
    public Color playerColor = Color.green;
    public Color enemyColor = Color.red;

    private Dictionary<Unit, GameObject> portraitDict = new Dictionary<Unit, GameObject>();

    private void Awake()
    {
        Instance = this;

        if (activeUnitPortrait != null) activeUnitPortrait.gameObject.SetActive(false);
        if (activeUnitFrame != null) activeUnitFrame.SetActive(false);
    }

    public void BuildNewRoundQueue()
    {
        portraitDict.Clear();

        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        foreach (Unit player in TurnManager.Instance.playerUnits)
        {
            if (player == null) continue;
            CreatePortrait(player, playerColor);
        }

        foreach (Unit enemy in TurnManager.Instance.enemyUnits)
        {
            if (enemy == null) continue;
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake)
            {
                CreatePortrait(enemy, enemyColor);
            }
        }
    }

    public void AddUnitToQueue(Unit unit)
    {
        if (unit != null && !portraitDict.ContainsKey(unit))
        {
            CreatePortrait(unit, unit.isPlayerOwned ? playerColor : enemyColor);
        }
    }

    public void RemoveUnit(Unit unit)
    {
        if (unit != null && portraitDict.ContainsKey(unit))
        {
            if (portraitDict[unit] != null) Destroy(portraitDict[unit]);
            portraitDict.Remove(unit);
        }
    }

    public void ClearAll()
    {
        portraitDict.Clear();
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        if (activeUnitPortrait != null) activeUnitPortrait.gameObject.SetActive(false);
        if (activeUnitFrame != null) activeUnitFrame.SetActive(false);
    }

    private void CreatePortrait(Unit unit, Color color)
    {
        GameObject portrait = Instantiate(portraitPrefab, container);
        portrait.GetComponent<Image>().color = color;
        portraitDict.Add(unit, portrait);
    }

    public void SetActiveUnit(Unit activeUnit)
    {
        if (portraitDict.ContainsKey(activeUnit))
        {
            Destroy(portraitDict[activeUnit]);
            portraitDict.Remove(activeUnit);
        }

        if (TurnManager.Instance != null && TurnManager.Instance.IsInCombat())
        {
            if (activeUnitPortrait != null)
            {
                activeUnitPortrait.gameObject.SetActive(true);
                activeUnitPortrait.color = activeUnit.isPlayerOwned ? playerColor : enemyColor;
            }

            if (activeUnitFrame != null) activeUnitFrame.SetActive(true);
        }
        else
        {
            if (activeUnitPortrait != null) activeUnitPortrait.gameObject.SetActive(false);
            if (activeUnitFrame != null) activeUnitFrame.SetActive(false);
        }
    }
}