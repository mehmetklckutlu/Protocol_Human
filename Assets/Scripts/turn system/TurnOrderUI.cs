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

    private static readonly Color AllyBgColor = new Color(0.05f, 0.20f, 0.13f, 0.72f);
    private static readonly Color AllyBorderColor = new Color(0.25f, 0.92f, 0.52f, 0.80f);
    private static readonly Color EnemyBgColor = new Color(0.22f, 0.05f, 0.07f, 0.72f);
    private static readonly Color EnemyBorderColor = new Color(0.95f, 0.28f, 0.28f, 0.80f);

    private Dictionary<Unit, GameObject> portraitDict = new Dictionary<Unit, GameObject>();
    private Image activeUnitIcon;

    private void Awake()
    {
        Instance = this;

        if (container != null)
        {
            HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 8f;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
            }
        }

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

        Unit main = null;
        foreach (Unit player in TurnManager.Instance.playerUnits)
        {
            if (player == null) continue;
            if (player.isMainDrone) main = player;
            else CreatePortrait(player, playerColor);
        }
        if (main != null)
        {
            CreatePortrait(main, playerColor);
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
        bool isAlly = unit != null && unit.isPlayerOwned;

        RectTransform rt = portrait.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(56f, 56f);

        Image bgImg = portrait.GetComponent<Image>();
        if (bgImg != null)
        {
            bgImg.sprite = null;
            bgImg.color = isAlly ? AllyBgColor : EnemyBgColor;
            bgImg.raycastTarget = false;
        }

        Outline outline = portrait.GetComponent<Outline>();
        if (outline == null) outline = portrait.AddComponent<Outline>();
        outline.effectColor = isAlly ? AllyBorderColor : EnemyBorderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject iconGo = new GameObject("DroneIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(portrait.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(5f, 5f);
        iconRt.offsetMax = new Vector2(-5f, -5f);

        Image iconImg = iconGo.GetComponent<Image>();
        Sprite sp = unit != null ? unit.PortraitSprite : null;
        iconImg.sprite = sp;
        iconImg.preserveAspect = true;
        iconImg.color = sp != null ? Color.white : color;
        iconImg.raycastTarget = false;

        portraitDict.Add(unit, portrait);
    }

    private Image EnsureActiveUnitIcon()
    {
        if (activeUnitPortrait == null) return null;
        if (activeUnitIcon != null) return activeUnitIcon;

        Transform existing = activeUnitPortrait.transform.Find("ActiveDroneIcon");
        if (existing != null)
        {
            activeUnitIcon = existing.GetComponent<Image>();
            if (activeUnitIcon != null) return activeUnitIcon;
        }

        GameObject iconGo = new GameObject("ActiveDroneIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(activeUnitPortrait.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(8f, 8f);
        iconRt.offsetMax = new Vector2(-8f, -8f);

        activeUnitIcon = iconGo.GetComponent<Image>();
        activeUnitIcon.preserveAspect = true;
        activeUnitIcon.raycastTarget = false;
        return activeUnitIcon;
    }

    public void SetActiveUnit(Unit activeUnit)
    {
        if (activeUnit != null && portraitDict.ContainsKey(activeUnit))
        {
            Destroy(portraitDict[activeUnit]);
            portraitDict.Remove(activeUnit);
        }

        if (TurnManager.Instance != null && TurnManager.Instance.IsInCombat() && activeUnit != null)
        {
            if (activeUnitPortrait != null)
            {
                bool isAlly = activeUnit.isPlayerOwned;
                activeUnitPortrait.gameObject.SetActive(true);
                activeUnitPortrait.sprite = null;
                activeUnitPortrait.color = isAlly ? AllyBgColor : EnemyBgColor;
                activeUnitPortrait.raycastTarget = false;

                Outline outline = activeUnitPortrait.GetComponent<Outline>();
                if (outline == null) outline = activeUnitPortrait.gameObject.AddComponent<Outline>();
                outline.effectColor = isAlly ? AllyBorderColor : EnemyBorderColor;
                outline.effectDistance = new Vector2(2.5f, -2.5f);

                Image iconImg = EnsureActiveUnitIcon();
                if (iconImg != null)
                {
                    Sprite sp = activeUnit.PortraitSprite;
                    iconImg.sprite = sp;
                    iconImg.preserveAspect = true;
                    iconImg.color = sp != null ? Color.white : (isAlly ? playerColor : enemyColor);
                }
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
