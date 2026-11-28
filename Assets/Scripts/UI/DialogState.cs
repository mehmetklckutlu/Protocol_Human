using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ekranda bir konuşma (tutorial diyaloğu, labirent girişi konuşması...) açık mı?
/// Açıkken oyuncu drone'u hareket ettirilemez, hurda aranamaz, tur bitirilemez.
/// Her konuşma açılırken Push, kapanırken Pop çağırır; sahne değişince sayaç sıfırlanır.
/// </summary>
public static class DialogState
{
    private static int count;

    public static bool Active => count > 0;

    public static void Push() { count++; }

    public static void Pop() { if (count > 0) count--; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        count = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) { count = 0; }
}
