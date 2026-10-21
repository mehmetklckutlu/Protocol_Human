using System.Collections.Generic;
using UnityEngine;

// Kart kaynaklı patlamalar (kendini patlatma). Varilin patlaması ExplosiveBarrel içinde.
public static class BlastUtil
{
    // center çevresinde Chebyshev yarıçapı kadar kareye, dost-düşman ayırmadan hasar verir.
    public static void Explode(Vector2Int center, int radius, int damage, Unit exclude)
    {
        GridManager gm = GridManager.Instance;
        if (gm == null) return;

        List<Vector2Int> cells = new List<Vector2Int>();
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int c = center + new Vector2Int(dx, dy);
                if (!gm.IsInsideGrid(c)) continue;
                cells.Add(c);
                set.Add(c);
            }

        GameObject fx = new GameObject("BlastFx");
        fx.AddComponent<BlastFx>().Play(cells, gm, new Color(1f, 0.65f, 0.1f, 0.85f));

        TurnManager tm = TurnManager.Instance;
        if (tm != null)
        {
            Damage(new List<Unit>(tm.playerUnits), set, damage, exclude);
            Damage(new List<Unit>(tm.enemyUnits), set, damage, exclude);
        }

        foreach (ExplosiveBarrel b in new List<ExplosiveBarrel>(ExplosiveBarrel.All))
            if (b != null && set.Contains(b.Cell)) b.Explode();
    }

    private static void Damage(List<Unit> units, HashSet<Vector2Int> area, int damage, Unit exclude)
    {
        foreach (Unit u in units)
        {
            if (u == null || u == exclude) continue;
            if (area.Contains(GridManager.Instance.GetGridPosition(u.transform.position)))
                u.TakeDamage(damage);
        }
    }
}
