#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Sadece Unity editöründe çalışan test kısayolları (oyunun yapısında yer almaz).
//   F1: +100 hurda ve bütün blueprint / yerden bulunan kartlar ele eklenir
//   F2: sahnedeki tüm düşmanları uyandırır (savaşı başlatır)
//   F3: bir düşmanı öldürür (ölünce düşenleri denemek için)
//   F4: ana drone'u yok eder (oyun kaybı ekranını denemek için)
public class DebugKisayollari : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject("DebugKisayollari");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<DebugKisayollari>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) GiveEverything();
        if (Input.GetKeyDown(KeyCode.F2)) WakeAllEnemies();
        if (Input.GetKeyDown(KeyCode.F3)) KillOneEnemy();
        if (Input.GetKeyDown(KeyCode.F4)) KillMain();
    }

    private void GiveEverything()
    {
        if (CardManager.Instance == null) return;
        ScrapInventory.Add(100);
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            CardData c = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c == null) continue;
            if (c.name.StartsWith("BP_") || c.name.StartsWith("P_"))
                CardManager.Instance.AddCard(c);
        }
        Debug.Log("F1: +100 hurda, tüm blueprint ve yerden bulunan kartlar eklendi.");
    }

    private void KillOneEnemy()
    {
        if (TurnManager.Instance == null) return;
        foreach (Unit e in new System.Collections.Generic.List<Unit>(TurnManager.Instance.enemyUnits))
        {
            if (e == null) continue;
            e.Kill();
            Debug.Log("F3: " + e.name + " öldürüldü.");
            return;
        }
    }

    private void KillMain()
    {
        if (TurnManager.Instance == null) return;
        foreach (Unit u in new System.Collections.Generic.List<Unit>(TurnManager.Instance.playerUnits))
            if (u != null && u.isMainDrone) { u.Kill(); return; }
    }

    private void WakeAllEnemies()
    {
        if (TurnManager.Instance == null) return;
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)) ai.isAwake = true;
        TurnManager.Instance.UpdateCombatState();
        Debug.Log("F2: tüm düşmanlar uyandırıldı.");
    }
}
#endif
