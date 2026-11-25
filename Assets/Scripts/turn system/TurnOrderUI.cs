using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ekranın üstündeki tur sırası çubuğu.
///  [TUR 3 · SENİN SIRAN] [oynadı][oynadı] [ AKTİF ] › [sıradaki][sıradaki]... [+N] | TUR SONU
/// Her portrede can şeridi, tip ikonu (Uzak/Yakın/Boss) ve durum ikonu (sersem/taunt) var.
/// Hamlesini yapan birim silinmez, sönük + tikli kalır. Portrenin üstüne gelince bilgi kartı açılır
/// ve haritada o birim vurgulanır.
/// TurnManager'ın çağırdığı API aynı: BuildNewRoundQueue, AddUnitToQueue, RemoveUnit, ClearAll, SetActiveUnit.
/// </summary>
public class TurnOrderUI : MonoBehaviour
{
    public static TurnOrderUI Instance { get; private set; }

    // Eski sahne bağlantıları: sahnedeki referanslar bozulmasın diye duruyor, eski görseller gizlenir.
    [Header("Eski arayüz (gizlenir)")]
    public GameObject portraitPrefab;
    public Transform container;
    public Image activeUnitPortrait;
    public GameObject activeUnitFrame;
    public Color playerColor = Color.green;
    public Color enemyColor = Color.red;

    [Header("Yeni çubuk")]
    [SerializeField, Min(1)] private int maxUpcoming = 8;
    [SerializeField, Min(0)] private int maxActedShown = 3;
    [SerializeField] private float topMargin = 14f;

    // ---- boyutlar (1920x1080 referans UI birimi) ----
    private const float ActiveSize = 66f;
    private const float SlotSize = 46f;
    private const float Gap = 8f;
    private const float HpH = 7f;

    private static readonly Color32 AllyBorder = new Color32(120, 255, 150, 255);
    private static readonly Color32 AllyBorderDim = new Color32(40, 150, 80, 255);
    private static readonly Color32 AllyFill = new Color32(10, 40, 24, 235);
    private static readonly Color32 EnemyBorder = new Color32(255, 70, 60, 255);
    private static readonly Color32 EnemyBorderDim = new Color32(150, 40, 40, 255);
    private static readonly Color32 EnemyFill = new Color32(40, 10, 14, 235);
    private static readonly Color DimGray = new Color(0.39f, 0.43f, 0.45f, 1f);

    // ---- model ----
    private readonly List<Unit> order = new List<Unit>();
    private readonly HashSet<Unit> acted = new HashSet<Unit>();
    private Unit current;
    private int round;
    private int lastPhaseKey = -1;
    private bool dirty;

    // ---- arayüz ----
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform bar;
    private Image bannerBg;
    private TMP_Text bannerRound, bannerPhase;
    private RectTransform arrow;
    private Image arrowImg;
    private TMP_Text sepText, endText, actedMoreText, overflowText;
    private Image actedMoreBg, overflowBg;
    private readonly List<Slot> slots = new List<Slot>();
    private int usedSlots;

    // ---- hover ----
    private Unit hoverUnit;
    private Slot hoverSlot;
    private RectTransform tooltip;
    private TMP_Text tipTitle, tipLeft, tipRight;
    private SpriteRenderer mapMarker;

    private class Slot
    {
        public RectTransform rt;
        public Image frame, icon, hpBack, hpFill, typeIcon, statusIcon, dim, check;
        public Unit unit;
        public bool isActive, isActed;
        public float size;
    }

    // ================================================================== yaşam döngüsü

    private void Awake()
    {
        Instance = this;
        HideLegacy(container != null ? container.gameObject : null);
        HideLegacy(activeUnitPortrait != null ? activeUnitPortrait.gameObject : null);
        HideLegacy(activeUnitFrame);
        Build();
        SetVisible(false);
    }

    /// <summary>
    /// Eski görseli gizler. Bu script'in kendi objesi ya da atası ise objeyi kapatmaz
    /// (yoksa script de durur); sadece eski çocukları siler ve grafiklerini kapatır.
    /// </summary>
    private void HideLegacy(GameObject go)
    {
        if (go == null) return;
        bool containsMe = go == gameObject || transform.IsChildOf(go.transform);
        if (!containsMe) { go.SetActive(false); return; }

        foreach (Transform child in go.transform)
            if (child != transform && !transform.IsChildOf(child)) Destroy(child.gameObject);
        foreach (Graphic g in go.GetComponents<Graphic>()) g.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (mapMarker != null) Destroy(mapMarker.gameObject);
    }

    private void Update()
    {
        TurnManager tm = TurnManager.Instance;
        bool show = tm != null && tm.IsInCombat() && !tm.IsGameOver && round > 0;
        SetVisible(show);
        if (!show) { HideHover(); return; }

        // Ölen / yok olan birimleri temizle
        for (int i = order.Count - 1; i >= 0; i--)
            if (order[i] == null || order[i].IsDead) { acted.Remove(order[i]); order.RemoveAt(i); dirty = true; }
        if (current != null && current.IsDead) { current = null; dirty = true; }

        if (dirty) { dirty = false; Rebuild(); }
        RefreshDynamic();
        UpdateHover();
    }

    private void SetVisible(bool on)
    {
        if (group == null) return;
        float a = on ? 1f : 0f;
        if (!Mathf.Approximately(group.alpha, a))
        {
            group.alpha = a;
            group.blocksRaycasts = on;
        }
    }

    // ================================================================== TurnManager API

    public void BuildNewRoundQueue()
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null) return;

        round++;
        order.Clear();
        acted.Clear();
        current = null;

        Unit main = null;
        foreach (Unit p in tm.playerUnits)
        {
            if (p == null || p.IsDead) continue;
            if (p.isMainDrone) main = p; else order.Add(p);
        }
        if (main != null) order.Add(main);

        foreach (Unit e in tm.enemyUnits)
        {
            if (e == null || e.IsDead) continue;
            EnemyAI ai = e.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake) order.Add(e);
        }
        dirty = true;
    }

    public void AddUnitToQueue(Unit unit)
    {
        if (unit == null || order.Contains(unit)) return;
        if (unit.isPlayerOwned)
        {
            int firstEnemy = order.FindIndex(u => u != null && !u.isPlayerOwned);
            if (firstEnemy < 0) order.Add(unit); else order.Insert(firstEnemy, unit);
        }
        else order.Add(unit);
        dirty = true;
    }

    public void RemoveUnit(Unit unit)
    {
        if (unit == null) return;
        order.Remove(unit);
        acted.Remove(unit);
        if (current == unit) current = null;
        if (hoverUnit == unit) HideHover();
        dirty = true;
    }

    public void ClearAll()
    {
        order.Clear();
        acted.Clear();
        current = null;
        round = 0;
        HideHover();
        dirty = true;
        SetVisible(false);
    }

    public void SetActiveUnit(Unit activeUnit)
    {
        if (activeUnit == null) return;
        if (current != null && current != activeUnit) acted.Add(current);
        current = activeUnit;
        acted.Remove(activeUnit);
        if (!order.Contains(activeUnit)) AddUnitToQueue(activeUnit);
        if (round == 0) round = 1;
        dirty = true;
    }

    // ================================================================== kurulum

    private void Build()
    {
        GameObject cgo = new GameObject("TurnOrderCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        group = cgo.GetComponent<CanvasGroup>();

        bar = NewRect(cgo.transform, "Bar");
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0f, 1f);

        // Banner
        RectTransform bn = NewRect(bar, "Banner");
        bn.sizeDelta = new Vector2(196f, 62f);
        bannerBg = bn.gameObject.AddComponent<Image>();
        bannerBg.type = Image.Type.Sliced;
        bannerBg.raycastTarget = false;
        bannerRound = Text(bn, "Round", 17, HudArt.Dim, TextAlignmentOptions.TopLeft);
        Place(bannerRound.rectTransform, 14f, -6f, 160f, 22f);
        bannerPhase = Text(bn, "Phase", 23, HudArt.Cyan, TextAlignmentOptions.TopLeft);
        Place(bannerPhase.rectTransform, 14f, -28f, 180f, 28f);

        sepText = Text(bar, "Sep", 26, HudArt.Dim, TextAlignmentOptions.Center);
        sepText.text = ">";
        sepText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        sepText.rectTransform.sizeDelta = new Vector2(20f, 30f);

        endText = Text(bar, "End", 17, HudArt.Dim, TextAlignmentOptions.Left);
        endText.text = "| TUR SONU";
        endText.rectTransform.pivot = new Vector2(0f, 0.5f);
        endText.rectTransform.sizeDelta = new Vector2(140f, 24f);

        overflowBg = Chip(bar, "Overflow", out overflowText, EnemyBorderDim);
        actedMoreBg = Chip(bar, "ActedMore", out actedMoreText, new Color32(70, 86, 96, 255));

        arrow = NewRect(bar, "Arrow");
        arrow.sizeDelta = new Vector2(9f * HudArt.Px * 0.8f, 6f * HudArt.Px * 0.8f);
        arrowImg = arrow.gameObject.AddComponent<Image>();
        arrowImg.sprite = HudArt.ArrowDown;
        arrowImg.raycastTarget = false;

        BuildTooltip(cgo.transform);

        GameObject mk = new GameObject("TurnHoverMarker");
        mapMarker = mk.AddComponent<SpriteRenderer>();
        mapMarker.sortingOrder = 13;
        mapMarker.enabled = false;
    }

    private Image Chip(Transform parent, string name, out TMP_Text label, Color32 border)
    {
        RectTransform rt = NewRect(parent, name);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = HudArt.Panel(border);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;
        label = Text(rt, "Label", 20, new Color(1f, 0.6f, 0.55f), TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        rt.gameObject.SetActive(false);
        return bg;
    }

    private void BuildTooltip(Transform root)
    {
        tooltip = NewRect(root, "Tooltip");
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 1f);
        tooltip.pivot = new Vector2(0f, 1f);
        tooltip.sizeDelta = new Vector2(250f, 96f);
        Image bg = tooltip.gameObject.AddComponent<Image>();
        bg.sprite = HudArt.Panel(HudArt.CyanMid);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;
        tipTitle = Text(tooltip, "Title", 21, new Color(1f, 0.6f, 0.55f), TextAlignmentOptions.TopLeft);
        Place(tipTitle.rectTransform, 14f, -8f, 230f, 26f);
        tipLeft = Text(tooltip, "Left", 17, HudArt.Dim, TextAlignmentOptions.TopLeft);
        Place(tipLeft.rectTransform, 14f, -36f, 115f, 54f);
        tipLeft.richText = true;
        tipRight = Text(tooltip, "Right", 17, HudArt.Dim, TextAlignmentOptions.TopLeft);
        Place(tipRight.rectTransform, 128f, -36f, 115f, 54f);
        tooltip.gameObject.SetActive(false);
    }

    private Slot GetSlot(int i)
    {
        while (slots.Count <= i)
        {
            Slot s = new Slot();
            s.rt = NewRect(bar, "Slot" + slots.Count);
            s.frame = s.rt.gameObject.AddComponent<Image>();
            s.frame.type = Image.Type.Sliced;
            s.frame.raycastTarget = true; // hover için

            s.icon = Img(s.rt, "Icon", null);
            s.icon.preserveAspect = true;
            s.dim = Img(s.rt, "Dim", HudArt.Solid);
            s.dim.color = new Color(0.02f, 0.04f, 0.055f, 0.5f);
            s.check = Img(s.rt, "Check", HudArt.CheckIcon);
            s.typeIcon = Img(s.rt, "Type", null);
            s.statusIcon = Img(s.rt, "Status", null);
            s.hpBack = Img(s.rt, "HpBack", HudArt.Solid);
            s.hpBack.color = (Color)HudArt.Outline;
            s.hpFill = Img(s.hpBack.rectTransform, "HpFill", HudArt.Solid);
            s.hpFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            s.hpFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            s.hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);

            TurnSlotHover h = s.rt.gameObject.AddComponent<TurnSlotHover>();
            Slot captured = s;
            h.onEnter = () => BeginHover(captured);
            h.onExit = () => { if (hoverSlot == captured) HideHover(); };

            slots.Add(s);
        }
        return slots[i];
    }

    // ================================================================== yerleşim

    private void Rebuild()
    {
        // Sıralar
        List<Unit> actedList = new List<Unit>();
        List<Unit> upcoming = new List<Unit>();
        foreach (Unit u in order)
        {
            if (u == null || u == current) continue;
            if (acted.Contains(u)) actedList.Add(u); else upcoming.Add(u);
        }

        float x = 0f;
        float midY = -topMargin - ActiveSize * 0.5f;

        // Banner
        RectTransform bn = bannerBg.rectTransform;
        bn.anchoredPosition = new Vector2(x, midY + bn.sizeDelta.y * 0.5f);
        x += bn.sizeDelta.x + 14f;

        usedSlots = 0;

        // Oynayanlar (en fazla son N tanesi), fazlası "+N" çipine
        int hiddenActed = Mathf.Max(0, actedList.Count - maxActedShown);
        if (hiddenActed > 0)
        {
            PlaceChip(actedMoreBg, actedMoreText, "+" + hiddenActed, ref x, midY);
            actedMoreText.color = HudArt.Dim;
        }
        else actedMoreBg.gameObject.SetActive(false);

        for (int i = hiddenActed; i < actedList.Count; i++)
            PlaceSlot(actedList[i], SlotSize, false, true, ref x, midY);
        if (actedList.Count > 0) x += 4f;

        // Aktif
        Slot activeSlot = null;
        if (current != null)
        {
            activeSlot = PlaceSlot(current, ActiveSize, true, false, ref x, midY);
            arrow.gameObject.SetActive(true);
            arrow.pivot = new Vector2(0.5f, 1f);
            arrow.anchoredPosition = new Vector2(activeSlot.rt.anchoredPosition.x + ActiveSize * 0.5f,
                                                 activeSlot.rt.anchoredPosition.y - ActiveSize - HpH - 8f);
        }
        else arrow.gameObject.SetActive(false);

        // Ayırıcı
        if (upcoming.Count > 0)
        {
            sepText.gameObject.SetActive(true);
            sepText.rectTransform.anchoredPosition = new Vector2(x + 10f, midY);
            x += 24f;
        }
        else sepText.gameObject.SetActive(false);

        // Sıradakiler
        int shown = Mathf.Min(upcoming.Count, maxUpcoming);
        for (int i = 0; i < shown; i++)
            PlaceSlot(upcoming[i], SlotSize, false, false, ref x, midY);

        int extra = upcoming.Count - shown;
        if (extra > 0)
        {
            PlaceChip(overflowBg, overflowText, "+" + extra, ref x, midY);
            overflowText.color = new Color(1f, 0.6f, 0.55f);
        }
        else overflowBg.gameObject.SetActive(false);

        // Tur sonu
        endText.rectTransform.anchoredPosition = new Vector2(x + 4f, midY);
        x += 4f + 140f;

        // Kullanılmayan slotlar
        for (int i = usedSlots; i < slots.Count; i++)
        {
            slots[i].rt.gameObject.SetActive(false);
            slots[i].unit = null;
        }

        // Ortala
        bar.anchoredPosition = new Vector2(-x * 0.5f, 0f);

        if (hoverSlot != null && (hoverSlot.unit == null || !hoverSlot.rt.gameObject.activeSelf)) HideHover();
    }

    private void PlaceChip(Image bg, TMP_Text label, string text, ref float x, float midY)
    {
        RectTransform rt = bg.rectTransform;
        rt.gameObject.SetActive(true);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, midY + SlotSize * 0.5f - 6f);
        label.text = text;
        x += SlotSize + Gap;
    }

    private Slot PlaceSlot(Unit u, float size, bool isActive, bool isActed, ref float x, float midY)
    {
        Slot s = GetSlot(usedSlots++);
        s.unit = u;
        s.isActive = isActive;
        s.isActed = isActed;
        s.size = size;
        s.rt.gameObject.SetActive(true);

        // Küçük slotlar aktif kutuya göre biraz aşağıda, altları hizalı
        float top = isActive ? midY + size * 0.5f : midY + size * 0.5f - 6f;
        s.rt.anchoredPosition = new Vector2(x, top);
        s.rt.sizeDelta = new Vector2(size, size);
        x += size + Gap;

        bool ally = u.isPlayerOwned;
        Color32 border = isActed ? new Color32(70, 86, 96, 255) : (ally ? (isActive ? AllyBorder : AllyBorderDim) : (isActive ? EnemyBorder : EnemyBorderDim));
        s.frame.sprite = HudArt.Panel(border, ally ? AllyFill : EnemyFill);
        s.frame.color = Color.white;

        float pad = size >= ActiveSize ? 7f : 5f;
        Fill(s.icon.rectTransform, pad);
        s.icon.sprite = u.PortraitSprite;
        s.icon.color = isActed ? new Color(0.5f, 0.52f, 0.55f, 0.55f) : Color.white;

        Fill(s.dim.rectTransform, 3f);
        s.dim.gameObject.SetActive(isActed);
        s.check.gameObject.SetActive(isActed);
        if (isActed) Center(s.check.rectTransform, 21f, 21f);

        // Tip ikonu sağ alt
        Sprite type = TypeIcon(u);
        s.typeIcon.gameObject.SetActive(type != null);
        if (type != null)
        {
            s.typeIcon.sprite = type;
            Corner(s.typeIcon.rectTransform, new Vector2(1f, 0f), new Vector2(-3f, 3f), 14f);
            s.typeIcon.color = isActed ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        }

        // Durum ikonu sol üst
        Corner(s.statusIcon.rectTransform, new Vector2(0f, 1f), new Vector2(3f, -3f), 14f);

        // Can şeridi kutunun altında
        RectTransform hb = s.hpBack.rectTransform;
        hb.anchorMin = hb.anchorMax = new Vector2(0f, 0f);
        hb.pivot = new Vector2(0f, 1f);
        hb.anchoredPosition = new Vector2(1f, -3f);
        hb.sizeDelta = new Vector2(size - 2f, HpH);
        RectTransform hf = s.hpFill.rectTransform;
        hf.offsetMin = new Vector2(1f, 1f);
        hf.offsetMax = new Vector2(1f, -1f);

        return s;
    }

    /// <summary>Her kare: can, durum ikonları, banner, aktif kutunun nabzı.</summary>
    private void RefreshDynamic()
    {
        bool playerPhase = current == null ? (TurnManager.Instance.CurrentState != TurnState.EnemyTurn) : current.isPlayerOwned;
        int phaseKey = round * 2 + (playerPhase ? 1 : 0);
        if (phaseKey != lastPhaseKey)
        {
            lastPhaseKey = phaseKey;
            bannerBg.sprite = HudArt.Panel(playerPhase ? HudArt.CyanMid : EnemyBorderDim);
            bannerRound.text = "TUR " + round;
            bannerPhase.text = playerPhase ? "SENİN SIRAN" : "DÜŞMAN SIRASI";
            bannerPhase.color = playerPhase ? (Color)HudArt.Cyan : (Color)EnemyBorder;
            arrowImg.color = playerPhase ? (Color)AllyBorder : (Color)EnemyBorder;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

        for (int i = 0; i < usedSlots; i++)
        {
            Slot s = slots[i];
            Unit u = s.unit;
            if (u == null) continue;

            float frac = u.maxHealth > 0 ? Mathf.Clamp01((float)u.currentHealth / u.maxHealth) : 0f;
            float w = (s.size - 4f) * frac;
            s.hpFill.rectTransform.sizeDelta = new Vector2(w, s.hpFill.rectTransform.sizeDelta.y);
            s.hpFill.color = s.isActed ? DimGray : (u.isPlayerOwned ? (Color)AllyBorder : (Color)EnemyBorder);

            Sprite status = null;
            if (u.stunTurns > 0) status = HudArt.StunIcon;
            else if (u.tauntTurns > 0) status = HudArt.TauntIcon;
            s.statusIcon.gameObject.SetActive(status != null);
            if (status != null) s.statusIcon.sprite = status;

            if (s.isActive) s.frame.color = Color.Lerp(new Color(0.8f, 0.8f, 0.8f, 1f), Color.white, pulse);
        }
    }

    // ================================================================== hover

    private void BeginHover(Slot s)
    {
        if (s == null || s.unit == null) return;
        hoverSlot = s;
        hoverUnit = s.unit;
        FillTooltip(s.unit);
        tooltip.gameObject.SetActive(true);
        Sfx.Play("ui_hover", 0.25f, 0.06f, 0.06f);
    }

    private void HideHover()
    {
        hoverSlot = null;
        hoverUnit = null;
        if (tooltip != null) tooltip.gameObject.SetActive(false);
        if (mapMarker != null) mapMarker.enabled = false;
    }

    private void UpdateHover()
    {
        if (hoverSlot == null || hoverUnit == null || hoverSlot.unit != hoverUnit || hoverUnit.IsDead)
        {
            if (hoverSlot != null || hoverUnit != null) HideHover();
            return;
        }

        // Bilgi kartı slotun altında (ekran kenarına taşmasın)
        Vector2 slotPos = bar.anchoredPosition + hoverSlot.rt.anchoredPosition;
        float tx = slotPos.x;
        RectTransform canvasRt = (RectTransform)canvas.transform;
        float halfW = canvasRt.rect.width * 0.5f;
        tx = Mathf.Clamp(tx, -halfW + 10f, halfW - tooltip.sizeDelta.x - 10f);
        tooltip.anchoredPosition = new Vector2(tx, slotPos.y - hoverSlot.size - HpH - 14f);
        FillTooltip(hoverUnit);

        // Haritada vurgu: sadece görünen birim
        GridManager gm = GridManager.Instance;
        bool visible = gm != null && (FogOfWar.Instance == null || FogOfWar.Instance.IsVisible(gm.GetGridPosition(hoverUnit.transform.position)));
        mapMarker.enabled = visible;
        if (visible)
        {
            mapMarker.sprite = CursorArt.GetGridSprite(hoverUnit.isPlayerOwned ? "ally" : "target");
            Vector3 p = gm.GetWorldPosition(gm.GetGridPosition(hoverUnit.transform.position));
            p.z = -1f;
            mapMarker.transform.position = p;
            float sc = gm.CellSize * (1.05f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f));
            mapMarker.transform.localScale = new Vector3(sc, sc, 1f);
        }
    }

    private void FillTooltip(Unit u)
    {
        DroneUnit d = u.GetComponent<DroneUnit>();
        EnemyAI ai = u.GetComponent<EnemyAI>();
        bool ally = u.isPlayerOwned;

        tipTitle.text = UnitTitle(u, d);
        tipTitle.color = ally ? (Color)AllyBorder : new Color(1f, 0.6f, 0.55f);

        string hpCol = ally ? "#78FF96" : "#FF463C";
        string left = "<color=" + hpCol + ">HP " + Mathf.Max(0, u.currentHealth) + "/" + u.maxHealth + "</color>";
        string right = "HAREKET " + u.maxMovement;

        if (ai != null)
        {
            IntRange dmg = ai.attackDamage;
            left += "\nHASAR " + (dmg.IsFixed ? dmg.min.ToString() : dmg.min + "-" + dmg.max);
            right += "\nMENZİL " + ai.attackRange;
        }
        else if (u.isMainDrone)
        {
            left += "\nCPU " + u.currentCPU + "/" + u.maxCPU;
            right += u.currentShield > 0 ? "\nKALKAN " + u.currentShield : "";
        }
        else if (d != null && d.isAlly)
        {
            left += "\nŞARJ " + d.charge;
        }

        if (u.stunTurns > 0) right += "\n<color=#FFE650>SERSEM " + u.stunTurns + "</color>";
        else if (u.tauntTurns > 0) right += "\n<color=#FFB833>TAUNT " + u.tauntTurns + "</color>";

        tipLeft.text = left;
        tipRight.text = right;
        tipRight.richText = true;
    }

    private static string UnitTitle(Unit u, DroneUnit d)
    {
        if (u.isMainDrone) return "PH-01";
        string type = d == null ? "DRONE" :
            d.droneType == DroneType.Ranged ? "UZAK" :
            d.droneType == DroneType.Boss ? "BOSS" : "YAKIN";
        return u.isPlayerOwned ? "DOST " + type : type + " DRONE";
    }

    private static Sprite TypeIcon(Unit u)
    {
        if (u.isMainDrone) return HudArt.StarIcon;
        DroneUnit d = u.GetComponent<DroneUnit>();
        if (d == null) return null;
        switch (d.droneType)
        {
            case DroneType.Ranged: return HudArt.RangedIcon;
            case DroneType.Boss: return HudArt.BossIcon;
            default: return HudArt.MeleeIcon;
        }
    }

    // ================================================================== küçük yardımcılar

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        return rt;
    }

    private static Image Img(Transform parent, string name, Sprite sprite)
    {
        RectTransform rt = NewRect(parent, name);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Text(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(parent, name);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero;
        return t;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Fill(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Corner(RectTransform rt, Vector2 anchor, Vector2 offset, float size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(size, size);
    }
}

/// <summary>Tur çubuğu portresi için fare giriş/çıkış olayları.</summary>
public class TurnSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action onEnter, onExit;
    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
}
