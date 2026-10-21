#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Unity üst menüsünde: Level > Drone Sistemini Kur
// - Kare sprite'ı, tüm kart assetlerini ve prefab'ları oluşturur (varsa dokunmaz: Inspector ayarların korunur)
// - Sahnedeki eski düşmanları / hurda yığınını / varilleri prefab örnekleriyle değiştirir
// - CardManager'a tur kartlarını bağlar, ana drone'u işaretler
// Birden fazla kez çalıştırmak güvenlidir.
public static class EnvironmentSetup
{
    private const string CardsDir = "Assets/Cards";
    private const string DronesDir = "Assets/Prefabs/Dronlar";
    private const string EnvDir = "Assets/Prefabs/Cevre";
    private const string SpriteDir = "Assets/Sprites";
    private const int PaidCost = 10; // yerden bulunan kartların varsayılan hurda bedeli

    private static Sprite square;
    private static readonly Dictionary<string, CardData> cards = new Dictionary<string, CardData>();

    // Eski "Temel Saldırı / Temel Savunma" kartlarını siler; ana drone: yakın + uzak saldırı, kalkan, şarj ile başlar
    [MenuItem("Level/Ana Drone Kartlarını Düzelt")]
    public static void FixMainCards()
    {
        BuildCards();

        CardManager cm = UnityEngine.Object.FindFirstObjectByType<CardManager>();
        if (cm != null)
        {
            SerializedObject so = new SerializedObject(cm);
            string[] keys = { "AnaKalkan", "Sarj", "YakinSaldiri", "UzakSaldiri" };
            SerializedProperty tc = so.FindProperty("turnCards");
            tc.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++) tc.GetArrayElementAtIndex(i).objectReferenceValue = cards[keys[i]];
            SerializedProperty sc = so.FindProperty("startingCards");
            if (sc != null) sc.arraySize = 0;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cm);
        }

        AssetDatabase.DeleteAsset(CardsDir + "/TemelSaldiri.asset");
        AssetDatabase.DeleteAsset(CardsDir + "/TemelSavunma.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("Ana drone kartları düzeltildi: Kalkan, Şarj, Yakın Saldırı, Uzak Saldırı.");
    }

    [MenuItem("Level/Drone Sistemini Kur")]
    public static void Run()
    {
        foreach (string d in new[] { CardsDir, "Assets/Prefabs", DronesDir, EnvDir, SpriteDir })
            EnsureFolder(d);

        square = EnsureSquareSprite();
        BuildCards();

        // Dost drone prefab'ları (kaynak: sahnedeki Player)
        GameObject allyMelee = MakeDrone("Drone_Dost_Yakin", "Dost Yakin Drone", DroneType.Melee, true, new Color(0.2f, 0.45f, 1f), 3, 4, 80, 0,
            new[] { "A_Yakin", "A_Stun", "A_Kalkan" }, null, null);
        GameObject allyRanged = MakeDrone("Drone_Dost_Uzak", "Dost Uzak Drone", DroneType.Ranged, true, new Color(1f, 0.9f, 0.1f), 3, 3, 30, 0,
            new[] { "A_Patlat", "A_Uzak", "A_Uzak2x" }, null, null);
        GameObject allyBoss = MakeDrone("Drone_Dost_Boss", "Dost Boss Drone", DroneType.Boss, true, new Color(0.06f, 0.06f, 0.08f), 5, 3, 90, 0,
            new[] { "A_Guclu", "A_Taunt", "A_CPU" }, null, null);

        // Blueprint kartları (çağırma bedeli: CPU + hurda)
        CardData bpMelee = Blueprint("BP_Yakin", "Blueprint: Yakın Drone", 3, allyMelee);
        CardData bpRanged = Blueprint("BP_Uzak", "Blueprint: Uzak Drone", 3, allyRanged);
        CardData bpBoss = Blueprint("BP_Boss", "Blueprint: Boss Drone", 5, allyBoss);

        // Düşman prefab'ları (kaynak: sahnedeki Enemy)
        GameObject enMelee = MakeDrone("Drone_Dusman_Yakin", "Dusman Yakin Drone", DroneType.Melee, false, new Color(0.85f, 0.15f, 0.15f), 0, 4, 100, 20,
            null, new[] { "P_Yakin", "P_Stun", "P_Kalkan" }, bpMelee);
        GameObject enRanged = MakeDrone("Drone_Dusman_Uzak", "Dusman Uzak Drone", DroneType.Ranged, false, new Color(1f, 0.5f, 0.1f), 0, 3, 40, 15,
            null, new[] { "P_Patlat", "P_Uzak", "P_Uzak2x" }, bpRanged);
        GameObject enBoss = MakeDrone("Drone_Dusman_Boss", "Dusman Boss Drone", DroneType.Boss, false, new Color(0.5f, 0.08f, 0.35f), 0, 3, 90, 45,
            null, new[] { "P_Guclu", "P_Taunt", "P_CPU" }, bpBoss);

        // Çevre prefab'ları
        GameObject pilePrefab = MakePile(new List<CardData> { bpMelee, bpRanged, bpBoss });
        GameObject barrelPrefab = MakeBarrel();

        // Sahne
        ConfigureScene(enMelee, enRanged, enBoss, pilePrefab, barrelPrefab);

        // Eski müttefik kartı artık kullanılmıyor
        AssetDatabase.DeleteAsset(CardsDir + "/MuttefikCagir.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=green>Drone sistemi kuruldu, sahne kaydedildi.</color>");
    }

    // ---------- Sprite ----------

    private static Sprite EnsureSquareSprite()
    {
        string path = SpriteDir + "/Kare.png";
        if (!File.Exists(path))
        {
            // 32x32: kenarlarda 4 piksel şeffaf boşluk, ortası beyaz kare
            Texture2D t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    t.SetPixel(x, y, (x >= 4 && x < 28 && y >= 4 && y < 28) ? Color.white : Color.clear);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);

            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- Kartlar ----------

    private static CardData Card(string file, string name, CardType type, int cpu, int scrap, Action<CardData> init)
    {
        string path = CardsDir + "/" + file + ".asset";
        CardData c = AssetDatabase.LoadAssetAtPath<CardData>(path);
        if (c == null)
        {
            c = ScriptableObject.CreateInstance<CardData>();
            c.cardName = name;
            c.cardType = type;
            c.cpuCost = cpu;
            c.scrapCost = scrap;
            if (init != null) init(c);
            AssetDatabase.CreateAsset(c, path);
        }
        cards[file] = c;
        return c;
    }

    private static void BuildCards()
    {
        cards.Clear();

        // Ana drone: her tur elde olan kartlar (bedelsiz)
        Card("AnaKalkan", "Kalkan", CardType.Defense, 1, 0, c => c.shield = 15);
        Card("Sarj", "Şarj", CardType.Utility, 1, 0, c => c.chargeAll = 1);
        Card("YakinSaldiri", "Yakın Saldırı", CardType.Attack, 1, 0, c => { c.damage = 20; c.range = 1; });
        Card("UzakSaldiri", "Uzak Saldırı", CardType.Attack, 1, 0, c => { c.damage = 15; c.range = 4; });

        // Yerden bulunan / düşen kartlar (hurda bedelli, tek kullanımlık)
        BuildFamily("P_", PaidCost, 1);
        // Dost drone kitleri (bedelsiz, CPU yok)
        BuildFamily("A_", 0, 0);
    }

    // Aynı efektlerin farklı bedelli sürümleri: P_ (yerden, hurda bedelli) ve A_ (dost drone kiti)
    private static void BuildFamily(string prefix, int scrap, int cpu)
    {
        Card(prefix + "Kalkan", "Kalkan", CardType.Defense, cpu, scrap, c => c.shield = 15);
        Card(prefix + "Yakin", "Yakın Saldırı", CardType.Attack, cpu, scrap, c => { c.damage = 20; c.range = 1; });
        Card(prefix + "Uzak", "Uzak Saldırı", CardType.Attack, cpu, scrap, c => { c.damage = 15; c.range = 4; });
        Card(prefix + "Stun", "Stun Saldırısı", CardType.Attack, cpu, scrap, c => { c.damage = 10; c.range = 1; c.stunChance = 0.7f; c.stunTurns = 1; });
        Card(prefix + "Guclu", "Güçlü Yakın Saldırı", CardType.Attack, cpu, scrap, c => { c.damage = 45; c.range = 1; });
        Card(prefix + "Uzak2x", "Riskli Uzak Saldırı", CardType.Attack, cpu, scrap, c => { c.damage = 30; c.range = 4; c.hitChance = 0.5f; });
        Card(prefix + "Taunt", "Taunt", CardType.Defense, cpu, scrap, c => { c.shield = 15; c.tauntTurns = 2; });
        Card(prefix + "CPU", "CPU Yenile", CardType.Utility, 0, scrap, c => c.cpuRestore = 3);
        Card(prefix + "Patlat", "Kendini Patlat", CardType.Utility, cpu, scrap, c => { c.selfDestruct = true; c.blastRadius = 2; c.blastDamage = 50; });
    }

    private static CardData Blueprint(string file, string name, int cpu, GameObject dronePrefab)
    {
        CardData c = Card(file, name, CardType.Summon, cpu, PaidCost, null);
        if (c.summonPrefab == null)
        {
            c.summonPrefab = dronePrefab;
            EditorUtility.SetDirty(c);
        }
        return c;
    }

    // ---------- Drone prefab'ları ----------

    private static GameObject MakeDrone(string file, string displayName, DroneType type, bool ally, Color color,
                                        int charge, int move, int hp, int damage,
                                        string[] kitKeys, string[] dropKeys, CardData blueprint)
    {
        string path = DronesDir + "/" + file + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        // Ana drone ayarlandıktan sonra sahnede "Player" hep var; düşman kaynağı "Enemy" ilk çalıştırmada var
        GameObject src = GameObject.Find(ally ? "Player" : "Enemy");
        if (src == null)
        {
            Debug.LogError((ally ? "Player" : "Enemy") + " sahnede bulunamadı, " + file + " oluşturulamadı.");
            return null;
        }

        GameObject clone = UnityEngine.Object.Instantiate(src);
        clone.name = displayName;
        clone.transform.position = Vector3.zero;
        Debug.Log(file + " kaynak kök ölçeği: " + src.transform.localScale);

        // Kaynağın animasyonu sprite'ı ezmesin
        foreach (Animator a in clone.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);

        SpriteRenderer sr = clone.GetComponent<SpriteRenderer>();
        if (sr == null) sr = clone.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;

        Unit u = clone.GetComponent<Unit>();
        u.isPlayerOwned = ally;
        u.isMainDrone = false;
        u.maxHealth = hp;
        u.currentHealth = hp;
        u.maxMovement = move;

        DroneUnit d = clone.GetComponent<DroneUnit>();
        if (d == null) d = clone.AddComponent<DroneUnit>();
        d.droneType = type;
        d.isAlly = ally;

        if (ally)
        {
            u.usesCpu = false;
            u.cardsPerTurn = 1;
            u.maxCPU = 0;
            d.charge = charge;
            d.kit = new List<CardData>();
            foreach (string k in kitKeys) d.kit.Add(cards[k]);
        }
        else
        {
            u.maxCPU = 2;
            EnemyAI ai = clone.GetComponent<EnemyAI>();
            ai.isAwake = false;
            ai.attackDamage = damage;
            ai.attackRange = type == DroneType.Ranged ? 4 : 1;
            ai.targetMode = type == DroneType.Ranged ? EnemyTargetMode.LowestHealth : EnemyTargetMode.Nearest;

            d.scrapMin = type == DroneType.Boss ? 5 : 2;
            d.scrapMax = type == DroneType.Boss ? 10 : 5;
            d.cardDropChance = type == DroneType.Boss ? 0.5f : 0.35f;
            d.blueprint = blueprint;
            d.blueprintChance = 0.25f;
            d.dropCards = new List<CardData>();
            foreach (string k in dropKeys) d.dropCards.Add(cards[k]);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, path);
        UnityEngine.Object.DestroyImmediate(clone);
        return prefab;
    }

    // ---------- Çevre prefab'ları ----------

    private static GameObject MakePile(List<CardData> blueprints)
    {
        string path = EnvDir + "/HurdaYigini.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            GameObject go = new GameObject("Hurda Yigini");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.45f, 0.35f, 0.25f);
            sr.sortingOrder = 3;
            go.AddComponent<ScrapPile>();
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        ScrapPile pile = prefab.GetComponent<ScrapPile>();
        SerializedObject so = new SerializedObject(pile);
        SerializedProperty list = so.FindProperty("blueprints");
        if (list.arraySize == 0)
        {
            list.arraySize = blueprints.Count;
            for (int i = 0; i < blueprints.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = blueprints[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
        }
        return prefab;
    }

    private static GameObject MakeBarrel()
    {
        string path = EnvDir + "/PatlayanVaril.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null) return prefab;

        GameObject go = new GameObject("Patlayan Varil");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = new Color(0.85f, 0.3f, 0.15f);
        sr.sortingOrder = 3;
        go.AddComponent<ExplosiveBarrel>();
        prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------- Sahne ----------

    private static void ConfigureScene(GameObject enMelee, GameObject enRanged, GameObject enBoss, GameObject pile, GameObject barrel)
    {
        // Ana drone: işaretle, boss çağırabilsin diye CPU üst sınırını en az 6 yap
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            SerializedObject pu = new SerializedObject(player.GetComponent<Unit>());
            pu.FindProperty("isMainDrone").boolValue = true;
            SerializedProperty cpu = pu.FindProperty("maxCPU");
            if (cpu.intValue < 6) cpu.intValue = 6;
            pu.ApplyModifiedProperties();
        }

        // CardManager: her tur elde olacak kartlar
        CardManager cm = UnityEngine.Object.FindFirstObjectByType<CardManager>();
        if (cm != null)
        {
            SerializedObject so = new SerializedObject(cm);
            SerializedProperty tc = so.FindProperty("turnCards");
            string[] keys = { "AnaKalkan", "Sarj", "YakinSaldiri", "UzakSaldiri" };
            tc.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++) tc.GetArrayElementAtIndex(i).objectReferenceValue = cards[keys[i]];
            so.ApplyModifiedProperties();
        }

        Replace("Enemy", enMelee);
        Replace("Enemy (2)", enRanged);
        Replace("Enemy (3)", enBoss);
        Replace("Hurda Yigini", pile);
        Replace("Patlayan Varil", barrel);
        Replace("Patlayan Varil (2)", barrel);
    }

    // Eski sahne objesini aynı konumdaki prefab örneğiyle değiştirir (zaten prefab örneğiyse dokunmaz)
    private static void Replace(string objName, GameObject prefab)
    {
        if (prefab == null) return;
        GameObject old = GameObject.Find(objName);
        if (old == null || PrefabUtility.IsPartOfPrefabInstance(old)) return;

        Vector3 pos = old.transform.position;
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.position = pos;
        inst.name = objName;
        Undo.RegisterCreatedObjectUndo(inst, objName);
        UnityEngine.Object.DestroyImmediate(old);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
