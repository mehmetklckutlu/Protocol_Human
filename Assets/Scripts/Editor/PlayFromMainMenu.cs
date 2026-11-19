using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string ScenePath = "Assets/Scenes/AnaMenü.unity";
    private const string MenuPath = "Protocol Human/Play'de Ana Menüden Başla";
    private const string PrefKey = "ProtocolHuman.PlayFromMainMenu2";

    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += Apply;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    // Ana menü Build Settings'te her zaman 0. sırada olsun (oyun build'de de menüyle açılsın).
    private static void EnsureMenuFirstInBuild()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int idx = list.FindIndex(s => s.path == ScenePath);
        if (idx == 0 && list[0].enabled) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
        if (idx >= 0) list.RemoveAt(idx);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void Apply()
    {
        EnsureMenuFirstInBuild();
        EditorSceneManager.playModeStartScene = Enabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
            : null;
    }
}
