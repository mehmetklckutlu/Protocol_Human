#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Unity üst menüsünde: Level > Ornek Level Kur
// "örnek level" sahnesine koridorlu, dallanan bir test haritası kurar.
// Birden fazla kez çalıştırmak güvenlidir (eski haritayı silip yeniden kurar).
public static class SampleLevelBuilder
{
    // Harita hücre koordinatından dünya koordinatına kaydırma
    private const int OX = -25;
    private const int OY = -15;

    // Zemin dikdörtgenleri: { x0, y0, x1, y1 } (sınırlar dahil)
    private static readonly int[][] FloorRects =
    {
        new[] { 1, 11, 8, 18 },    // Başlangıç odası
        new[] { 9, 13, 18, 15 },   // Batı koridoru
        new[] { 19, 13, 21, 15 },  // Kavşak (4 yollu)
        new[] { 19, 16, 21, 22 },  // Kuzey dal koridoru
        new[] { 14, 23, 26, 28 },  // Kuzey oda
        new[] { 19, 6, 21, 12 },   // Güney dal koridoru
        new[] { 14, 1, 26, 5 },    // Güney oda
        new[] { 22, 13, 35, 15 },  // Doğu koridoru
        new[] { 33, 16, 35, 21 },  // Kuzeye çıkan koridor
        new[] { 33, 7, 35, 12 },   // İkinci güney dal
        new[] { 29, 1, 39, 6 },    // Güneydoğu oda
        new[] { 36, 19, 40, 21 },  // Son odaya giden koridor
        new[] { 41, 15, 48, 25 },  // Son oda
    };

    // Sarı çizgili yönlendirme hattı: { x0, y0, x1, y1, yatay(1)/dikey(0) }
    private static readonly int[][] GuideSegments =
    {
        new[] { 3, 14, 34, 14, 1 },
        new[] { 34, 15, 34, 20, 0 },
        new[] { 35, 20, 44, 20, 1 },
    };

    // Tile numaraları (Human Protocol Tileset-ENSON_N)
    private static readonly int[] FloorTiles = { 2, 2, 2, 2, 35, 48, 49, 0 };
    // Duvarlar: tek tip, tutarlı şema (yön başına bir tile ailesi) + belli aralıkla havalandırma
    private const int TopWallTile = 20;        // zemine kuzeyden bakan duvar: yatay çift boru hattı (kesintisiz devam eder)
    private const int BottomWallTile = 43;     // zeminin altındaki duvar: sade panel
    private static readonly int[] SideWallTiles = { 3, 24 };  // yan duvarlar: dikey raylar
    private const int CornerWallTile = 57;
    private const int VentTile = 18;           // her 7 karede bir havalandırma
    private static readonly Color WallTint = new Color(0.48f, 0.52f, 0.58f, 1f); // duvarları koyulaştırır

    // Siperler (X'li kutular): { x, y, genişlik, yükseklik }
    private static readonly int[][] CoverBoxes =
    {
        new[] { 5, 16, 2, 1 }, new[] { 6, 12, 1, 1 }, new[] { 2, 17, 1, 1 },        // başlangıç odası
        new[] { 12, 15, 1, 1 }, new[] { 15, 13, 1, 1 },                             // batı koridoru
        new[] { 21, 18, 1, 1 }, new[] { 19, 20, 1, 1 },                             // kuzey dal
        new[] { 16, 25, 2, 1 }, new[] { 22, 26, 2, 2 }, new[] { 15, 27, 1, 1 },    // kuzey oda
        new[] { 19, 9, 1, 1 }, new[] { 21, 11, 1, 1 },                              // güney dal
        new[] { 16, 3, 2, 1 }, new[] { 22, 3, 2, 1 }, new[] { 19, 2, 1, 1 },       // güney oda
        new[] { 26, 13, 1, 1 }, new[] { 29, 15, 1, 1 }, new[] { 32, 13, 1, 1 },    // doğu koridoru
        new[] { 33, 18, 1, 1 }, new[] { 35, 17, 1, 1 },                             // kuzeye çıkan koridor
        new[] { 33, 10, 1, 1 }, new[] { 35, 9, 1, 1 },                              // ikinci güney dal
        new[] { 31, 4, 2, 1 }, new[] { 36, 2, 2, 2 }, new[] { 30, 1, 1, 1 },       // güneydoğu oda
        new[] { 38, 19, 1, 1 }, new[] { 38, 21, 1, 1 },                             // son odaya koridor
        new[] { 43, 17, 2, 1 }, new[] { 46, 22, 2, 2 }, new[] { 42, 24, 1, 1 }, new[] { 46, 16, 1, 1 }, // son oda
    };
    private static readonly int[] CoverTiles = { 14, 15, 31, 51 };

    private const int GuideTileA = 27;
    private const int GuideTileB = 37;

    private const int ObstacleLayer = 3; // GridManager.unwalkableLayer ile aynı katman

    [MenuItem("Level/Ornek Level Kur")]
    public static void Build()
    {
        string levelPath = FindScene(false);
        string codePath = FindScene(true);
        if (levelPath == null) { Debug.LogError("'ornek level' sahnesi bulunamadı."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        // Oyun sistemini (Player, Enemy, yöneticiler, arayüz) CodeScene'den getir
        if (!HasRoot(scene, "TurnManager") && codePath != null)
            ImportGameplayObjects(scene, codePath);

        // Tile'ları sahneler açıldıktan SONRA yükle (sahne açılırken kullanılmayan assetler bellekten atılır)
        Dictionary<int, TileBase> tiles = LoadTiles();
        if (tiles.Count < 50) { Debug.LogError("Tileset bulunamadı (" + tiles.Count + " tile)."); return; }

        // Önceki kurulumu temizle
        foreach (string n in new[] { "LevelGrid", "Enemy (2)", "Enemy (3)" })
        {
            GameObject old = FindRoot(scene, n);
            if (old != null) Object.DestroyImmediate(old);
        }

        // Harita
        System.Random rng = new System.Random(12345);
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach (int[] r in FloorRects)
            for (int x = r[0]; x <= r[2]; x++)
                for (int y = r[1]; y <= r[3]; y++)
                    floor.Add(new Vector2Int(x, y));

        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        foreach (Vector2Int f in floor)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int c = new Vector2Int(f.x + dx, f.y + dy);
                    if (!floor.Contains(c)) walls.Add(c);
                }

        GameObject gridGo = new GameObject("LevelGrid", typeof(Grid));
        SceneManager.MoveGameObjectToScene(gridGo, scene);

        Tilemap floorMap = CreateTilemap(gridGo.transform, "Floor", -10, false);
        Tilemap wallMap = CreateTilemap(gridGo.transform, "Walls", -5, true);
        Tilemap coverMap = CreateTilemap(gridGo.transform, "Cover", -4, true);
        wallMap.color = WallTint; // duvarlar zeminden belirgin şekilde koyu

        // Zemin
        foreach (Vector2Int c in floor)
            floorMap.SetTile(ToCell(c), tiles[FloorTiles[rng.Next(FloorTiles.Length)]]);

        // Sarı yönlendirme çizgisi (dikeylerde 90 derece döndürülür)
        int step = 0;
        foreach (int[] g in GuideSegments)
        {
            bool horizontal = g[4] == 1;
            int dx = g[2] > g[0] ? 1 : (g[2] < g[0] ? -1 : 0);
            int dy = g[3] > g[1] ? 1 : (g[3] < g[1] ? -1 : 0);
            int x = g[0], y = g[1];
            while (true)
            {
                Vector3Int p = ToCell(new Vector2Int(x, y));
                floorMap.SetTile(p, tiles[step % 4 == 3 ? GuideTileB : GuideTileA]);
                if (!horizontal)
                    floorMap.SetTransformMatrix(p, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), Vector3.one));
                step++;
                if (x == g[2] && y == g[3]) break;
                x += dx; y += dy;
            }
        }

        // Duvarlar: yöne göre tutarlı tile ailesi, görsel bütünlük için rastgelelik yok
        HashSet<int> usedWallTiles = new HashSet<int>();
        foreach (Vector2Int c in walls)
        {
            int id = PickWall(c, floor);
            usedWallTiles.Add(id);
            wallMap.SetTile(ToCell(c), tiles[id]);
        }

        // Siperler (üzerinde X olan kutular): yürünemez ve görüşü keser
        HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();
        foreach (int[] g in GuideSegments)
        {
            int gx = g[0], gy = g[1];
            int gdx = g[2] > g[0] ? 1 : (g[2] < g[0] ? -1 : 0);
            int gdy = g[3] > g[1] ? 1 : (g[3] < g[1] ? -1 : 0);
            while (true)
            {
                reserved.Add(new Vector2Int(gx, gy));
                if (gx == g[2] && gy == g[3]) break;
                gx += gdx; gy += gdy;
            }
        }
        reserved.Add(new Vector2Int(3, 14));   // oyuncu
        reserved.Add(new Vector2Int(44, 20));  // düşmanlar
        reserved.Add(new Vector2Int(20, 26));
        reserved.Add(new Vector2Int(34, 3));

        HashSet<int> usedCoverTiles = new HashSet<int>();
        foreach (int[] b in CoverBoxes)
            for (int bx = b[0]; bx < b[0] + b[2]; bx++)
                for (int by = b[1]; by < b[1] + b[3]; by++)
                {
                    Vector2Int c = new Vector2Int(bx, by);
                    if (!floor.Contains(c) || reserved.Contains(c))
                    {
                        Debug.LogWarning("Siper atlandi (zemin degil ya da yolun uzerinde): " + c);
                        continue;
                    }
                    int cid = CoverTiles[rng.Next(CoverTiles.Length)];
                    usedCoverTiles.Add(cid);
                    coverMap.SetTile(ToCell(c), tiles[cid]);
                }

        // Duvar tile'larına çarpışma ver (düşman görüş çizgisi ve yürünemezlik için)
        usedWallTiles.UnionWith(usedCoverTiles);
        foreach (int id in usedWallTiles)
        {
            if (tiles[id] is Tile t && t.colliderType != Tile.ColliderType.Grid)
            {
                t.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(t);
            }
        }
        AssetDatabase.SaveAssets();
        floorMap.CompressBounds();
        wallMap.CompressBounds();
        coverMap.CompressBounds();

        // Karakterler
        GameObject player = FindRoot(scene, "Player");
        GameObject enemy = FindRoot(scene, "Enemy");
        if (player != null) player.transform.position = ToWorld(3, 14);
        if (enemy != null)
        {
            enemy.transform.position = ToWorld(44, 20);
            enemy.name = "Enemy";
            PlaceEnemyCopy(enemy, "Enemy (2)", ToWorld(20, 26), scene); // kuzey oda
            PlaceEnemyCopy(enemy, "Enemy (3)", ToWorld(34, 3), scene);  // güneydoğu oda
        }

        // GridManager: sadece zemin tile'ı olan kareler yürünebilir, duvar tile'ları yürünemez
        GridManager gm = Object.FindFirstObjectByType<GridManager>();
        if (gm != null)
        {
            SerializedObject so = new SerializedObject(gm);
            SerializedProperty fp = so.FindProperty("floorTilemap");
            SerializedProperty wp = so.FindProperty("wallTilemap");
            SerializedProperty cp = so.FindProperty("coverTilemap");
            if (fp != null) fp.objectReferenceValue = floorMap;
            if (wp != null) wp.objectReferenceValue = wallMap;
            if (cp != null) cp.objectReferenceValue = coverMap;
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("Sahnede GridManager yok.");

        // Kamera: oyuncuya odaklan ve haritaya sınırla
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Vector3 pp = ToWorld(3, 14);
            cam.transform.position = new Vector3(pp.x, pp.y, -10f);
            cam.orthographic = true;
            cam.orthographicSize = 6f;

            TacticalCamera tc = cam.GetComponent<TacticalCamera>();
            if (tc == null) tc = cam.gameObject.AddComponent<TacticalCamera>();
            SerializedObject cso = new SerializedObject(tc);
            SerializedProperty bp = cso.FindProperty("boundsTilemap");
            SerializedProperty mp = cso.FindProperty("edgeMargin");
            SerializedProperty tp = cso.FindProperty("targetToFocus");
            if (bp != null) bp.objectReferenceValue = floorMap;
            if (mp != null) mp.floatValue = 1.5f;
            if (tp != null && player != null) tp.objectReferenceValue = player.transform;
            cso.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=green>Ornek level kuruldu ve sahne kaydedildi.</color>");
    }

    // ---------- Yardımcılar ----------

    private static void ImportGameplayObjects(Scene target, string codePath)
    {
        Scene code = EditorSceneManager.OpenScene(codePath, OpenSceneMode.Additive);
        string[] keep = { "Main Camera", "Player", "Enemy", "Grid Manager", "CursorHighlighter",
                          "HighlightManager", "EventSystem", "TurnManager", "MainUI_Canvas", "CardManager" };

        foreach (GameObject root in code.GetRootGameObjects())
        {
            bool take = System.Array.IndexOf(keep, root.name) >= 0 || root.transform.Find("HighlightTilemap") != null;
            if (!take) continue;

            if (root.name == "Main Camera")
            {
                GameObject defaultCam = FindRoot(target, "Main Camera");
                if (defaultCam != null) Object.DestroyImmediate(defaultCam);
            }
            SceneManager.MoveGameObjectToScene(root, target);
        }

        SceneManager.SetActiveScene(target);
        EditorSceneManager.CloseScene(code, true); // CodeScene dosyası değişmez (kaydedilmiyor)
    }

    private static void PlaceEnemyCopy(GameObject original, string name, Vector3 pos, Scene scene)
    {
        GameObject copy = Object.Instantiate(original);
        copy.name = name;
        copy.transform.position = pos;
        SceneManager.MoveGameObjectToScene(copy, scene);
    }

    private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, bool collider)
    {
        GameObject go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        if (collider)
        {
            go.AddComponent<TilemapCollider2D>();
            go.layer = ObstacleLayer;
        }
        return go.GetComponent<Tilemap>();
    }

    // Duvar hücresinin hangi yöne baktığına göre tutarlı tile seçer
    private static int PickWall(Vector2Int c, HashSet<Vector2Int> floor)
    {
        bool floorBelow = floor.Contains(new Vector2Int(c.x, c.y - 1));
        bool floorAbove = floor.Contains(new Vector2Int(c.x, c.y + 1));
        bool floorSide = floor.Contains(new Vector2Int(c.x - 1, c.y)) || floor.Contains(new Vector2Int(c.x + 1, c.y));
        bool vent = ((c.x * 3 + c.y * 5) % 7) == 0;

        if (floorBelow) return vent ? VentTile : TopWallTile;
        if (floorAbove) return vent ? VentTile : BottomWallTile;
        if (floorSide) return vent ? VentTile : SideWallTiles[Mathf.Abs(c.y) % SideWallTiles.Length];
        return CornerWallTile;
    }

    private static Vector3Int ToCell(Vector2Int c) { return new Vector3Int(c.x + OX, c.y + OY, 0); }
    private static Vector3 ToWorld(int x, int y) { return new Vector3(x + OX + 0.5f, y + OY + 0.5f, 0f); }

    private static Dictionary<int, TileBase> LoadTiles()
    {
        Dictionary<int, TileBase> d = new Dictionary<int, TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("Human Protocol Tileset-ENSON_ t:TileBase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(path);
            int us = n.LastIndexOf('_');
            if (us >= 0 && int.TryParse(n.Substring(us + 1), out int i))
                d[i] = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        }
        return d;
    }

    private static string FindScene(bool code)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileName(path).ToLowerInvariant();
            if (code && file == "codescene.unity") return path;
            if (!code && file.Contains("level")) return path;
        }
        return null;
    }

    private static bool HasRoot(Scene s, string name) { return FindRoot(s, name) != null; }

    private static GameObject FindRoot(Scene s, string name)
    {
        foreach (GameObject r in s.GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }
}
#endif
