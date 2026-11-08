using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Oyunun TÜM denge değerleri burada. Bir sayıyı değiştir, sonra menüden
/// "Protocol Human > Denge > Değerleri Uygula" de: kartlar, drone prefab'ları, varil/hurda prefab'ları
/// ve level sahnelerindeki örnekler bu tabloya göre güncellenir.
/// Ardından "Simülasyonu Çalıştır" ile sonucu test et (bkz. DengeSimulatoru.cs).
/// </summary>
public static class DengeTablosu
{
    public const string CardsFolder = "Assets/Cards";
    public const string DronePrefabs = "Assets/Prefabs/Dronlar";
    public const string BarrelPrefab = "Assets/Prefabs/Cevre/PatlayanVaril.prefab";
    public const string ScrapPrefab = "Assets/Prefabs/Cevre/HurdaYigini.prefab";
    public static readonly string[] LevelScenes = { "Assets/Scenes/Buyuk Level.unity", "Assets/Scenes/örnek level.unity" };

    // ───────────────────────── ANA DRONE ─────────────────────────
    public const int MainHP = 120;
    public const int MainMove = 5;
    public const int MainMaxCPU = 6;
    public const int MainCpuPerTurn = 2;

    // ───────────────────────── KARTLAR ─────────────────────────
    // Sabit kartlar = ana drone'a her tur gelir (birikmez)
    // Rastgele havuz = savaşta her tur 1 tane çekilir, oynanmazsa elde birikir (bedelsiz)
    // A_*  = dost drone kitleri (CPU yok, turda 1 kart, oynayınca sıra geçer)
    // P_*  = yerden/düşmandan bulunan tek kullanımlık kartlar (hurda ister)
    // BP_* = blueprint (dost drone çağırır)
    public static readonly CardSpec[] Cards =
    {
        // Ana drone sabit kartlar
        Atk("YakinSaldiri", "Yakın Saldırı",       cpu: 1, scrap: 0, dmg: R(18, 26), range: 1),
        Atk("UzakSaldiri",  "Uzak Saldırı",        cpu: 1, scrap: 0, dmg: R(10, 15), range: 6),

        // Ana drone rastgele havuz kartları
        Def("AnaKalkan",    "Kalkan",              cpu: 1, scrap: 0, shield: R(12, 18)),
        Util("Sarj",        "Şarj",                cpu: 1, scrap: 0, c => c.chargeAll = 1),
        Atk("R_AgirDarbe",  "Ağır Darbe",          cpu: 2, scrap: 0, dmg: R(28, 38), range: 1),
        Atk("R_SokAtisi",   "Şok Atışı",           cpu: 1, scrap: 0, dmg: R(6, 10),  range: 3, stun: 0.6f),
        Atk("R_DeliciAtis", "Delici Atış",         cpu: 2, scrap: 0, dmg: R(18, 24), range: 6),
        Util("R_HizliTamir","Hızlı Tamir",         cpu: 1, scrap: 0, c => c.heal = R(14, 20)),

        // Dost drone kitleri
        Atk("A_Yakin",  "Yakın Saldırı",           cpu: 0, scrap: 0, dmg: R(16, 22), range: 1),
        Atk("A_Stun",   "Stun Saldırısı",          cpu: 0, scrap: 0, dmg: R(6, 10),  range: 1, stun: 0.7f),
        Def("A_Kalkan", "Kalkan",                  cpu: 0, scrap: 0, shield: R(12, 18)),
        Atk("A_Uzak",   "Uzak Saldırı",            cpu: 0, scrap: 0, dmg: R(10, 14), range: 5),
        Atk("A_Uzak2x", "Riskli Uzak Saldırı",     cpu: 0, scrap: 0, dmg: R(24, 34), range: 5, hit: 0.5f),
        Util("A_Patlat","Kendini Patlat",          cpu: 0, scrap: 0, c => { c.selfDestruct = true; c.blastRadius = 2; c.blastDamage = R(35, 50); }),
        Atk("A_Guclu",  "Güçlü Yakın Saldırı",     cpu: 0, scrap: 0, dmg: R(30, 40), range: 1),
        Def("A_Taunt",  "Taunt",                   cpu: 0, scrap: 0, shield: R(15, 22), taunt: 2),
        Util("A_CPU",   "CPU Yenile",              cpu: 0, scrap: 0, c => c.cpuRestore = 2),

        // Bulunan kartlar (tek kullanımlık)
        Atk("P_Yakin",  "Yakın Saldırı",           cpu: 1, scrap: 2, dmg: R(20, 28), range: 1),
        Atk("P_Guclu",  "Güçlü Yakın Saldırı",     cpu: 1, scrap: 5, dmg: R(34, 46), range: 1),
        Atk("P_Stun",   "Stun Saldırısı",          cpu: 1, scrap: 3, dmg: R(8, 12),  range: 1, stun: 0.75f),
        Atk("P_Uzak",   "Uzak Saldırı",            cpu: 1, scrap: 2, dmg: R(12, 18), range: 6),
        Atk("P_Uzak2x", "Riskli Uzak Saldırı",     cpu: 1, scrap: 4, dmg: R(28, 40), range: 6, hit: 0.55f),
        Def("P_Kalkan", "Kalkan",                  cpu: 1, scrap: 2, shield: R(20, 28)),
        Def("P_Taunt",  "Taunt",                   cpu: 1, scrap: 2, shield: R(14, 20), taunt: 2),
        Util("P_CPU",   "CPU Yenile",              cpu: 0, scrap: 4, c => c.cpuRestore = 3),
        Util("P_Patlat","Kendini Patlat",          cpu: 1, scrap: 10, c => { c.selfDestruct = true; c.blastRadius = 2; c.blastDamage = R(35, 50); }),
        Util("P_Tamir", "Tamir Kiti",              cpu: 1, scrap: 3, c => c.heal = R(25, 35)),

        // Blueprint'ler (CPU + hurda)
        Summon("BP_Yakin", "Blueprint: Yakın Drone", cpu: 3, scrap: 8,  prefab: "Drone_Dost_Yakin"),
        Summon("BP_Uzak",  "Blueprint: Uzak Drone",  cpu: 3, scrap: 8,  prefab: "Drone_Dost_Uzak"),
        Summon("BP_Boss",  "Blueprint: Boss Drone",  cpu: 5, scrap: 15, prefab: "Drone_Dost_Boss"),
    };

    /// <summary>Saldırı kartlarının varsayılan kritik şansı (kritik = x1.5 hasar).</summary>
    public const float CritChance = 0.1f;

    // ───────────────────────── EL / DESTE ─────────────────────────
    public static readonly string[] FixedCards = { "YakinSaldiri", "UzakSaldiri" };

    /// <summary>Rastgele kart havuzu: (kart, ağırlık). Ağırlık büyüdükçe daha sık gelir.</summary>
    public static readonly (string card, int weight)[] RandomPool =
    {
        ("AnaKalkan",     30),
        ("R_AgirDarbe",   18),
        ("R_SokAtisi",    15),
        ("R_DeliciAtis",  15),
        ("R_HizliTamir",  15),
        ("Sarj",          10),
    };

    public const int MaxHandSize = 9;

    // ───────────────────────── DRONE'LAR ─────────────────────────
    public static readonly DroneSpec[] Drones =
    {
        // Düşmanlar
        new DroneSpec("Drone_Dusman_Yakin") { hp = 60,  move = 4, damage = R(11, 16), range = 1, target = EnemyTargetMode.Nearest,
            scrap = R(2, 4), cardDrop = 0.30f, blueprintDrop = 0.12f, drops = new[] { "P_Yakin", "P_Stun", "P_Kalkan", "P_Tamir" } },
        new DroneSpec("Drone_Dusman_Uzak")  { hp = 40,  move = 3, damage = R(9, 13),  range = 4, target = EnemyTargetMode.LowestHealth,
            scrap = R(2, 4), cardDrop = 0.35f, blueprintDrop = 0.15f, drops = new[] { "P_Uzak", "P_Uzak2x", "P_Tamir" } },
        new DroneSpec("Drone_Dusman_Boss")  { hp = 130, move = 3, damage = R(16, 23), range = 1, target = EnemyTargetMode.Nearest,
            scrap = R(6, 10), cardDrop = 0.60f, blueprintDrop = 0.35f, drops = new[] { "P_Guclu", "P_Taunt", "P_CPU", "P_Tamir" } },

        // Dostlar (blueprint ile çağrılır, şarj = kaç tur yaşar)
        new DroneSpec("Drone_Dost_Yakin") { hp = 70,  move = 4, charge = 3 },
        new DroneSpec("Drone_Dost_Uzak")  { hp = 35,  move = 3, charge = 3 },
        new DroneSpec("Drone_Dost_Boss")  { hp = 110, move = 3, charge = 5 },
    };

    // ───────────────────────── ÇEVRE ─────────────────────────
    public const int BarrelRadius = 2;
    public static readonly IntRange BarrelDamage = R(25, 35);

    public static readonly IntRange PileScrap = R(1, 4);
    public const float PileBlueprintChance = 0.10f;
    public const string PileRepairCard = "P_Tamir";
    public const float PileRepairChance = 0.30f;

    // ═════════════════════════ Uygulama ═════════════════════════

    [MenuItem("Protocol Human/Denge/Değerleri Uygula")]
    public static void ApplyAll()
    {
        var cards = ApplyCards();
        ApplyDrones(cards);
        ApplyEnvironment(cards);
        ApplyScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("<color=#7CFF8A>[Denge]</color> Tüm değerler uygulandı.");
    }

    public static CardData LoadCard(string file) => AssetDatabase.LoadAssetAtPath<CardData>($"{CardsFolder}/{file}.asset");

    private static Dictionary<string, CardData> ApplyCards()
    {
        var map = new Dictionary<string, CardData>();
        foreach (CardSpec s in Cards)
        {
            CardData c = LoadCard(s.file);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(c, $"{CardsFolder}/{s.file}.asset");
            }

            Undo.RecordObject(c, "Denge");
            c.cardName = s.name;
            c.cardType = s.type;
            c.cpuCost = s.cpu;
            c.scrapCost = s.scrap;

            // Kartın türü dışındaki alanları sıfırla: eski kartlarda kalan çöp değerler etki etmesin.
            c.damage = 0; c.range = 1; c.hitChance = 1f; c.critChance = 0f; c.stunChance = 0f; c.stunTurns = 1;
            c.shield = 0; c.tauntTurns = 0;
            c.heal = 0; c.cpuRestore = 0; c.chargeAll = 0; c.selfDestruct = false; c.blastRadius = 2; c.blastDamage = 0;

            if (s.type == CardType.Attack)
            {
                c.damage = s.damage; c.range = s.range; c.hitChance = s.hit; c.stunChance = s.stun; c.critChance = CritChance;
            }
            if (s.type == CardType.Defense) { c.shield = s.shield; c.tauntTurns = s.taunt; }
            if (s.type == CardType.Summon)
                c.summonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{DronePrefabs}/{s.prefab}.prefab");
            s.extra?.Invoke(c);

            EditorUtility.SetDirty(c);
            map[s.file] = c;
        }
        return map;
    }

    private static void ApplyDrones(Dictionary<string, CardData> cards)
    {
        foreach (DroneSpec s in Drones)
        {
            string path = $"{DronePrefabs}/{s.prefab}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Unit u = root.GetComponent<Unit>();
                u.maxHealth = s.hp;
                u.currentHealth = s.hp;
                u.maxMovement = s.move;

                DroneUnit d = root.GetComponent<DroneUnit>();
                EnemyAI ai = root.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    ai.attackDamage = s.damage;
                    ai.attackRange = s.range;
                    ai.targetMode = s.target;
                }
                if (d != null)
                {
                    if (d.isAlly) d.charge = s.charge;
                    else
                    {
                        d.scrap = s.scrap;
                        d.cardDropChance = s.cardDrop;
                        d.blueprintChance = s.blueprintDrop;
                        d.dropCards = new List<CardData>();
                        foreach (string k in s.drops) d.dropCards.Add(cards[k]);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static void ApplyEnvironment(Dictionary<string, CardData> cards)
    {
        GameObject barrel = PrefabUtility.LoadPrefabContents(BarrelPrefab);
        try
        {
            var so = new SerializedObject(barrel.GetComponent<ExplosiveBarrel>());
            so.FindProperty("explosionRadius").intValue = BarrelRadius;
            SetRange(so.FindProperty("explosionDamage"), BarrelDamage);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(barrel, BarrelPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(barrel); }

        GameObject pile = PrefabUtility.LoadPrefabContents(ScrapPrefab);
        try
        {
            ApplyPile(new SerializedObject(pile.GetComponent<ScrapPile>()), cards);
            PrefabUtility.SaveAsPrefabAsset(pile, ScrapPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(pile); }
    }

    private static void ApplyPile(SerializedObject so, Dictionary<string, CardData> cards)
    {
        SetRange(so.FindProperty("scrap"), PileScrap);
        so.FindProperty("blueprintChance").floatValue = PileBlueprintChance;
        so.FindProperty("repairCard").objectReferenceValue = cards[PileRepairCard];
        so.FindProperty("repairChance").floatValue = PileRepairChance;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Sahnelerdeki örneklerde, tabloda yönetilen alanlardaki override'ları geri alır (prefab değeri geçerli olsun).
    /// Prefab'a bağlı olmayan objelere değeri doğrudan yazar. Ana drone değerlerini de ayarlar.
    /// </summary>
    private static void ApplyScenes()
    {
        string[] unitProps = { "maxHealth", "currentHealth", "maxMovement" };
        string[] aiProps = { "attackDamage", "attackRange", "targetMode" };
        string[] droneProps = { "charge", "scrap", "cardDropChance", "blueprintChance", "dropCards" };
        string[] barrelProps = { "explosionRadius", "explosionDamage" };
        string[] pileProps = { "scrap", "blueprintChance", "repairCard", "repairChance" };

        var cards = new Dictionary<string, CardData>();
        foreach (CardSpec s in Cards) cards[s.file] = LoadCard(s.file);

        var open = new List<string>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++) open.Add(EditorSceneManager.GetSceneAt(i).path);

        foreach (string path in LevelScenes)
        {
            bool wasOpen = open.Contains(path);
            var scene = wasOpen ? EditorSceneManager.GetSceneByPath(path) : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Unit u in root.GetComponentsInChildren<Unit>(true))
                {
                    if (u.isMainDrone)
                    {
                        Undo.RecordObject(u, "Denge");
                        u.maxHealth = MainHP; u.currentHealth = MainHP; u.maxMovement = MainMove;
                        u.maxCPU = MainMaxCPU; u.cpuPerTurn = MainCpuPerTurn;
                        EditorUtility.SetDirty(u);
                        continue;
                    }
                    RevertOrNothing(u, unitProps);
                    RevertOrNothing(u.GetComponent<EnemyAI>(), aiProps);
                    RevertOrNothing(u.GetComponent<DroneUnit>(), droneProps);
                }
                foreach (ExplosiveBarrel b in root.GetComponentsInChildren<ExplosiveBarrel>(true))
                {
                    if (!RevertOrNothing(b, barrelProps))
                    {
                        var so = new SerializedObject(b);
                        so.FindProperty("explosionRadius").intValue = BarrelRadius;
                        SetRange(so.FindProperty("explosionDamage"), BarrelDamage);
                        so.ApplyModifiedProperties();
                    }
                }
                foreach (ScrapPile p in root.GetComponentsInChildren<ScrapPile>(true))
                    if (!RevertOrNothing(p, pileProps)) ApplyPile(new SerializedObject(p), cards);
                foreach (CardManager cm in root.GetComponentsInChildren<CardManager>(true))
                    ApplyHand(new SerializedObject(cm), cards);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ApplyHand(SerializedObject so, Dictionary<string, CardData> cards)
    {
        SerializedProperty fixedProp = so.FindProperty("fixedCards");
        fixedProp.arraySize = FixedCards.Length;
        for (int i = 0; i < FixedCards.Length; i++)
            fixedProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[FixedCards[i]];

        SerializedProperty pool = so.FindProperty("randomPool");
        pool.arraySize = RandomPool.Length;
        for (int i = 0; i < RandomPool.Length; i++)
        {
            SerializedProperty e = pool.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("card").objectReferenceValue = cards[RandomPool[i].card];
            e.FindPropertyRelative("weight").intValue = RandomPool[i].weight;
        }

        so.FindProperty("maxHandSize").intValue = MaxHandSize;
        so.ApplyModifiedProperties();
    }

    /// <returns>Obje bir prefab örneğiyse true (override'lar geri alındı).</returns>
    private static bool RevertOrNothing(Component c, string[] props)
    {
        if (c == null || !PrefabUtility.IsPartOfPrefabInstance(c)) return false;
        var so = new SerializedObject(c);
        foreach (string p in props)
        {
            SerializedProperty sp = so.FindProperty(p);
            if (sp != null && sp.prefabOverride) PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
        }
        return true;
    }

    private static void SetRange(SerializedProperty p, IntRange r)
    {
        p.FindPropertyRelative("min").intValue = r.min;
        p.FindPropertyRelative("max").intValue = r.max;
    }

    // ═════════════════════════ Tanım yardımcıları ═════════════════════════

    private static IntRange R(int min, int max) => new IntRange(min, max);

    private static CardSpec Atk(string file, string name, int cpu, int scrap, IntRange dmg, int range, float hit = 1f, float stun = 0f)
        => new CardSpec { file = file, name = name, type = CardType.Attack, cpu = cpu, scrap = scrap, damage = dmg, range = range, hit = hit, stun = stun };

    private static CardSpec Def(string file, string name, int cpu, int scrap, IntRange shield, int taunt = 0)
        => new CardSpec { file = file, name = name, type = CardType.Defense, cpu = cpu, scrap = scrap, shield = shield, taunt = taunt };

    private static CardSpec Util(string file, string name, int cpu, int scrap, Action<CardData> set)
        => new CardSpec { file = file, name = name, type = CardType.Utility, cpu = cpu, scrap = scrap, extra = set };

    private static CardSpec Summon(string file, string name, int cpu, int scrap, string prefab)
        => new CardSpec { file = file, name = name, type = CardType.Summon, cpu = cpu, scrap = scrap, prefab = prefab };

    public class CardSpec
    {
        public string file, name, prefab;
        public CardType type;
        public int cpu, scrap, range = 1, taunt;
        public IntRange damage, shield;
        public float hit = 1f, stun;
        public Action<CardData> extra;
    }

    public class DroneSpec
    {
        public readonly string prefab;
        public int hp, move, range = 1, charge = 3;
        public IntRange damage, scrap;
        public EnemyTargetMode target;
        public float cardDrop, blueprintDrop;
        public string[] drops = Array.Empty<string>();
        public DroneSpec(string prefab) { this.prefab = prefab; }
    }
}
