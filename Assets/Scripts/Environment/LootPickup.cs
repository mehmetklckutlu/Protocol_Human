using System.Collections.Generic;
using UnityEngine;

// Ölen düşman drone'un yere bıraktığı ganimet. Bir drone'umuz üstüne yürüyünce toplanır.
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
        Color c = blueprint != null ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.85f, 0.25f);

        glow = RuntimeArt.MakeSquare(null, "LootGlow", new Color(c.r, c.g, c.b, 0.35f), gm.CellSize * 0.9f, 2, p);
        icon = RuntimeArt.MakeSquare(null, "LootIcon", c, gm.CellSize * 0.5f, 4, p);
        icon.sprite = RuntimeArt.Star;
        glow.transform.SetParent(transform, true);
        icon.transform.SetParent(transform, true);
    }

    private void Update()
    {
        if (collected) return;

        GridManager gm = GridManager.Instance;
        bool visible = FogOfWar.Instance == null || FogOfWar.Instance.IsVisible(cell);
        if (glow != null) glow.enabled = visible;
        if (icon != null)
        {
            icon.enabled = visible;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            icon.transform.localScale = Vector3.one * gm.CellSize * Mathf.Lerp(0.45f, 0.6f, pulse);
        }

        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.CurrentState != TurnState.PlayerTurn) return; // yürürken değil, durunca topla

        Vector3 center = gm.GetWorldPosition(cell);
        foreach (Unit u in tm.playerUnits)
        {
            if (u == null) continue;
            if (Vector2.Distance(u.transform.position, center) < 0.1f) { Collect(); return; }
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
