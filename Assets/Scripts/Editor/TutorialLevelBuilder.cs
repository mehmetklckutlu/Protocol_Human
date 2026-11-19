#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Menü: Level > Tutorial Kur
// "örnek level" sahnesini kopyalayıp "Tutorial" sahnesini kurar: PH-01 drone'unun test tesisi.
// Kilitli geçitlerle ayrılmış 7 bölme: hareket, hurda toplama, ilk savaş, savunma, patlayıcı varil,
// destek drone ve çıkış. Akışı ve yazıları TutorialManager yönetir.
// Örnek level sahnesine dokunmaz. Tekrar çalıştırılırsa Tutorial sahnesini baştan kurar.
public static class TutorialLevelBuilder
{
    private const string TargetScenePath = "Assets/Scenes/Tutorial.unity";
    private const int Seed = 1;
    private const int GridMargin = 4;
    private const float FirstEnemyHealthScale = 0.5f;

    // Koordinatlar "plan" koordinatlarıdır; tilemap hücresi = (x + OX, y + OY).
    private const int OX = -62;
    private const int OY = 0;

    // Bölmeler: A başlangıç/hareket, B hurda, C ilk savaş, D savunma, E varil, F son test, G çıkış
    private static readonly int[][] Rooms =
    {
        new[] { 0, -4, 11, 4 },
        new[] { 18, -3, 27, 3 },
        new[] { 34, -5, 46, 5 },
        new[] { 53, -6, 66, 6 },
        new[] { 73, -6, 86, 6 },
        new[] { 93, -7, 110, 7 },
        new[] { 117, -3, 124, 3 }
    };

    private static readonly int[][] Corridors =
    {
        new[] { 12, -1, 17, 1 },
        new[] { 28, -1, 33, 1 },
        new[] { 47, -1, 52, 1 },
        new[] { 67, -1, 72, 1 },
        new[] { 87, -1, 92, 1 },
        new[] { 111, -1, 116, 1 }
    };

    // Siper kutuları
    private static readonly int[,] Crates =
    {
        {38,1}, {38,2}, {40,-2}, {58,0}, {59,0}, {58,1}, {59,1}, {60,-3}, {61,-3}, {57,3}, {58,3}, {76,3}, {77,3}, {76,-3}, {77,-3}, {99,2}, {99,3}, {100,-2}, {101,-2}, {105,0}, {97,0},
    };

    // Kilitli geçit kareleri: x, y, geçit sırası
    private const int GateCount = 6;
    private static readonly int[,] GateCells =
    {
        {14,-1,0}, {14,0,0}, {14,1,0}, {30,-1,1}, {30,0,1}, {30,1,1}, {49,-1,2}, {49,0,2}, {49,1,2}, {69,-1,3}, {69,0,3}, {69,1,3}, {89,-1,4}, {89,0,4}, {89,1,4}, {113,-1,5}, {113,0,5}, {113,1,5},
    };

    private static readonly Vector2Int PlayerStart = new Vector2Int(2, 0);
    private static readonly Vector2Int MoveTarget = new Vector2Int(9, 2);
    private static readonly Vector2Int ExitTarget = new Vector2Int(122, 0);

    // Düşmanlar: x, y; tür (0 = Yakın, 1 = Uzak, 2 = Boss); test aşaması (1-4)
    private static readonly int[,] EnemySpawns =
    {
        {42,0}, {62,2}, {81,1}, {81,-1}, {104,3}, {106,-2}, {102,-3},
    };
    private static readonly int[] EnemyKinds = { 0, 1, 0, 0, 0, 1, 0 };
    private static readonly int[] EnemyStage = { 1, 2, 3, 3, 4, 4, 4 };

    private static readonly int[,] ScrapSpawns =
    {
        {22,1}, {25,-1},
    };

    private static readonly int[,] BarrelSpawns =
    {
        {80,0}, {103,1},
    };

    // ---- Duvarlar: örnek leveldeki kurallarla ----
    // Duvarın zemine bakan sırası (iç sıra): altında kablolar, sağ/sol duvarlarda döndürülmüş kablolar, köşelerde köşe parçaları.
    // Arka sıra (dış sıra): altında borular, köşelerde boru köşesi. Walls tilemap'i bunların üstüne yarı saydam koyu katman basar.
    // Kutular (siperler): zeminde kutu tile'ı + Walls'ta koyu katman, örnek leveldeki 2x2 sütunlar gibi.
    private const string G_CableA   = "b019528b9f169a84cb7b049ab0ab7101";
    private const string G_CableB   = "185cf50080b48a04c9e783386085688f";
    private const string G_CableC   = "cb90cbbd743df5e40909bef5dfb078ef";
    private const string G_PipeA    = "5056228780718a44d9a0a585f3cf7618";
    private const string G_PipeB    = "ea2e60e31a8a37e419b0040cd29ca244";
    private const string G_PipeCorner = "b5555bddc09ebe24bb55ead636d953c9";
    private const string G_Filler   = "3ca5e0acaa53b824ab9ad8b8a284b2b0";
    private const string G_Crate    = "fcea52359f840934ab97a3b365f540cb";
    private static readonly string[] G_CableCorners =
    {
        "4e33f360a84df314287a4bc64d863e54", "0cdd2bdc276e29f4cb948c3253d40b55",
        "ff8ed6ebac57d214c9103b3bbdf06e78", "3e2911ade40f9d64e90dc47ec6333e38"
    };
    // Walls tilemap tile'ları
    private const string W_Dark     = "3ca5e0acaa53b824ab9ad8b8a284b2b0";
    private const string W_TopFace  = "974aaf26325a25241af917607cc537fd";
    private const string W_BotFace  = "75ff8f0ea5e80a34394f212480269272";
    private const string W_SideA    = "1fab2a9d4c81eea4288a8a1c7b92bd49";
    private const string W_SideB    = "9ba82ecf6b1d060438bb918ea9781761";
    private const string W_Accent   = "bce6b2c92957ac6479a8735904e3b9aa";
    private const string W_Corner   = "9d195ac509580a74ab34362c95d9b3f0";

    // Zemin tile'ları (örnek levelin Floor tilemap'inden, GUID ile)
    private const string BaseFloorGuid = "330ff212e9a36c643847a72b4878304b";   // düz perçinli mavi panel
    private const string DecorGuid     = "3b76384731045854394d3ec3e188c896";   // ara ara serpiştirilen süs
    private const string LineHGuid     = "14a0c1731229caa43b5e139731f0fb0e";   // şerit stili 1 (yatay/dikey)
    private const string LineVGuid     = "fa1671bbb258bb14b8183692c4377353";   // şerit stili 2 (dikey/yatay)
    private const string LineAGuid     = "aca6550331a6bea4eb1aedf383f43967";   // şerit stili 3 (sadece yatay)
    private const string CornerGuid    = "a59bc77ff46f36d458c653744d0cca3e";   // şerit köşesi
    private const string JunctionGuid  = "ea169b640aa8c8e4f9260511f5feb3f2";   // şerit kavşağı
    private const int DecorChance = 4; // yüzde

    // Örnek leveldeki tilemap matrisleri (e00, e01, e10, e11)
    private static readonly Matrix4x4 MId    = M(1, 0, 0, 1);
    private static readonly Matrix4x4 MRot180 = M(-1, 0, 0, -1);
    private static readonly Matrix4x4 MFlipY = M(1, 0, 0, -1);
    private static readonly Matrix4x4 MFlipX = M(-1, 0, 0, 1);
    private static readonly Matrix4x4 MRotL  = M(0, -1, 1, 0);
    private static readonly Matrix4x4 MRotR  = M(0, 1, -1, 0);
    private static readonly Matrix4x4 MTrans = M(0, -1, -1, 0);

    private static System.Random rng;

    [MenuItem("Level/Tutorial Kur")]
    public static void Build()
    {
        string samplePath = FindSampleScene();
        if (samplePath == null) { Debug.LogError("'örnek level' sahnesi bulunamadı."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(TargetScenePath))
        {
            if (!EditorUtility.DisplayDialog("Tutorial",
                    "'" + TargetScenePath + "' zaten var. Silinip örnek levelden yeniden kurulsun mu?",
                    "Evet, yeniden kur", "Vazgeç"))
                return;
            AssetDatabase.DeleteAsset(TargetScenePath);
        }

        if (!AssetDatabase.CopyAsset(samplePath, TargetScenePath))
        {
            Debug.LogError("Sahne kopyalanamadı: " + samplePath + " -> " + TargetScenePath);
            return;
        }
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        rng = new System.Random(Seed);


        GridManager gm = Object.FindFirstObjectByType<GridManager>();
        if (gm == null) { Debug.LogError("Sahnede GridManager yok."); return; }

        SerializedObject gso = new SerializedObject(gm);
        Tilemap floorMap = gso.FindProperty("floorTilemap").objectReferenceValue as Tilemap;
        Tilemap wallMap = gso.FindProperty("wallTilemap").objectReferenceValue as Tilemap;
        if (floorMap == null) floorMap = FindTilemap(scene, "Floor");
        if (wallMap == null) wallMap = FindTilemap(scene, "Walls");
        if (floorMap == null || wallMap == null) { Debug.LogError("Floor / Walls tilemap bulunamadı."); return; }

        // ---- Plan ----
        HashSet<Vector2Int> roomCells = new HashSet<Vector2Int>();
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach (int[] r in Rooms) AddRect(r, floor, roomCells);
        foreach (int[] c in Corridors) AddRect(c, floor, null);
        HashSet<Vector2Int> crates = new HashSet<Vector2Int>();
        for (int i = 0; i < Crates.GetLength(0); i++)
        {
            Vector2Int cc = new Vector2Int(Crates[i, 0], Crates[i, 1]);
            crates.Add(cc);
            floor.Remove(cc);
        }
        // Kilitli geçitler: kurulumda kutu gibi kapalı, tutorial ilerledikçe TutorialManager açar.
        for (int i = 0; i < GateCells.GetLength(0); i++)
        {
            Vector2Int gc = new Vector2Int(GateCells[i, 0], GateCells[i, 1]);
            crates.Add(gc);
            floor.Remove(gc);
        }

        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        foreach (Vector2Int f in floor)
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int c = new Vector2Int(f.x + dx, f.y + dy);
                    if (!floor.Contains(c)) walls.Add(c);
                }

        // ---- Tilemap'leri temizle ve çiz ----
        floorMap.ClearAllTiles();
        wallMap.ClearAllTiles();

        TileBase baseFloor;

        Dictionary<string, TileBase> ft = new Dictionary<string, TileBase>();
        foreach (string g in new[] { BaseFloorGuid, DecorGuid, LineHGuid, LineVGuid, LineAGuid, CornerGuid, JunctionGuid })
        {
            TileBase tb = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (tb == null) { Debug.LogError("Zemin tile'ı bulunamadı (GUID " + g + ")."); return; }
            ft[g] = tb;
        }
        baseFloor = ft[BaseFloorGuid];

        Dictionary<string, TileBase> wt = new Dictionary<string, TileBase>();
        string[] wallGuids = { G_CableA, G_CableB, G_CableC, G_PipeA, G_PipeB, G_PipeCorner, G_Filler, G_Crate,
                               W_Dark, W_TopFace, W_BotFace, W_SideA, W_SideB, W_Accent, W_Corner };
        foreach (string g in wallGuids)
        {
            if (wt.ContainsKey(g)) continue;
            TileBase tb = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (tb == null) { Debug.LogError("Duvar tile'ı bulunamadı (GUID " + g + ")."); return; }
            wt[g] = tb;
        }
        foreach (string g in G_CableCorners)
        {
            TileBase tb = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (tb == null) { Debug.LogError("Duvar tile'ı bulunamadı (GUID " + g + ")."); return; }
            wt[g] = tb;
        }

        HashSet<Vector2Int> floorOrCrate = new HashSet<Vector2Int>(floor);
        floorOrCrate.UnionWith(crates);
        HashSet<string> usedWallTiles = new HashSet<string>();
        foreach (Vector2Int c in walls)
        {
            Vector3Int cell = ToCell(c);
            string ug, wg; int um;
            if (crates.Contains(c)) { ug = G_Crate; um = 0; wg = W_Dark; }
            else WallCell(c, floorOrCrate, out ug, out um, out wg);
            floorMap.SetTile(cell, wt[ug]);
            if (um != 0) floorMap.SetTransformMatrix(cell, um == 1 ? MRotL : MRotR);
            wallMap.SetTile(cell, wt[wg]);
            usedWallTiles.Add(wg);
        }
        foreach (string g in usedWallTiles)
        {
            if (wt[g] is Tile t && t.colliderType != Tile.ColliderType.Grid)
            {
                t.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(t);
            }
        }

        HashSet<Vector2Int> path = BuildPaths(floor);
        foreach (Vector2Int c in floor)
        {
            Vector3Int cell = ToCell(c);
            if (path.Contains(c))
            {
                PathTile(c, path, ft, out TileBase pt, out Matrix4x4 pm);
                floorMap.SetTile(cell, pt);
                floorMap.SetTransformMatrix(cell, pm);
            }
            else if (rng.Next(100) < DecorChance)
                floorMap.SetTile(cell, ft[DecorGuid]);
            else
                floorMap.SetTile(cell, baseFloor);
        }

        AssetDatabase.SaveAssets();
        floorMap.CompressBounds();
        wallMap.CompressBounds();

        // ---- GridManager boyutu ----
        BoundsInt b = wallMap.cellBounds;
        Vector3 cellMin = wallMap.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0));
        float cs = gm.CellSize;
        gso.FindProperty("gridOrigin").vector3Value =
            new Vector3(cellMin.x - GridMargin * cs, cellMin.y - GridMargin * cs, 0f);
        gso.FindProperty("gridSize").vector2IntValue =
            new Vector2Int(b.size.x + GridMargin * 2, b.size.y + GridMargin * 2);
        gso.FindProperty("floorTilemap").objectReferenceValue = floorMap;
        gso.FindProperty("wallTilemap").objectReferenceValue = wallMap;
        gso.ApplyModifiedPropertiesWithoutUndo();

        // ---- Oyuncu ----
        Vector3 startWorld = CellCenter(wallMap, PlayerStart);
        List<Unit> players = new List<Unit>();
        List<Unit> enemies = new List<Unit>();
        foreach (Unit u in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (u.gameObject.scene != scene) continue;
            if (u.isPlayerOwned) players.Add(u); else enemies.Add(u);
        }
        players.Sort((u1, u2) => u2.isMainDrone.CompareTo(u1.isMainDrone));
        Vector2Int[] allyOffsets = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        for (int i = 0; i < players.Count; i++)
        {
            Vector2Int c = PlayerStart + allyOffsets[Mathf.Min(i, allyOffsets.Length - 1)];
            SetPos(players[i].transform, CellCenter(wallMap, c));
        }

        // ---- Düşmanlar ----
        List<GameObject>[] enemyByKind = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        foreach (Unit e in enemies) enemyByKind[EnemyKindOf(e.gameObject)].Add(e.gameObject);
        GameObject anyEnemy = enemies.Count > 0 ? enemies[0].gameObject : null;
        string[] enemyPrefabNames = { "Drone_Dusman_Yakin", "Drone_Dusman_Uzak", "Drone_Dusman_Boss" };
        Transform enemyParent = anyEnemy != null ? anyEnemy.transform.parent : null;

        int[] reuseIdx = new int[3];
        List<GameObject> keptEnemies = new List<GameObject>();
        GameObject[] placedEnemies = new GameObject[EnemySpawns.GetLength(0)];
        for (int i = 0; i < EnemySpawns.GetLength(0); i++)
        {
            int kind = EnemyKinds[i];
            List<GameObject> pool = enemyByKind[kind];
            GameObject go;
            if (reuseIdx[kind] < pool.Count) go = pool[reuseIdx[kind]++];
            else if (pool.Count > 0) go = Duplicate(pool[0]);
            else go = InstantiatePrefab(enemyPrefabNames[kind], enemyParent) ?? (anyEnemy != null ? Duplicate(anyEnemy) : null);
            if (go == null) continue;
            if (enemyParent != null && go.transform.parent != enemyParent) go.transform.SetParent(enemyParent, true);
            SetPos(go.transform, CellCenter(wallMap, new Vector2Int(EnemySpawns[i, 0], EnemySpawns[i, 1])));
            keptEnemies.Add(go);
            placedEnemies[i] = go;
        }
        foreach (Unit e in enemies)
            if (e != null && !keptEnemies.Contains(e.gameObject)) Object.DestroyImmediate(e.gameObject);

        // ---- Hurda yığınları ve variller ----
        List<GameObject> scrapObjs = PlaceProps<ScrapPile>(scene, wallMap, ScrapSpawns, "HurdaYigini");
        PlaceProps<ExplosiveBarrel>(scene, wallMap, BarrelSpawns, "PatlayanVaril");

        // ---- Kamera ----
        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.transform.position = new Vector3(startWorld.x, startWorld.y, -10f);
            TacticalCamera tc = cam.GetComponent<TacticalCamera>();
            if (tc != null)
            {
                SerializedObject cso = new SerializedObject(tc);
                SerializedProperty bp = cso.FindProperty("boundsTilemap");
                if (bp != null) bp.objectReferenceValue = floorMap;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---- İlk eğitim drone'u daha zayıf ----
        for (int i = 0; i < placedEnemies.Length; i++)
        {
            if (placedEnemies[i] == null || EnemyStage[i] != 1) continue;
            Unit u = placedEnemies[i].GetComponent<Unit>();
            if (u == null) continue;
            u.maxHealth = Mathf.Max(1, Mathf.RoundToInt(u.maxHealth * FirstEnemyHealthScale));
            u.currentHealth = u.maxHealth;
            PrefabUtility.RecordPrefabInstancePropertyModifications(u);
            EditorUtility.SetDirty(u);
        }

        // ---- Tutorial yöneticisi ----
        foreach (TutorialManager old in Object.FindObjectsByType<TutorialManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);
        TutorialManager tut = new GameObject("TutorialManager").AddComponent<TutorialManager>();
        tut.floorMap = floorMap;
        tut.wallMap = wallMap;
        tut.openFloorTile = baseFloor;
        for (int g = 0; g < GateCount; g++)
        {
            TutorialManager.Gate gate = new TutorialManager.Gate();
            for (int i = 0; i < GateCells.GetLength(0); i++)
                if (GateCells[i, 2] == g) gate.cells.Add(ToCell(new Vector2Int(GateCells[i, 0], GateCells[i, 1])));
            tut.gates.Add(gate);
        }
        tut.moveTargetCell = ToCell(MoveTarget);
        tut.exitTargetCell = ToCell(ExitTarget);
        foreach (GameObject s in scrapObjs)
        {
            ScrapPile sp = s != null ? s.GetComponent<ScrapPile>() : null;
            if (sp != null) tut.scrapPiles.Add(sp);
        }
        List<Unit>[] fights = { null, tut.fight1, tut.fight2, tut.fight3, tut.fight4 };
        for (int i = 0; i < placedEnemies.Length; i++)
        {
            if (placedEnemies[i] == null) continue;
            Unit u = placedEnemies[i].GetComponent<Unit>();
            if (u != null && EnemyStage[i] >= 1 && EnemyStage[i] <= 4) fights[EnemyStage[i]].Add(u);
        }
        tut.defenseCard = LoadCard("AnaKalkan");
        tut.summonCard = LoadCard("BP_Yakin");
        tut.nextScene = "MainLevel";
        EditorUtility.SetDirty(tut);

        AddToBuildSettings(TargetScenePath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=green>Tutorial kuruldu: " + GateCount + " test bölmesi geçidi, " +
                  keptEnemies.Count + " eğitim drone'u, " + ScrapSpawns.GetLength(0) + " hurda, " +
                  BarrelSpawns.GetLength(0) + " varil. (" + TargetScenePath + ")</color>");
    }

    // ---------------- yardımcılar ----------------

    private static void AddRect(int[] r, HashSet<Vector2Int> a, HashSet<Vector2Int> b)
    {
        for (int x = r[0]; x <= r[2]; x++)
            for (int y = r[1]; y <= r[3]; y++)
            {
                a.Add(new Vector2Int(x, y));
                if (b != null) b.Add(new Vector2Int(x, y));
            }
    }

    // Örnek leveldeki gibi zemin şeritleri: her odanın içinde duvardan 2 kare içeride bir halka,
    // koridorların ortasından geçen çizgiler halkaları birbirine bağlar.
    private static HashSet<Vector2Int> BuildPaths(HashSet<Vector2Int> floor)
    {
        HashSet<Vector2Int> path = new HashSet<Vector2Int>();
        foreach (int[] r in Rooms)
        {
            int x0 = r[0] + 2, y0 = r[1] + 2, x1 = r[2] - 2, y1 = r[3] - 2;
            if (x1 - x0 < 2 || y1 - y0 < 2) continue;
            for (int x = x0; x <= x1; x++) { path.Add(new Vector2Int(x, y0)); path.Add(new Vector2Int(x, y1)); }
            for (int y = y0; y <= y1; y++) { path.Add(new Vector2Int(x0, y)); path.Add(new Vector2Int(x1, y)); }
        }
        foreach (int[] c in Corridors)
        {
            int w = c[2] - c[0] + 1, h = c[3] - c[1] + 1;
            if (w >= h)
            {
                int y = (c[1] + c[3]) / 2;
                for (int x = c[0] - 2; x <= c[2] + 2; x++) path.Add(new Vector2Int(x, y));
            }
            else
            {
                int x = (c[0] + c[2]) / 2;
                for (int y = c[1] - 2; y <= c[3] + 2; y++) path.Add(new Vector2Int(x, y));
            }
        }
        path.RemoveWhere(p => !floor.Contains(p));

        // Çıkmaz uçları budayıp sadece bağlı şeritleri bırak.
        bool changed = true;
        while (changed)
        {
            changed = false;
            List<Vector2Int> dead = new List<Vector2Int>();
            foreach (Vector2Int p in path)
            {
                int n = 0;
                if (path.Contains(p + Vector2Int.up)) n++;
                if (path.Contains(p + Vector2Int.down)) n++;
                if (path.Contains(p + Vector2Int.left)) n++;
                if (path.Contains(p + Vector2Int.right)) n++;
                if (n <= 1) dead.Add(p);
            }
            foreach (Vector2Int p in dead) { path.Remove(p); changed = true; }
        }
        return path;
    }

    // Şerit tile'larının yönleri örnek levelde kullanılan matrislerle aynıdır.
    private static void PathTile(Vector2Int c, HashSet<Vector2Int> path, Dictionary<string, TileBase> ft,
                                 out TileBase tile, out Matrix4x4 m)
    {
        bool n = path.Contains(c + Vector2Int.up), s = path.Contains(c + Vector2Int.down);
        bool e = path.Contains(c + Vector2Int.right), w = path.Contains(c + Vector2Int.left);
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

        if (count >= 3)
        {
            tile = ft[JunctionGuid];
            if (count == 4) m = MFlipY;
            else if (!n) m = MId;       // D-G-B
            else if (!s) m = MFlipY;    // K-D-B
            else if (!w) m = MRotL;     // K-D-G
            else m = MRotR;             // K-G-B
            return;
        }
        if (count == 2 && (n || s) && (e || w))
        {
            tile = ft[CornerGuid];
            if (e && s) m = MId;
            else if (n && e) m = MFlipY;
            else if (s && w) m = MFlipX;
            else m = MRot180;
            return;
        }

        bool horizontal = e || w;
        int style = Mathf.Abs((horizontal ? c.y : c.x) * 31 + 7) % 3;   // aynı çizgi boyunca aynı stil
        if (horizontal)
        {
            if (style == 0) { tile = ft[LineHGuid]; m = MId; }
            else if (style == 1) { tile = ft[LineVGuid]; m = MRotL; }
            else { tile = ft[LineAGuid]; m = MId; }
        }
        else
        {
            if (style == 0) { tile = ft[LineHGuid]; m = MTrans; }
            else { tile = ft[LineVGuid]; m = MFlipX; }
        }
    }

    private static readonly Vector2Int[] Orth = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private static readonly Vector2Int[] Diag = { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1) };

    // Bir duvar karesinin zemindeki (kablo/boru) tile'ını, matrisini ve Walls katmanı tile'ını seçer.
    private static void WallCell(Vector2Int c, HashSet<Vector2Int> floor,
                                 out string under, out int matrix, out string wall)
    {
        bool n1 = floor.Contains(c + Vector2Int.up), s1 = floor.Contains(c + Vector2Int.down);
        bool e1 = floor.Contains(c + Vector2Int.right), w1 = floor.Contains(c + Vector2Int.left);
        bool d1 = false;
        foreach (Vector2Int d in Diag) if (floor.Contains(c + d)) d1 = true;
        bool vert1 = n1 || s1, horz1 = e1 || w1;
        matrix = 0;

        if (vert1 || horz1 || d1)
        {
            // İç sıra: kablolar
            if ((vert1 && horz1) || (!vert1 && !horz1))
            {
                under = G_CableCorners[rng.Next(G_CableCorners.Length)];
                wall = rng.Next(100) < 60 ? W_Corner : W_Dark;
            }
            else if (vert1)
            {
                int r = rng.Next(100);
                under = r < 45 ? G_CableA : (r < 90 ? G_CableB : G_CableC);
                int q = rng.Next(100);
                if (s1) wall = q < 60 ? W_TopFace : (q < 90 ? W_Dark : W_Accent);   // duvar zeminin üstünde
                else wall = q < 50 ? W_BotFace : (q < 90 ? W_Dark : W_Accent);      // duvar zeminin altında
            }
            else
            {
                int r = rng.Next(100);
                under = r < 45 ? G_CableA : (r < 85 ? G_CableB : G_CableC);
                matrix = rng.Next(2) == 0 ? 1 : 2;
                int q = rng.Next(100);
                wall = q < 40 ? W_SideA : (q < 80 ? W_SideB : (q < 95 ? W_Accent : W_Dark));
            }
            return;
        }

        // Dış sıra: borular
        wall = W_Dark;
        bool n2 = floor.Contains(c + Vector2Int.up * 2), s2 = floor.Contains(c + Vector2Int.down * 2);
        bool e2 = floor.Contains(c + Vector2Int.right * 2), w2 = floor.Contains(c + Vector2Int.left * 2);
        bool d2 = false;
        foreach (Vector2Int d in Diag) if (floor.Contains(c + d * 2)) d2 = true;
        bool vert2 = n2 || s2, horz2 = e2 || w2;

        if (vert2 && !horz2)
            under = rng.Next(100) < 78 ? G_PipeA : G_PipeB;
        else if (horz2 && !vert2)
        {
            under = rng.Next(100) < 60 ? G_PipeA : G_PipeB;
            matrix = rng.Next(2) == 0 ? 1 : 2;
        }
        else if (vert2 || horz2 || d2)
            under = G_PipeCorner;
        else
            under = G_Filler;
    }

    private static int PickIndex(int[][] pool, int total)
    {
        int r = rng.Next(total);
        for (int i = 0; i < pool.Length; i++)
        {
            r -= pool[i][3];
            if (r < 0) return i;
        }
        return 0;
    }

    private static Matrix4x4 Rot(int degrees)
    {
        return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, degrees), Vector3.one);
    }

    private static Matrix4x4 M(float e00, float e01, float e10, float e11)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = e00; m.m01 = e01; m.m10 = e10; m.m11 = e11;
        return m;
    }

    private static Vector3Int ToCell(Vector2Int c) { return new Vector3Int(c.x + OX, c.y + OY, 0); }

    private static Vector3 CellCenter(Tilemap map, Vector2Int c)
    {
        Vector3 p = map.GetCellCenterWorld(ToCell(c));
        p.z = 0f;
        return p;
    }

    private static void SetPos(Transform t, Vector3 p)
    {
        t.position = new Vector3(p.x, p.y, t.position.z);
    }

    private static int EnemyKindOf(GameObject go)
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        string key = (string.IsNullOrEmpty(path) ? go.name : Path.GetFileNameWithoutExtension(path)).ToLowerInvariant();
        if (key.Contains("boss")) return 2;
        if (key.Contains("uzak")) return 1;
        return 0;
    }

    private static List<GameObject> PlaceProps<T>(Scene scene, Tilemap map, int[,] spawns, string prefabName) where T : Component
    {
        List<GameObject> placed = new List<GameObject>();
        List<GameObject> existing = new List<GameObject>();
        foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.gameObject.scene == scene) existing.Add(c.gameObject);

        Transform parent = existing.Count > 0 ? existing[0].transform.parent : null;
        int used = 0;
        for (int i = 0; i < spawns.GetLength(0); i++)
        {
            GameObject go;
            if (used < existing.Count) go = existing[used++];
            else if (existing.Count > 0) go = Duplicate(existing[0]);
            else go = InstantiatePrefab(prefabName, parent);
            if (go == null) continue;
            if (parent != null && go.transform.parent != parent) go.transform.SetParent(parent, true);
            SetPos(go.transform, CellCenter(map, new Vector2Int(spawns[i, 0], spawns[i, 1])));
            placed.Add(go);
        }
        for (int i = used; i < existing.Count; i++)
            if (existing[i] != null) Object.DestroyImmediate(existing[i]);
        return placed;
    }

    private static CardData LoadCard(string assetName)
    {
        foreach (string guid in AssetDatabase.FindAssets(assetName + " t:CardData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == assetName)
                return AssetDatabase.LoadAssetAtPath<CardData>(path);
        }
        Debug.LogWarning("Kart bulunamadı: " + assetName);
        return null;
    }

    // Sahnedeki objeyi (prefab bağlantısı, override'lar ve sahnede eklenen Animator'larla birlikte) kopyalar.
    private static GameObject Duplicate(GameObject template)
    {
        Object[] prevSelection = Selection.objects;
        Selection.objects = new Object[] { template };
        Unsupported.DuplicateGameObjectsUsingPasteboard();
        GameObject copy = Selection.activeGameObject;
        Selection.objects = prevSelection;

        if (copy == null || copy == template)
            copy = Object.Instantiate(template, template.transform.parent);

        Transform parent = template.transform.parent;
        string baseName = template.name;
        int paren = baseName.IndexOf(" (");
        if (paren > 0) baseName = baseName.Substring(0, paren);
        copy.name = parent != null ? GameObjectUtility.GetUniqueNameForSibling(parent, baseName) : baseName;
        return copy;
    }

    private static GameObject InstantiatePrefab(string prefabName, Transform parent)
    {
        foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != prefabName) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            if (parent != null) go.transform.SetParent(parent, true);
            return go;
        }
        Debug.LogWarning("Prefab bulunamadı: " + prefabName);
        return null;
    }

    private static Dictionary<int, TileBase> LoadTiles(string prefix)
    {
        Dictionary<int, TileBase> d = new Dictionary<int, TileBase>();
        foreach (string guid in AssetDatabase.FindAssets(prefix + " t:TileBase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(path);
            if (!n.StartsWith(prefix)) continue;
            if (int.TryParse(n.Substring(prefix.Length), out int i))
                d[i] = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        }
        return d;
    }

    private static Tilemap FindTilemap(Scene s, string name)
    {
        foreach (GameObject r in s.GetRootGameObjects())
            foreach (Tilemap t in r.GetComponentsInChildren<Tilemap>(true))
                if (t.name == name) return t;
        return null;
    }

    private static string FindSampleScene()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (file == "örnek level" || file == "ornek level") return path;
        }
        return null;
    }

    private static void AddToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in list)
            if (s.path == path) return;
        // Ana menüden hemen sonra gelsin.
        list.Insert(Mathf.Min(1, list.Count), new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
