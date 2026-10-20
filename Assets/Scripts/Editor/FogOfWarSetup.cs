#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Unity üst menüsünde: Level > Gorus Sistemi Ekle
// Açık sahneye FogOfWar objesini ekler (zaten varsa dokunmaz).
public static class FogOfWarSetup
{
    [MenuItem("Level/Gorus Sistemi Ekle")]
    public static void Add()
    {
        FogOfWar existing = Object.FindFirstObjectByType<FogOfWar>();
        if (existing != null)
        {
            Debug.Log("Sahnede FogOfWar zaten var.");
            Selection.activeObject = existing.gameObject;
            return;
        }

        GameObject go = new GameObject("FogOfWar");
        go.AddComponent<FogOfWar>();
        Undo.RegisterCreatedObjectUndo(go, "FogOfWar");
        Selection.activeObject = go;

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=green>Gorus sistemi (FogOfWar) eklendi ve sahne kaydedildi.</color>");
    }
}
#endif
