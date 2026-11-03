using System.Collections.Generic;
using UnityEngine;

public class LootPickup : MonoBehaviour
{
    private int scrap;
    private List<CardData> cards = new List<CardData>();
    private CardData blueprint;

    private Vector2Int cell;
    private SpriteRenderer glow;
    private SpriteRenderer icon;
    private bool collected;

    public static void Spawn(Vector3 worldPos, int scrap, List<CardData> cards, CardData blueprint)
    {
        if (scrap <= 0 && (cards == null || cards.Count == 0)) return;
        if (GridManager.Instance == null) return;

        GameObject go = new GameObject("Ganimet");
        LootPickup l = go.AddComponent<LootPickup>();
        l.scrap = scrap;
        if (cards != null) l.cards.AddRange(cards);
        l.blueprint = blueprint;
        l.cell = GridManager.Instance.GetGridPosition(worldPos);
        go.transform.position = GridManager.Instance.GetWorldPosition(l.cell);
    }

    private void Start()
    {
        GridManager gm = GridManager.Instance;
        Vector3 p = gm.GetWorldPosition(cell);
        Color glowColor = blueprint != null ? new Color(0.35f, 0.75f, 1f, 0.28f) : new Color(1f, 0.78f, 0.2f, 0.22f);

        glow = RuntimeArt.MakeSquare(null, "LootGlow", glowColor, gm.CellSize * 0.75f, 2, p);
        glow.sprite = RuntimeArt.Ring;

        Sprite scrapSprite = RuntimeArt.ScrapIcon;
        Color iconColor = scrapSprite != null
            ? (blueprint != null ? new Color(0.75f, 0.92f, 1f, 1f) : Color.white)
            : (blueprint != null ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.85f, 0.25f));

        icon = RuntimeArt.MakeSquare(null, "LootIcon", iconColor, gm.CellSize * 0.6f, 4, p);
        icon.sprite = scrapSprite != null ? scrapSprite : RuntimeArt.Star;
        glow.transform.SetParent(transform, true);
        icon.transform.SetParent(transform, true);
    }

    private void Update()
    {
        if (collected) return;

        GridManager gm = GridManager.Instance;
        Vector3 basePos = gm.GetWorldPosition(cell);
        bool visible = FogOfWar.Instance == null || FogOfWar.Instance.IsVisible(cell);
        if (glow != null) glow.enabled = visible;
        if (icon != null)
        {
            icon.enabled = visible;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            icon.transform.localScale = Vector3.one * gm.CellSize * Mathf.Lerp(0.55f, 0.68f, pulse);
            icon.transform.position = basePos + new Vector3(0f, Mathf.Sin(Time.time * 3.5f) * 0.05f, 0f);
        }

        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.CurrentState != TurnState.PlayerTurn) return;

        foreach (Unit u in tm.playerUnits)
        {
            if (u == null) continue;
            if (Vector2.Distance(u.transform.position, basePos) < 0.1f) { Collect(); return; }
        }
    }

    private void Collect()
    {
        collected = true;
        Vector3 pos = transform.position;
        int line = 0;

        if (scrap > 0)
        {
            ScrapInventory.Add(scrap);
            FloatingText.Spawn(pos, "+" + scrap + " hurda", new Color(1f, 0.9f, 0.3f), line++);
        }

        foreach (CardData c in cards)
        {
            if (c == null || CardManager.Instance == null) continue;
            CardManager.Instance.AddCard(c);
            bool isBp = c == blueprint;
            FloatingText.Spawn(pos, (isBp ? "BLUEPRINT: " : "Kart: ") + c.cardName,
                isBp ? new Color(0.5f, 0.8f, 1f) : new Color(0.6f, 1f, 0.6f), line++);
        }

        Destroy(gameObject);
    }
}
