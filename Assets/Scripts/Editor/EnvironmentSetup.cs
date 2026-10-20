#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Unity üst menüsünde: Level > Hurda ve Varil Ekle
// - "Muttefik Cagir" kartı assetini oluşturur (yoksa)
// - Açık sahnede oyuncunun yakınına 1 hurda yığını ve 2 patlayan varil koyar (daha önce konduysa dokunmaz)
public static class EnvironmentSetup
{
    [MenuItem("Level/Hurda ve Varil Ekle")]
    public static void Run()
    {
        CardData allyCard = GetOrCreateAllyCard();

        GameObject player = GameObject.Find("Player");
        GridManager gmComp = Object.FindFirstObjectByType<GridManager>();
        if (player == null || gmComp == null)
        {
            Debug.LogError("Sahnede 'Player' veya GridManager bulunamadı.");
            return;
        }

        SerializedObject gso = new SerializedObject(gmComp);
        Vector3 origin = gso.FindProperty("gridOrigin").vector3Value;
        float cell = gso.FindProperty("cellSize").floatValue;

        Tilemap floor = FindMap("Floor"), walls = FindMap("Walls"), cover = FindMap("Cover");
        if (floor == null) { Debug.LogError("LevelGrid/Floor bulunamadı."); return; }

        System.Func<Vector2Int, Vector3> toWorld = c => origin + new Vector3(c.x * cell + cell / 2f, c.y * cell + cell / 2f, 0f);
        System.Func<Vector2Int, bool> free = c =>
        {
            Vector3Int tc = floor.WorldToCell(toWorld(c));
            if (!floor.HasTile(tc)) return false;
            if (walls != null && walls.HasTile(tc)) return false;
            if (cover != null && cover.HasTile(tc)) return false;
            return true;
        };
        System.Func<Vector2Int, bool> roomy = c =>
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (!free(c + new Vector2Int(dx, dy))) return false;
            return true;
        };

        Vector2Int pc = new Vector2Int(Mathf.FloorToInt((player.transform.position.x - origin.x) / cell),
                                       Mathf.FloorToInt((player.transform.position.y - origin.y) / cell));

        if (Object.FindFirstObjectByType<ScrapPile>() == null)
        {
            Vector2Int? pileCell = FindCell(pc, 3, 7, roomy, new List<Vector2Int>(), 3);
            if (pileCell == null) { Debug.LogError("Hurda yığını için boş yer bulunamadı."); return; }

            GameObject pile = new GameObject("Hurda Yigini");
            pile.transform.position = toWorld(pileCell.Value);
            ScrapPile sp = pile.AddComponent<ScrapPile>();
            SerializedObject so = new SerializedObject(sp);
            so.FindProperty("allyCard").objectReferenceValue = allyCard;
            so.ApplyModifiedProperties();
            Undo.RegisterCreatedObjectUndo(pile, "Hurda Yigini");

            List<Vector2Int> avoid = new List<Vector2Int> { pileCell.Value, pileCell.Value + Vector2Int.down };
            Vector2Int? b1 = FindCell(pc, 3, 6, roomy, avoid, 3);
            if (b1 != null)
            {
                MakeBarrel("Patlayan Varil", toWorld(b1.Value));
                Vector2Int b2c = b1.Value + new Vector2Int(2, 0);
                if (!free(b2c) || avoid.Contains(b2c)) b2c = b1.Value + new Vector2Int(-2, 0);
                if (free(b2c) && !avoid.Contains(b2c)) MakeBarrel("Patlayan Varil (2)", toWorld(b2c));
            }
        }
        else Debug.Log("Sahnede zaten hurda yığını var, yeniden eklenmedi.");

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=green>Hurda yığını ve patlayan variller eklendi, sahne kaydedildi.</color>");
    }

    private static Vector2Int? FindCell(Vector2Int center, int minDist, int maxDist,
                                        System.Func<Vector2Int, bool> ok, List<Vector2Int> avoid, int avoidDist)
    {
        for (int d = minDist; d <= maxDist; d++)
            for (int dx = -d; dx <= d; dx++)
                for (int dy = -d; dy <= d; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != d) continue;
                    Vector2Int c = center + new Vector2Int(dx, dy);
                    bool near = false;
                    foreach (Vector2Int a in avoid)
                        if (Mathf.Abs(a.x - c.x) + Mathf.Abs(a.y - c.y) < avoidDist) near = true;
                    if (!near && ok(c)) return c;
                }
        return null;
    }

    private static void MakeBarrel(string name, Vector3 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<ExplosiveBarrel>();
        Undo.RegisterCreatedObjectUndo(go, name);
    }

    private static Tilemap FindMap(string name)
    {
        GameObject g = GameObject.Find("LevelGrid/" + name);
        return g != null ? g.GetComponent<Tilemap>() : null;
    }

    private static CardData GetOrCreateAllyCard()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cards")) AssetDatabase.CreateFolder("Assets", "Cards");
        const string path = "Assets/Cards/MuttefikCagir.asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
        if (card != null) return card;

        card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "Müttefik Çağır";
        card.cardType = CardType.Summon;
        card.cpuCost = 1;
        card.allyHealth = 60;
        AssetDatabase.CreateAsset(card, path);
        AssetDatabase.SaveAssets();
        return card;
    }
}
#endif
