using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum TurnState { PlayerTurn, EnemyTurn, Busy }

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }
    public TurnState CurrentState { get; private set; }

    [Header("Sahnedeki Karakterler")]
    public List<Unit> playerUnits = new List<Unit>();
    public List<Unit> enemyUnits = new List<Unit>();

    [Header("Arayüz (UI)")]
    public GameObject enemyAlertUI;
    public GameObject endTurnButton;
    public GameObject roundEndUI;

    [Header("Kısayol")]
    [Tooltip("Turu bitirmek için klavye kısayol tuşu.")]
    public KeyCode endTurnHotkey = KeyCode.E;

    private readonly List<Unit> playerQueue = new List<Unit>();
    public Unit CurrentPlayerUnit { get; private set; }

    private TurnState previousState;
    private bool wasInCombat = false;

    private void Awake()
    {
        Instance = this;
        // Sahnelerde Turu Bitir butonu atanmamışsa adından bul; savaş dışında gizlenebilsin.
        if (endTurnButton == null) endTurnButton = GameObject.Find("EndTurn_Button");
        StyleEndTurnButton();
        // Eski sahne yazıları hiç görünmesin (yerlerine HudBanner kullanılıyor).
        if (enemyAlertUI != null) enemyAlertUI.SetActive(false);
        if (roundEndUI != null) roundEndUI.SetActive(false);
        RuntimeArt.ApplyPixelFontToScene();

        foreach (Unit u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            if (u != null && !u.IsDead) RegisterUnit(u);
        }
    }

    private void StyleEndTurnButton()
    {
        if (endTurnButton == null) return;

        RectTransform rt = endTurnButton.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 24f);
            rt.sizeDelta = new Vector2(236f, 68f);
        }

        Image img = endTurnButton.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = RuntimeArt.EndTurnButtonSprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }

        Button btn = endTurnButton.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.72f, 1f, 1f, 1f);
            cb.pressedColor = new Color(1f, 0.82f, 0.38f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
        }

        TMP_Text label = endTurnButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            // Yazı butonun içinde ortalı dursun: tek satır, butonu kenar boşluklarıyla doldurur,
            // sığmazsa küçülür. Kısayol tuşu sağda küçük bir etiket olarak.
            RuntimeArt.ApplyPixelFont(label);
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(14f, 6f);
            lr.offsetMax = new Vector2(-14f, -6f);
            lr.localScale = Vector3.one;
            label.text = "TURU BİTİR  <size=70%><color=#FFD264>[E]</color></size>";
            label.fontSize = 24f;
            label.enableAutoSizing = true;
            label.fontSizeMax = 24f;
            label.fontSizeMin = 14f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.85f, 0.98f, 1f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.lineSpacing = 0f;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (CurrentState != TurnState.PlayerTurn || !IsInCombat() || IsGameOver) return;

        if (Input.GetKeyDown(endTurnHotkey))
        {
            if (DialogState.Active) return;
            GameObject dialog = GameObject.Find("Dialog");
            if (dialog != null && dialog.activeInHierarchy) return;

            if (CurrentPlayerUnit != null)
            {
                UnitMovement move = CurrentPlayerUnit.GetComponent<UnitMovement>();
                if (move != null && move.IsMoving) return;
            }

            EndPlayerTurn();
        }
    }

    private void Start()
    {
        StartPlayerTurn();
        UpdateCombatState();
    }

    // IsInCombat karede onlarca kez çağrılıyor (hurda yığınları, imleç, UI...); GetComponent'i her seferinde yapmamak için önbellek.
    private readonly System.Collections.Generic.Dictionary<Unit, EnemyAI> aiCache = new System.Collections.Generic.Dictionary<Unit, EnemyAI>();

    public bool IsInCombat()
    {
        foreach (Unit enemy in enemyUnits)
        {
            if (enemy == null) continue;
            if (!aiCache.TryGetValue(enemy, out EnemyAI ai) || ai == null)
            {
                ai = enemy.GetComponent<EnemyAI>();
                aiCache[enemy] = ai;
            }
            if (ai != null && ai.isAwake) return true;
        }
        return false;
    }

    public void UpdateCombatState()
    {
        bool inCombat = IsInCombat();

        if (endTurnButton != null)
            endTurnButton.SetActive(inCombat);

        if (inCombat && !wasInCombat)
        {
            Sfx.Play("combat_start", 0.7f, 0f, 0.5f);
            BeginPlayerRound();
        }

        if (!inCombat && wasInCombat)
        {
            EndCombat();
        }

        wasInCombat = inCombat;

        if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
    }

    private void EndCombat()
    {
        if (!IsGameOver) Sfx.Play("combat_end", 0.7f, 0f, 0.5f);
        Debug.Log("<color=green>Savaş bitti, Keşif Moduna dönüldü.</color>");

        if (enemyAlertUI != null) enemyAlertUI.SetActive(false);
        if (roundEndUI != null) roundEndUI.SetActive(false);
        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.ClearAll();

        foreach (Unit player in new List<Unit>(playerUnits))
        {
            if (player == null) continue;
            DroneUnit d = player.GetComponent<DroneUnit>();
            if (d != null && d.isAlly)
            {
                FloatingText.Spawn(player.transform.position, "KAPANDI", Color.gray);
                player.Kill();
            }
        }
        playerQueue.Clear();
        CurrentPlayerUnit = null;

        foreach (Unit player in playerUnits)
        {
            if (player != null) player.OnTurnStart();
        }
    }

    public Unit GetClosestPlayer(Vector3 fromWorldPos)
    {
        Unit best = null;
        int bestDist = int.MaxValue;
        Vector2Int from = GridManager.Instance.GetGridPosition(fromWorldPos);

        foreach (Unit p in playerUnits)
        {
            if (p == null) continue;
            Vector2Int pp = GridManager.Instance.GetGridPosition(p.transform.position);
            int d = Mathf.Abs(from.x - pp.x) + Mathf.Abs(from.y - pp.y);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    // Sahnedeki eski düz yazılar (sola yaslı, ortadan kaymış) yerine HUD temalı banner gösterilir.
    public IEnumerator ShowEnemyAlert()
    {
        if (enemyAlertUI != null) enemyAlertUI.SetActive(false);
        HudBanner.Show("DÜŞMAN HAREKETİ", HudArt.Red, 1.5f, "Bir drone seni fark etti");
        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator ShowRoundEndRoutine()
    {
        if (roundEndUI != null) roundEndUI.SetActive(false);
        if (IsInCombat())
        {
            HudBanner.Show("TUR BİTTİ", HudArt.Amber, 1.5f);
            yield return new WaitForSeconds(1.5f);
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit.isPlayerOwned && !playerUnits.Contains(unit)) playerUnits.Add(unit);
        else if (!unit.isPlayerOwned && !enemyUnits.Contains(unit)) enemyUnits.Add(unit);
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit.isPlayerOwned) playerUnits.Remove(unit);
        else enemyUnits.Remove(unit);

        playerQueue.Remove(unit);
        if (unit == CurrentPlayerUnit)
        {
            CurrentPlayerUnit = null;
            if (unit.isPlayerOwned && !IsGameOver && IsInCombat()) StartCoroutine(AdvanceNextFrame());
        }

        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.RemoveUnit(unit);

        if (!unit.isPlayerOwned) UpdateCombatState();
        else if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
    }

    private void FocusTurnOn(Unit u, bool selectForCards)
    {
        if (u == null) return;

        if (!u.isPlayerOwned && FogOfWar.Instance != null && GridManager.Instance != null &&
            !FogOfWar.Instance.IsVisible(GridManager.Instance.GetGridPosition(u.transform.position)))
            return;

        if (TacticalCamera.Instance != null && IsInCombat()) TacticalCamera.Instance.FollowUnit(u.transform);
        if (selectForCards && CardManager.Instance != null) CardManager.Instance.SetActiveUnit(u);
    }

    public void StartPlayerTurn()
    {
        CurrentState = TurnState.PlayerTurn;
        CurrentPlayerUnit = null;

        if (IsInCombat())
        {
            BeginPlayerRound();
        }
        else
        {
            foreach (Unit u in playerUnits) { if (u != null) u.OnTurnStart(); }
            if (CardManager.Instance != null) CardManager.Instance.OnPlayerTurnStart();
        }
    }

    private void BeginPlayerRound()
    {
        playerQueue.Clear();
        Unit main = null;
        foreach (Unit u in playerUnits)
        {
            if (u == null) continue;
            if (u.isMainDrone) main = u; else playerQueue.Add(u);
        }
        if (main != null) playerQueue.Add(main);

        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.BuildNewRoundQueue();
        AdvancePlayerQueue();
    }

    private void AdvancePlayerQueue()
    {
        while (playerQueue.Count > 0)
        {
            Unit u = playerQueue[0];
            playerQueue.RemoveAt(0);
            if (u == null) continue;
            BeginUnitTurn(u);
            return;
        }
        CurrentPlayerUnit = null;
        StartEnemyTurn();
    }

    private void BeginUnitTurn(Unit u)
    {
        CurrentPlayerUnit = u;
        u.OnTurnStart();
        if (u.isMainDrone && IsInCombat()) Sfx.Play("turn_start", 0.6f, 0f, 0.3f);

        if (CardManager.Instance != null)
        {
            if (u.isMainDrone) CardManager.Instance.OnPlayerTurnStart();
            CardManager.Instance.SetActiveUnit(u);
            // Aynı dost drone'un sırası arka arkaya gelince (arada ana drone oynamadan) SetActiveUnit
            // eli yenilemiyordu; kartlar önceki turdaki "oynandı" haliyle kilitli kalıyor, drone ancak
            // hareket edince (UI yenilenince) vurabiliyordu. Her tur başında eli yeniden çiz.
            CardManager.Instance.RefreshUI();
        }

        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.SetActiveUnit(u);
        FocusTurnOn(u, false);
    }

    private IEnumerator AdvanceNextFrame()
    {
        yield return null;
        if (CurrentState == TurnState.PlayerTurn && CurrentPlayerUnit == null && !IsGameOver && IsInCombat())
            AdvancePlayerQueue();
    }

    public void AddToPlayerQueue(Unit u)
    {
        if (u != null && IsInCombat() && !playerQueue.Contains(u) && u != CurrentPlayerUnit) playerQueue.Add(u);
    }

    public void EndPlayerTurn()
    {
        if (CurrentState != TurnState.PlayerTurn) return;
        if (CardManager.Instance != null) CardManager.Instance.CancelTargeting();
        if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();

        if (!IsInCombat()) return;
        AdvancePlayerQueue();
    }

    private void StartEnemyTurn()
    {
        if (IsInCombat()) Sfx.Play("turn_end", 0.55f, 0f, 0.3f);
        CurrentState = TurnState.EnemyTurn;
        if (CardManager.Instance != null) CardManager.Instance.CancelTargeting();
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        foreach (Unit enemy in new List<Unit>(enemyUnits))
        {
            if (enemy == null) continue;

            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.isAwake)
            {
                enemy.OnTurnStart();

                if (TurnOrderUI.Instance != null)
                    TurnOrderUI.Instance.SetActiveUnit(enemy);

                FocusTurnOn(enemy, false);
                yield return new WaitForSeconds(0.5f);

                yield return StartCoroutine(ai.TakeTurnRoutine());
            }
        }

        yield return StartCoroutine(ShowRoundEndRoutine());

        if (IsGameOver) yield break;
        TickRoundEffects();

        StartPlayerTurn();
    }

    private void TickRoundEffects()
    {
        if (!IsInCombat()) return;

        foreach (Unit u in new List<Unit>(playerUnits))
        {
            if (u == null) continue;
            if (u.tauntTurns > 0) u.tauntTurns--;

            DroneUnit d = u.GetComponent<DroneUnit>();
            if (d != null && d.isAlly) d.TickCharge();
        }
    }

    public bool IsGameOver { get; private set; }

    public void GameOver(bool showUI = true)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        CurrentState = TurnState.Busy;
        if (showUI) GameOverUI.Show();
    }

    public void SetBusy()
    {
        if (CurrentState != TurnState.Busy)
        {
            previousState = CurrentState;
            CurrentState = TurnState.Busy;
            if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
        }
    }

    public void ClearBusy()
    {
        if (CurrentState == TurnState.Busy)
        {
            CurrentState = previousState;
            if (CardManager.Instance != null) CardManager.Instance.RefreshUI();
        }
    }
}
