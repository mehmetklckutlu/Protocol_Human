#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Menü: Level > Buyuk Level Kur
// "örnek level" sahnesini kopyalayıp "Buyuk Level" adında yeni bir sahne oluşturur ve
// içine örnek levelin mantığıyla (2 kare kalın duvarlar, 3 kare koridorlar, oda içi sütunlar,
// zemin şeritleri, düşman / hurda / varil dağılımı) yaklaşık 10 kat büyük bir level çizer.
// Örnek level sahnesine dokunmaz. Tekrar çalıştırılırsa Buyuk Level sahnesini baştan kurar.
public static class BigLevelBuilder
{
    private const string TargetScenePath = "Assets/Scenes/Buyuk Level.unity";
    private const int Seed = 207;
    private const int GridMargin = 4;

    // Koordinatlar "plan" koordinatlarıdır; tilemap hücresi = (x + OX, y + OY).
    private const int OX = -83;
    private const int OY = -52;

    // Odalar (x0, y0, x1, y1) - örnek leveldeki gibi dikdörtgen odalar
    private static readonly int[][] Rooms =
    {
        new[] { 5, 60, 21, 69 },
        new[] { 6, 5, 23, 23 },
        new[] { 6, 81, 20, 91 },
        new[] { 10, 30, 19, 48 },
        new[] { 32, 85, 49, 95 },
        new[] { 33, 34, 48, 48 },
        new[] { 35, 5, 49, 22 },
        new[] { 37, 55, 48, 64 },
        new[] { 61, 8, 74, 23 },
        new[] { 61, 57, 77, 70 },
        new[] { 64, 39, 102, 47 },
        new[] { 65, 82, 79, 96 },
        new[] { 88, 57, 105, 73 },
        new[] { 88, 81, 105, 98 },
        new[] { 89, 5, 101, 23 },
        new[] { 113, 57, 131, 70 },
        new[] { 113, 81, 158, 97 },
        new[] { 114, 6, 131, 21 },
        new[] { 122, 34, 133, 42 },
        new[] { 142, 32, 152, 44 },
        new[] { 143, 59, 152, 73 },
        new[] { 145, 5, 160, 23 }
    };

    // Koridorlar (x0, y0, x1, y1) - 3 kare genişliğinde
    private static readonly int[][] Corridors =
    {
        new[] { 123, 21, 125, 34 },
        new[] { 123, 42, 125, 57 },
        new[] { 133, 36, 142, 38 },
        new[] { 40, 64, 42, 85 },
        new[] { 40, 22, 42, 34 },
        new[] { 131, 15, 145, 17 },
        new[] { 49, 11, 61, 13 },
        new[] { 12, 23, 14, 30 },
        new[] { 145, 73, 147, 81 },
        new[] { 44, 48, 46, 55 },
        new[] { 131, 61, 143, 63 },
        new[] { 92, 23, 94, 39 },
        new[] { 89, 47, 91, 57 },
        new[] { 77, 58, 88, 60 },
        new[] { 21, 61, 37, 63 },
        new[] { 66, 23, 68, 39 },
        new[] { 71, 70, 73, 82 },
        new[] { 20, 87, 32, 89 },
        new[] { 19, 36, 33, 38 },
        new[] { 105, 91, 113, 93 },
        new[] { 102, 42, 113, 44 },
        new[] { 111, 37, 113, 44 },
        new[] { 111, 37, 122, 39 },
        new[] { 146, 23, 148, 32 },
        new[] { 74, 47, 76, 57 },
        new[] { 74, 10, 89, 12 },
        new[] { 48, 42, 64, 44 },
        new[] { 48, 59, 61, 61 }
    };

    // Siper kutuları (odalarda 1x1 - 3x1 - 2x2 kümeler, koridor kenarlarında tek kutular)
    private static readonly int[,] Crates =
    {
        {9,11}, {9,12}, {9,13}, {9,17}, {9,18}, {9,88}, {10,8}, {10,63},
        {10,64}, {10,65}, {11,84}, {12,16}, {12,19}, {12,20}, {12,87}, {12,88},
        {13,87}, {13,88}, {14,10}, {14,11}, {14,35}, {14,36}, {14,64}, {14,65},
        {14,66}, {15,15}, {15,18}, {15,35}, {15,36}, {15,42}, {15,45}, {15,84},
        {16,42}, {17,8}, {17,63}, {17,66}, {17,88}, {18,8}, {18,11}, {18,12},
        {18,15}, {18,19}, {18,63}, {18,66}, {19,8}, {19,11}, {19,12}, {19,19},
        {23,38}, {24,87}, {25,61}, {29,36}, {32,63}, {35,90}, {35,91}, {36,42},
        {36,43}, {38,38}, {38,88}, {38,91}, {38,92}, {39,10}, {39,15}, {39,18},
        {39,88}, {39,91}, {39,92}, {40,15}, {40,18}, {40,26}, {40,41}, {40,42},
        {40,68}, {41,15}, {41,18}, {41,41}, {41,42}, {41,45}, {42,38}, {42,59},
        {42,60}, {42,75}, {42,88}, {42,89}, {42,92}, {43,10}, {43,11}, {43,59},
        {43,60}, {44,10}, {44,11}, {44,14}, {44,43}, {44,44}, {45,14}, {45,17},
        {45,18}, {45,37}, {45,38}, {45,89}, {45,90}, {46,89}, {46,90}, {52,42},
        {52,59}, {53,11}, {57,61}, {58,44}, {64,20}, {64,63}, {64,64}, {65,20},
        {65,60}, {65,67}, {66,12}, {66,15}, {66,16}, {66,27}, {66,67}, {67,12},
        {67,15}, {67,16}, {67,43}, {67,44}, {67,63}, {68,19}, {68,32}, {68,63},
        {68,85}, {68,91}, {68,92}, {69,60}, {69,66}, {69,67}, {69,88}, {70,12},
        {70,13}, {70,66}, {70,67}, {71,18}, {71,74}, {71,92}, {71,93}, {72,43},
        {72,62}, {72,63}, {72,86}, {72,87}, {73,66}, {73,86}, {73,87}, {74,66},
        {75,42}, {76,51}, {76,86}, {76,89}, {76,90}, {76,91}, {78,12}, {78,43},
        {79,43}, {80,43}, {81,58}, {83,44}, {85,10}, {86,43}, {89,43}, {89,44},
        {90,43}, {90,44}, {91,51}, {91,63}, {91,66}, {91,89}, {91,90}, {91,91},
        {91,95}, {92,8}, {92,9}, {92,12}, {92,13}, {92,14}, {92,19}, {92,32},
        {92,60}, {92,85}, {93,43}, {93,44}, {93,60}, {94,27}, {94,65}, {94,70},
        {94,91}, {94,92}, {94,95}, {95,12}, {95,15}, {95,16}, {95,17}, {95,20},
        {95,70}, {95,85}, {95,86}, {96,8}, {96,9}, {96,42}, {96,61}, {96,70},
        {96,85}, {96,86}, {97,42}, {97,61}, {98,18}, {98,19}, {99,64}, {99,67},
        {99,68}, {99,69}, {99,84}, {99,85}, {99,94}, {100,60}, {100,61}, {100,64},
        {100,84}, {100,85}, {100,90}, {102,68}, {102,69}, {102,94}, {106,44}, {109,91},
        {113,40}, {116,60}, {116,61}, {116,62}, {116,84}, {116,85}, {116,86}, {117,9},
        {117,10}, {118,67}, {118,90}, {119,15}, {119,16}, {119,17}, {119,84}, {119,85},
        {119,93}, {120,61}, {120,62}, {120,84}, {120,85}, {120,93}, {121,9}, {121,61},
        {121,62}, {121,93}, {122,14}, {122,15}, {122,18}, {123,14}, {123,15}, {123,46},
        {123,65}, {123,88}, {124,9}, {124,65}, {124,92}, {124,93}, {125,25}, {125,52},
        {125,85}, {126,15}, {126,16}, {126,37}, {126,38}, {126,61}, {127,9}, {127,10},
        {127,11}, {127,15}, {127,16}, {127,37}, {127,38}, {127,67}, {127,89}, {127,90},
        {128,64}, {128,67}, {128,84}, {128,85}, {130,39}, {130,91}, {130,92}, {130,93},
        {133,88}, {133,94}, {134,84}, {134,88}, {135,17}, {135,63}, {137,36}, {137,93},
        {139,90}, {140,84}, {140,85}, {140,90}, {141,15}, {143,85}, {143,88}, {143,91},
        {145,35}, {145,39}, {145,40}, {145,41}, {146,27}, {146,85}, {146,88}, {146,93},
        {147,63}, {147,67}, {147,68}, {147,69}, {147,77}, {147,88}, {147,93}, {148,16},
        {148,19}, {148,20}, {148,37}, {148,38}, {148,39}, {148,93}, {149,11}, {149,12},
        {150,8}, {150,85}, {150,86}, {151,16}, {151,19}, {151,20}, {151,85}, {151,86},
        {151,89}, {151,90}, {151,93}, {151,94}, {152,16}, {153,8}, {153,9}, {153,16},
        {154,8}, {154,9}, {154,85}, {154,86}, {154,87}, {155,12}, {155,91}, {155,92},
        {155,93}, {156,15}, {157,15}, {157,20},
    };

    private static readonly Vector2Int PlayerStart = new Vector2Int(14, 39);

    // Düşmanlar: x, y, tür (0 = Yakın, 1 = Uzak, 2 = Boss)
    private static readonly int[,] EnemySpawns =
    {
        {14,21}, {38,45}, {47,16}, {47,8}, {40,61}, {87,45}, {74,60}, {65,18},
        {64,10}, {10,67}, {7,63}, {36,88}, {46,92}, {102,63}, {102,60}, {97,16},
        {98,21}, {98,9}, {73,90}, {77,93}, {15,87}, {8,83}, {131,36}, {126,40},
        {120,11}, {129,8}, {120,59}, {129,59}, {148,34}, {150,37}, {156,10}, {158,17},
        {145,67}, {150,65}, {135,86}, {129,88}, {96,96}, {98,90}, {91,87}, {100,96},
    };
    private static readonly int[] EnemyKinds =
    {
        0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 2, 1, 2, 0, 2, 0, 2, 1, 2, 0, 1, 0
    };

    private static readonly int[,] ScrapSpawns =
    {
        {18,33}, {9,15}, {20,6}, {40,38}, {37,9}, {46,11}, {45,57}, {47,59},
        {39,58}, {71,46}, {66,41}, {87,41}, {73,69}, {69,15}, {6,68}, {12,63},
        {35,93}, {48,94}, {46,94}, {93,67}, {94,62}, {91,71}, {98,12}, {99,7},
        {78,91}, {72,95}, {8,85}, {19,85}, {123,35}, {118,12}, {121,20}, {123,12},
        {118,69}, {130,62}, {143,43}, {154,18}, {147,71}, {151,63}, {130,82}, {115,82},
        {155,89}, {101,88},
    };

    private static readonly int[,] BarrelSpawns =
    {
        {16,35}, {16,37}, {12,9}, {20,11}, {18,9}, {22,22}, {20,8}, {39,42},
        {43,38}, {47,35}, {46,37}, {37,11}, {40,7}, {44,12}, {41,9}, {43,62},
        {41,59}, {44,59}, {81,43}, {100,41}, {68,43}, {94,46}, {73,43}, {75,66},
        {73,63}, {70,58}, {71,63}, {67,14}, {73,19}, {64,13}, {72,12}, {18,68},
        {19,66}, {15,64}, {44,87}, {41,86}, {40,88}, {38,89}, {91,60}, {92,58},
        {98,64}, {100,65}, {96,12}, {90,17}, {93,19}, {93,6}, {72,92}, {75,90},
        {70,92}, {71,86}, {16,84}, {12,84}, {18,88}, {128,38}, {127,36}, {129,41},
        {115,20}, {128,16}, {125,9}, {116,7}, {117,62}, {127,61}, {122,61}, {119,67},
        {146,39}, {147,37}, {146,35}, {159,12}, {155,20}, {156,12}, {151,11}, {148,63},
        {146,63}, {148,67}, {155,87}, {126,89}, {124,88}, {152,85}, {124,95}, {141,90},
        {131,93}, {90,83}, {97,88}, {92,90}, {93,95}, {125,28}, {125,44}, {141,38},
        {42,67}, {40,81}, {42,25}, {138,17}, {51,13}, {12,25}, {147,75}, {44,54},
        {141,63}, {92,37}, {89,49}, {86,60}, {35,63}, {66,31}, {71,72}, {30,89},
        {31,38}, {108,93}, {103,44}, {113,38}, {117,39}, {146,24}, {76,53}, {84,12},
        {62,44}, {54,61},
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

    [MenuItem("Level/Buyuk Level Kur")]
    public static void Build()
    {
        string samplePath = FindSampleScene();
        if (samplePath == null) { Debug.LogError("'örnek level' sahnesi bulunamadı."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(TargetScenePath))
        {
            if (!EditorUtility.DisplayDialog("Buyuk Level",
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
        }
        foreach (Unit e in enemies)
            if (e != null && !keptEnemies.Contains(e.gameObject)) Object.DestroyImmediate(e.gameObject);

        // ---- Hurda yığınları ve variller ----
        PlaceProps<ScrapPile>(scene, wallMap, ScrapSpawns, "HurdaYigini");
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

        AddToBuildSettings(TargetScenePath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=green>Buyuk Level kuruldu: " + floor.Count + " yürünebilir kare, " +
                  keptEnemies.Count + " düşman, " + ScrapSpawns.GetLength(0) + " hurda, " +
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

    private static void PlaceProps<T>(Scene scene, Tilemap map, int[,] spawns, string prefabName) where T : Component
    {
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
        }
        for (int i = used; i < existing.Count; i++)
            if (existing[i] != null) Object.DestroyImmediate(existing[i]);
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
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
