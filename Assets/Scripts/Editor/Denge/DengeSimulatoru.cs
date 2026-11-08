using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monte Carlo denge testi. Kartları ve drone prefab'larını OLDUĞU GİBİ okur, level sahnelerindeki gerçek
/// düşman gruplarını çıkarır ve binlerce kez oynatır.
///
/// Basitleştirmeler (sonuçlar oyuncu aleyhine, yani "en kötü durum"a yakın):
///  - Dost drone çağırma ve patlayan variller simüle edilmez (gerçek oyunda işler daha kolay).
///  - Mesafe tek boyutlu: düşman grubu ana drone'dan WakeDistance kare uzakta uyanır.
///  - Ana drone yakın dövüş için yaklaşır, sonra elindeki en etkili kartları oynar.
/// Rapor: proje kökünde "Denge Raporu.md".
/// </summary>
public static class DengeSimulatoru
{
    private const int Runs = 3000;
    private const int WakeDistance = 8;
    private const int MaxTurns = 40;
    private const float HealBelow = 0.55f;

    [MenuItem("Protocol Human/Denge/Simülasyonu Çalıştır")]
    public static void RunMenu()
    {
        string report = Run();
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Denge Raporu.md");
        File.WriteAllText(path, report, Encoding.UTF8);
        Debug.Log("<color=#7CFF8A>[Denge]</color> Simülasyon bitti → " + path + "\n" + report);
    }

    // ───────────────────────── Veri ─────────────────────────

    private class EnemyDef
    {
        public string key, name; public int hp, move, range; public IntRange dmg, scrap;
        public float cardDrop, bpDrop; public List<CardData> drops; public CardData blueprint;
    }

    private class Enemy
    {
        public EnemyDef def; public int hp, dist, stun;
    }

    private class Ally
    {
        public string name; public int hp, shield, charge, taunt; public List<CardData> kit;
    }

    private class Ctx
    {
        public System.Random rng;
        public int hp, maxHp, cpu, maxCpu, cpuPerTurn, move, shield, scrap;
        public bool useAllies, hardFight;
        public List<CardData> loot = new List<CardData>();
        public List<CardData> turnCards;
        public List<Ally> allies = new List<Ally>();
        // istatistik
        public int dmgTaken, healed, lootPlayed, scrapEarned, scrapSpent, blueprints, summons, turns;
    }

    private static Dictionary<char, EnemyDef> defs;
    private static List<CardData> turnCards;
    private static List<(CardData card, int weight)> randomPool;
    private static PileInfo pile;

    /// <summary>Ele kart ekler; el doluysa kart kaybolur (oyunda yerde kalır ama sim basit tutuluyor).</summary>
    private static void Give(Ctx ctx, CardData c)
    {
        if (c != null && ctx.loot.Count < DengeTablosu.MaxHandSize - turnCards.Count) ctx.loot.Add(c);
    }

    private static void DrawRandom(Ctx ctx)
    {
        bool allies = ctx.allies.Any(a => a.hp > 0);
        var pool = randomPool.Where(p => allies || p.card.chargeAll <= 0).ToList();
        int total = pool.Sum(p => p.weight);
        if (total <= 0) return;
        int roll = ctx.rng.Next(total);
        foreach (var p in pool)
        {
            if (roll < p.weight) { Give(ctx, p.card); return; }
            roll -= p.weight;
        }
    }

    private struct PileInfo { public IntRange scrap; public float repairChance, bpChance; public CardData repair; public List<CardData> blueprints; }

    public static string Run()
    {
        LoadData();
        var sb = new StringBuilder();
        sb.AppendLine("# Protocol Human – Denge Raporu");
        sb.AppendLine();
        sb.AppendLine($"Her senaryo {Runs} kez oynatıldı. Dost drone ve varil kullanılmadı (gerçek oyun bundan kolaydır).");
        sb.AppendLine();

        sb.AppendLine("## Değerler");
        sb.AppendLine();
        sb.AppendLine("| Düşman | HP | Hasar | Menzil | Hareket | Hurda | Kart % | Blueprint % |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var d in defs.Values)
            sb.AppendLine($"| {d.name} | {d.hp} | {d.dmg} | {d.range} | {d.move} | {d.scrap} | {d.cardDrop * 100:0} | {d.bpDrop * 100:0} |");
        sb.AppendLine();
        sb.AppendLine("Sabit kartlar: " + string.Join(", ", turnCards.Select(c => $"{c.cardName} ({c.MainValueText})")) + $". Her tur +1 rastgele kart, el limiti {DengeTablosu.MaxHandSize}.");
        sb.AppendLine();

        CardData bpYakin = DengeTablosu.LoadCard("BP_Yakin");
        sb.AppendLine("## Tek karşılaşmalar (tam canla)");
        sb.AppendLine();
        sb.AppendLine("\"Yalnız\" = sadece tur kartları. \"Dost ile\" = elde 1 Yakın Drone blueprint'i ve yetecek hurda var (zor savaşlarda çağrılır).");
        sb.AppendLine();
        sb.AppendLine("| Grup | Yalnız: kazanma | Yalnız: kayıp can | Yalnız: en kötü %10 | Dost ile: kazanma | Dost ile: kayıp can | Tur |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (string g in new[] { "M", "R", "MM", "MR", "RR", "MMM", "MRR", "B", "BM", "BR", "BMM" })
        {
            var (w1, l1, p1, t1) = Single(g, null);
            var (w2, l2, _, _) = Single(g, bpYakin);
            sb.AppendLine($"| {Pretty(g)} | %{w1:0} | {l1:0} | {p1} | %{w2:0} | {l2:0} | {t1:0.0} |");
        }
        sb.AppendLine();

        foreach (string scene in DengeTablosu.LevelScenes)
        {
            var (groups, piles) = ReadLevel(scene);
            sb.AppendLine($"## Kampanya: {Path.GetFileNameWithoutExtension(scene)}");
            sb.AppendLine();
            sb.AppendLine($"{groups.Count} karşılaşma, {groups.Sum(g => g.Length)} düşman, {piles} hurda yığını. Can bir sonraki savaşa taşınır.");
            sb.AppendLine();
            sb.AppendLine("**Dost drone'lar kullanılarak** (bulunan blueprint'ler zor savaşlarda çağrılır):");
            sb.AppendLine();
            Campaign(sb, groups, piles, true);
            sb.AppendLine("**Hiç dost drone kullanmadan** (en kötü durum):");
            sb.AppendLine();
            Campaign(sb, groups, piles, false);
        }
        return sb.ToString();
    }

    private static (float win, float lost, int p90, float turns) Single(string g, CardData blueprint)
    {
        var lost = new List<int>(); int wins = 0; int turns = 0;
        for (int i = 0; i < Runs; i++)
        {
            var ctx = NewCtx(i);
            if (blueprint != null) { ctx.useAllies = true; ctx.loot.Add(blueprint); ctx.scrap = blueprint.scrapCost; }
            if (Fight(ctx, g)) wins++;
            lost.Add(ctx.maxHp - Mathf.Max(0, ctx.hp));
            turns += ctx.turns;
        }
        lost.Sort();
        return (wins * 100f / Runs, (float)lost.Average(), lost[(int)(lost.Count * 0.9f)], turns / (float)Runs);
    }

    private static string Pretty(string g)
    {
        var parts = g.GroupBy(c => c).Select(x => $"{x.Count()}x {defs[x.Key].name}");
        return string.Join(" + ", parts);
    }

    private static Ctx NewCtx(int seed)
    {
        return new Ctx
        {
            rng = new System.Random(seed * 7919 + 17),
            hp = DengeTablosu.MainHP, maxHp = DengeTablosu.MainHP, maxCpu = DengeTablosu.MainMaxCPU,
            cpu = DengeTablosu.MainMaxCPU, cpuPerTurn = DengeTablosu.MainCpuPerTurn, move = DengeTablosu.MainMove,
            turnCards = turnCards,
        };
    }

    private static void Campaign(StringBuilder sb, List<string> groups, int piles, bool allies)
    {
        int wins = 0; var deathAt = new int[groups.Count];
        long endHp = 0, scrapE = 0, scrapS = 0, healed = 0, lootPlayed = 0, bps = 0, summons = 0;
        for (int r = 0; r < Runs; r++)
        {
            var ctx = NewCtx(r + 100000);
            ctx.useAllies = allies;
            bool alive = true;
            for (int i = 0; i < groups.Count; i++)
            {
                // Bu karşılaşmadan önce araştırılan hurda yığınları (eşit dağıtılmış)
                int found = (i + 1) * piles / groups.Count - i * piles / groups.Count;
                for (int k = 0; k < found; k++)
                {
                    int s = Roll(ctx, pile.scrap); ctx.scrap += s; ctx.scrapEarned += s;
                    if (pile.repair != null && ctx.rng.NextDouble() < pile.repairChance) Give(ctx, pile.repair);
                    if (pile.blueprints.Count > 0 && ctx.rng.NextDouble() < pile.bpChance)
                    { ctx.blueprints++; Give(ctx, pile.blueprints[ctx.rng.Next(pile.blueprints.Count)]); }
                }
                ctx.cpu = ctx.maxCpu; // keşifte CPU dolar
                if (!Fight(ctx, groups[i])) { alive = false; deathAt[i]++; break; }
            }
            if (alive) { wins++; endHp += ctx.hp; }
            scrapE += ctx.scrapEarned; scrapS += ctx.scrapSpent; healed += ctx.healed; lootPlayed += ctx.lootPlayed;
            bps += ctx.blueprints; summons += ctx.summons;
        }

        sb.AppendLine($"- **Bitirme oranı: %{wins * 100f / Runs:0.0}**");
        if (wins > 0) sb.AppendLine($"- Bitirenlerin kalan canı (ort.): {endHp / (float)wins:0} / {DengeTablosu.MainHP}");
        sb.AppendLine($"- Kazanılan hurda (ort.): {scrapE / (float)Runs:0}, harcanan: {scrapS / (float)Runs:0}");
        sb.AppendLine($"- Tamir ile geri kazanılan can (ort.): {healed / (float)Runs:0}");
        sb.AppendLine($"- Oynanan bulunmuş kart (ort.): {lootPlayed / (float)Runs:0.0}, bulunan blueprint (ort.): {bps / (float)Runs:0.0}, çağrılan dost drone (ort.): {summons / (float)Runs:0.0}");
        var worst = deathAt.Select((n, i) => (n, i)).Where(x => x.n > 0).OrderByDescending(x => x.n).Take(4).ToList();
        if (worst.Count > 0)
            sb.AppendLine("- En çok ölünen karşılaşmalar: " + string.Join(", ", worst.Select(x => $"#{x.i + 1} {Pretty(groups[x.i])} (%{x.n * 100f / Runs:0.0})")));
        sb.AppendLine();
    }

    // ───────────────────────── Savaş ─────────────────────────

    private static bool Fight(Ctx ctx, string group)
    {
        var enemies = group.Select((ch, i) => new Enemy { def = defs[ch], hp = defs[ch].hp, dist = WakeDistance + i % 2 }).ToList();
        ctx.turns = 0;
        ctx.allies.Clear();
        ctx.hardFight = group.Contains('B') || group.Length >= 3;
        // Savaş başı: oyuncu turu, CPU = min(max, cpu + perTurn)
        while (ctx.turns < MaxTurns)
        {
            ctx.turns++;
            PlayerTurn(ctx, enemies);
            AlliesTurn(ctx, enemies);
            enemies.RemoveAll(e => e.hp <= 0);
            if (enemies.Count == 0) return true; // savaş bitince dost drone'lar kapanır

            EnemyTurn(ctx, enemies);
            if (ctx.hp <= 0) return false;

            // tur sonu: şarj ve taunt azalır
            foreach (var a in ctx.allies) { a.charge--; if (a.taunt > 0) a.taunt--; }
            ctx.allies.RemoveAll(a => a.hp <= 0 || a.charge <= 0);
        }
        return false;
    }

    /// <summary>Zor savaşta elde blueprint varsa ve parası yetiyorsa en güçlü dost drone'u çağırır.</summary>
    private static void TrySummon(Ctx ctx)
    {
        CardData pick = ctx.loot.Where(c => c.cardType == CardType.Summon && c.summonPrefab != null
                                            && c.cpuCost <= ctx.cpu && c.scrapCost <= ctx.scrap)
                                .OrderByDescending(c => c.scrapCost).FirstOrDefault();
        if (pick == null) return;
        ctx.cpu -= pick.cpuCost;
        ctx.scrap -= pick.scrapCost; ctx.scrapSpent += pick.scrapCost;
        ctx.loot.Remove(pick); ctx.lootPlayed++; ctx.summons++;
        var u = pick.summonPrefab.GetComponent<Unit>(); var d = pick.summonPrefab.GetComponent<DroneUnit>();
        ctx.allies.Add(new Ally { name = pick.summonPrefab.name, hp = u.maxHealth, charge = d.charge, kit = d.kit.Where(k => k != null).ToList() });
    }

    private static void AlliesTurn(Ctx ctx, List<Enemy> enemies)
    {
        foreach (var a in ctx.allies)
        {
            a.shield = 0;
            if (a.hp <= 0 || !enemies.Any(e => e.hp > 0)) continue;
            CardData best = null; Enemy target = null; float score = 0f;
            foreach (var c in a.kit)
            {
                if (c.selfDestruct) continue;
                if (c.cardType == CardType.Attack)
                {
                    var t = enemies.Where(e => e.hp > 0 && e.dist <= c.range).OrderBy(e => e.hp).FirstOrDefault();
                    if (t == null) continue;
                    float s = Mathf.Min(c.damage.Average * c.hitChance, t.hp + 5) + c.stunChance * 8f;
                    if (s > score) { score = s; best = c; target = t; }
                }
                else if (c.cardType == CardType.Defense && c.tauntTurns > 0 && ctx.hp < ctx.maxHp * 0.5f && 20f > score)
                { score = 20f; best = c; target = null; }
                else if (c.cpuRestore > 0 && ctx.cpu <= 1 && 6f > score) { score = 6f; best = c; target = null; }
            }
            if (best == null) continue;
            if (best.cardType == CardType.Attack)
            {
                if (ctx.rng.NextDouble() <= best.hitChance)
                {
                    int dmg = Roll(ctx, best.damage);
                    if (ctx.rng.NextDouble() < best.critChance) dmg = Mathf.RoundToInt(dmg * CardData.CritMultiplier);
                    if (best.stunChance > 0 && ctx.rng.NextDouble() <= best.stunChance) target.stun = Mathf.Max(target.stun, best.stunTurns);
                    target.hp -= dmg;
                    if (target.hp <= 0) Loot(ctx, target.def);
                }
            }
            else if (best.cardType == CardType.Defense) { a.shield += Roll(ctx, best.shield); a.taunt = Mathf.Max(a.taunt, best.tauntTurns); }
            else ctx.cpu = Mathf.Min(ctx.maxCpu, ctx.cpu + best.cpuRestore);
        }
    }

    private static void PlayerTurn(Ctx ctx, List<Enemy> enemies)
    {
        ctx.cpu = Mathf.Min(ctx.maxCpu, ctx.cpu + ctx.cpuPerTurn);
        ctx.shield = 0;
        DrawRandom(ctx);
        var usedTurnCards = new HashSet<CardData>();
        if (ctx.useAllies && ctx.hardFight && ctx.allies.Count == 0) TrySummon(ctx);

        // Hareket: en düşük canlı düşmana yakın dövüş mesafesine gir (ulaşabiliyorsa).
        Enemy focus = enemies.OrderBy(e => e.hp).ThenBy(e => e.dist).First();
        int step = Mathf.Min(ctx.move, Mathf.Max(0, focus.dist - 1));
        foreach (var e in enemies) e.dist = Mathf.Max(1, e.dist - step);

        bool Threat() => enemies.Any(e => e.hp > 0 && e.stun == 0 && e.dist <= e.def.range + e.def.move * 2);

        // 1) Can düşükse tamir
        if (ctx.hp <= ctx.maxHp * HealBelow) TryPlay(ctx, enemies, c => !c.heal.IsZero, usedTurnCards);

        // 2) Can çok düşük ve tehdit varsa önce kalkan
        if (ctx.hp <= ctx.maxHp * 0.35f && Threat()) TryPlay(ctx, enemies, c => c.cardType == CardType.Defense, usedTurnCards);

        // 3) Saldırılar (en yüksek beklenen hasar önce)
        while (enemies.Any(e => e.hp > 0) && TryPlay(ctx, enemies, c => c.cardType == CardType.Attack, usedTurnCards)) { }

        // 4) Kalan CPU ile kalkan (tehdit varsa)
        if (enemies.Any(e => e.hp > 0) && Threat()) TryPlay(ctx, enemies, c => c.cardType == CardType.Defense, usedTurnCards);

        // 5) CPU kartı: bir sonraki tura yedek
        if (ctx.cpu <= 1) TryPlay(ctx, enemies, c => c.cpuRestore > 0, usedTurnCards);

        // 6) El dolmak üzereyse artan CPU ile bedelsiz kartları oynayıp yer aç (ganimet ele sığsın)
        int cap = DengeTablosu.MaxHandSize - turnCards.Count;
        while (ctx.cpu > 0 && ctx.loot.Count >= cap - 1 &&
               TryPlay(ctx, enemies, c => c.scrapCost == 0 && c.cardType != CardType.Summon && !turnCards.Contains(c), usedTurnCards, dumping: true)) { }
    }

    private static bool TryPlay(Ctx ctx, List<Enemy> enemies, System.Func<CardData, bool> filter, HashSet<CardData> used, bool dumping = false)
    {
        if (dumping)
        {
            // Yer açmak için: oynanabilen en ucuz bedelsiz kartı (hedef gerekmeyen) at.
            CardData dump = ctx.loot.Where(c => filter(c) && c.cpuCost <= ctx.cpu &&
                                                (c.cardType != CardType.Attack || enemies.Any(e => e.hp > 0 && e.dist <= c.range)))
                                    .OrderBy(c => c.cpuCost).FirstOrDefault();
            if (dump == null) return false;
            ctx.cpu -= dump.cpuCost; ctx.loot.Remove(dump); ctx.lootPlayed++;
            if (dump.cardType == CardType.Defense) ctx.shield += Roll(ctx, dump.shield);
            else if (!dump.heal.IsZero) { int h = Mathf.Min(Roll(ctx, dump.heal), ctx.maxHp - ctx.hp); ctx.hp += h; ctx.healed += h; }
            else if (dump.cardType == CardType.Attack)
            {
                var t = enemies.Where(e => e.hp > 0 && e.dist <= dump.range).OrderBy(e => e.hp).First();
                t.hp -= Roll(ctx, dump.damage);
                if (t.hp <= 0) Loot(ctx, t.def);
            }
            return true;
        }

        CardData best = null; Enemy bestTarget = null; float bestScore = 0f; bool bestIsLoot = false;

        void Consider(CardData c, bool isLoot)
        {
            if (c == null || !filter(c) || c.cpuCost > ctx.cpu || c.scrapCost > ctx.scrap) return;
            if (c.selfDestruct || c.cardType == CardType.Summon) return;
            if (c.cardType == CardType.Attack)
            {
                Enemy t = enemies.Where(e => e.hp > 0 && e.dist <= c.range).OrderBy(e => e.hp).FirstOrDefault();
                if (t == null) return;
                float exp = c.damage.Average * c.hitChance * (1f + c.critChance * (CardData.CritMultiplier - 1f));
                // Bulunan kartı küçük işlere harcama; turda bedava kart önce gelsin
                if (isLoot && t.hp < exp * 0.6f) return;
                float score = Mathf.Min(exp, t.hp + 5) + (isLoot ? -0.5f : 0.5f) + c.stunChance * 8f;
                if (score > bestScore) { bestScore = score; best = c; bestTarget = t; bestIsLoot = isLoot; }
            }
            else
            {
                float score = c.cardType == CardType.Defense ? c.shield.Average : !c.heal.IsZero ? c.heal.Average : 1f;
                if (isLoot && c.cardType == CardType.Defense) score -= 3f;
                if (score > bestScore) { bestScore = score; best = c; bestTarget = null; bestIsLoot = isLoot; }
            }
        }

        foreach (var c in ctx.turnCards) if (!used.Contains(c)) Consider(c, false);
        foreach (var c in ctx.loot.Distinct()) Consider(c, true);
        if (best == null) return false;

        ctx.cpu -= best.cpuCost;
        if (best.scrapCost > 0) { ctx.scrap -= best.scrapCost; ctx.scrapSpent += best.scrapCost; }
        if (bestIsLoot) { ctx.loot.Remove(best); ctx.lootPlayed++; } else used.Add(best);

        switch (best.cardType)
        {
            case CardType.Attack:
                if (ctx.rng.NextDouble() <= best.hitChance)
                {
                    int dmg = Roll(ctx, best.damage);
                    if (ctx.rng.NextDouble() < best.critChance) dmg = Mathf.RoundToInt(dmg * CardData.CritMultiplier);
                    if (best.stunChance > 0 && ctx.rng.NextDouble() <= best.stunChance) bestTarget.stun = Mathf.Max(bestTarget.stun, best.stunTurns);
                    bestTarget.hp -= dmg;
                    if (bestTarget.hp <= 0) Loot(ctx, bestTarget.def);
                }
                break;
            case CardType.Defense:
                ctx.shield += Roll(ctx, best.shield);
                break;
            default:
                if (!best.heal.IsZero) { int h = Mathf.Min(Roll(ctx, best.heal), ctx.maxHp - ctx.hp); ctx.hp += h; ctx.healed += h; }
                if (best.cpuRestore > 0) ctx.cpu = Mathf.Min(ctx.maxCpu, ctx.cpu + best.cpuRestore);
                break;
        }
        return true;
    }

    private static void Loot(Ctx ctx, EnemyDef d)
    {
        int s = Roll(ctx, d.scrap); ctx.scrap += s; ctx.scrapEarned += s;
        if (d.drops.Count > 0 && ctx.rng.NextDouble() < d.cardDrop)
        {
            var c = d.drops[ctx.rng.Next(d.drops.Count)];
            if (c != null) Give(ctx, c);
        }
        if (d.blueprint != null && ctx.rng.NextDouble() < d.bpDrop) { ctx.blueprints++; Give(ctx, d.blueprint); }
    }

    private static void EnemyTurn(Ctx ctx, List<Enemy> enemies)
    {
        foreach (var e in enemies)
        {
            if (e.hp <= 0) continue;
            if (e.stun > 0) { e.stun--; continue; }
            int cpu = 2; bool attacked = false;
            while (cpu > 0)
            {
                if (e.dist <= e.def.range && !attacked)
                {
                    cpu--; attacked = true;
                    int dmg = Roll(ctx, e.def.dmg);
                    Ally a = PickAllyTarget(ctx, e);
                    if (a != null)
                    {
                        int ab = Mathf.Min(a.shield, dmg); a.shield -= ab; a.hp -= dmg - ab;
                        continue;
                    }
                    int absorbed = Mathf.Min(ctx.shield, dmg);
                    ctx.shield -= absorbed; dmg -= absorbed;
                    ctx.hp -= dmg; ctx.dmgTaken += dmg;
                    if (ctx.hp <= 0) return;
                }
                else if (e.dist > e.def.range)
                {
                    cpu--;
                    e.dist = Mathf.Max(e.def.range, e.dist - e.def.move);
                }
                else break;
            }
        }
    }

    /// <summary>Düşman dost drone'a mı saldırır? Taunt → her zaman; yakın düşman → yarı yarıya (hangisi yakınsa); uzak → canı düşük olana.</summary>
    private static Ally PickAllyTarget(Ctx ctx, Enemy e)
    {
        var alive = ctx.allies.Where(a => a.hp > 0).ToList();
        if (alive.Count == 0) return null;
        var taunter = alive.FirstOrDefault(a => a.taunt > 0);
        if (taunter != null) return taunter;
        if (e.def.range > 1)
        {
            var low = alive.OrderBy(a => a.hp).First();
            return low.hp < ctx.hp ? low : null;
        }
        return ctx.rng.NextDouble() < 0.5 ? alive[ctx.rng.Next(alive.Count)] : null;
    }

    private static int Roll(Ctx ctx, IntRange r) => ctx.rng.Next(r.min, Mathf.Max(r.min, r.max) + 1);

    // ───────────────────────── Yükleme ─────────────────────────

    private static void LoadData()
    {
        defs = new Dictionary<char, EnemyDef>
        {
            ['M'] = LoadEnemy("Drone_Dusman_Yakin", "Yakın"),
            ['R'] = LoadEnemy("Drone_Dusman_Uzak", "Uzak"),
            ['B'] = LoadEnemy("Drone_Dusman_Boss", "Boss"),
        };
        turnCards = DengeTablosu.FixedCards.Select(DengeTablosu.LoadCard).Where(c => c != null).ToList();
        randomPool = DengeTablosu.RandomPool.Select(p => (DengeTablosu.LoadCard(p.card), p.weight)).Where(p => p.Item1 != null).ToList();

        var pileGo = AssetDatabase.LoadAssetAtPath<GameObject>(DengeTablosu.ScrapPrefab);
        var so = new SerializedObject(pileGo.GetComponent<ScrapPile>());
        pile = new PileInfo
        {
            scrap = new IntRange(so.FindProperty("scrap.min").intValue, so.FindProperty("scrap.max").intValue),
            repairChance = so.FindProperty("repairChance").floatValue,
            repair = so.FindProperty("repairCard").objectReferenceValue as CardData,
            bpChance = so.FindProperty("blueprintChance").floatValue,
            blueprints = new List<CardData>(),
        };
        var bps = so.FindProperty("blueprints");
        for (int i = 0; i < bps.arraySize; i++)
            if (bps.GetArrayElementAtIndex(i).objectReferenceValue is CardData bp) pile.blueprints.Add(bp);
    }

    private static EnemyDef LoadEnemy(string prefab, string name)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{DengeTablosu.DronePrefabs}/{prefab}.prefab");
        var u = go.GetComponent<Unit>(); var ai = go.GetComponent<EnemyAI>(); var d = go.GetComponent<DroneUnit>();
        return new EnemyDef
        {
            key = prefab, name = name, hp = u.maxHealth, move = u.maxMovement, range = ai.attackRange, dmg = ai.attackDamage,
            scrap = d.scrap, cardDrop = d.cardDropChance, bpDrop = d.blueprint != null ? d.blueprintChance : 0f, blueprint = d.blueprint,
            drops = d.dropCards.Where(c => c != null && !(c.selfDestruct)).ToList(),
        };
    }

    /// <summary>Sahnedeki düşmanları yakınlığa göre gruplar (6 karelik küme), başlangıca uzaklığa göre sıralar.</summary>
    private static (List<string> groups, int piles) ReadLevel(string path)
    {
        bool wasOpen = false;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++) if (EditorSceneManager.GetSceneAt(i).path == path) wasOpen = true;
        var scene = wasOpen ? EditorSceneManager.GetSceneByPath(path) : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        var en = new List<(char t, Vector2Int p)>();
        Vector2Int start = Vector2Int.zero; int piles = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var u in root.GetComponentsInChildren<Unit>(true))
            {
                var p = new Vector2Int(Mathf.RoundToInt(u.transform.position.x), Mathf.RoundToInt(u.transform.position.y));
                if (u.isMainDrone) { start = p; continue; }
                var d = u.GetComponent<DroneUnit>();
                if (d == null || d.isAlly) continue;
                en.Add((d.droneType == DroneType.Melee ? 'M' : d.droneType == DroneType.Ranged ? 'R' : 'B', p));
            }
            piles += root.GetComponentsInChildren<ScrapPile>(true).Length;
        }
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);

        int n = en.Count; var parent = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (Mathf.Max(Mathf.Abs(en[i].p.x - en[j].p.x), Mathf.Abs(en[i].p.y - en[j].p.y)) <= 6) parent[Find(i)] = Find(j);

        int Dist(int i) => Mathf.Abs(en[i].p.x - start.x) + Mathf.Abs(en[i].p.y - start.y);
        var groups = Enumerable.Range(0, n).GroupBy(Find)
            .OrderBy(g => g.Min(Dist))
            .Select(g => new string(g.Select(i => en[i].t).OrderBy(c => c).ToArray()))
            .ToList();
        return (groups, piles);
    }
}
