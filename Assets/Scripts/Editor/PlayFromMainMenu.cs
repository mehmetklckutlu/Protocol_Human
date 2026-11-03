using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string ScenePath = "Assets/Scenes/AnaMenü.unity";
    private const string MenuPath = "Protocol Human/Play'de Ana Menüden Başla";
    private const string PrefKey = "ProtocolHuman.PlayFromMainMenu";

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

    private static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
            : null;
    }
}
