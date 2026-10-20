#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Unity üst menüsünde: Kartlar > Kurulumu Yap
// Kart assetlerini, CardManager'ı, kart prefab'ını ve ekrandaki el panelini otomatik kurar.
// Birden fazla kez çalıştırmak güvenlidir (var olanları tekrar oluşturmaz).
public static class CardSystemSetup
{
    [MenuItem("Kartlar/Kurulumu Yap")]
    public static void Run()
    {
        EnsureFolder("Assets", "Cards");
        EnsureFolder("Assets", "Prefabs");

        // 1) Temel kartlar (varsa dokunmaz, böylece Inspector'da yaptığın ayarlar korunur)
        CardData attack = GetOrCreateCard("Assets/Cards/TemelSaldiri.asset", "Temel Saldırı", CardType.Attack, 20, 1, 0);
        CardData defense = GetOrCreateCard("Assets/Cards/TemelSavunma.asset", "Temel Savunma", CardType.Defense, 0, 1, 15);

        // 2) CardManager
        CardManager cm = Object.FindFirstObjectByType<CardManager>();
        if (cm == null)
        {
            GameObject go = new GameObject("CardManager");
            Undo.RegisterCreatedObjectUndo(go, "CardManager");
            cm = go.AddComponent<CardManager>();
        }
        SerializedObject cmSo = new SerializedObject(cm);
        cmSo.FindProperty("defaultAttackCard").objectReferenceValue = attack;
        cmSo.FindProperty("defaultDefenseCard").objectReferenceValue = defense;
        cmSo.ApplyModifiedProperties();

        // 3) Kart prefab'ı
        CardView cardPrefab = GetOrCreateCardPrefab("Assets/Prefabs/Card.prefab");

        // 4) El paneli (sadece yoksa kurulur)
        if (Object.FindFirstObjectByType<CardHandUI>() == null)
        {
            Canvas canvas = null;
            GameObject canvasGo = GameObject.Find("MainUI_Canvas");
            if (canvasGo != null) canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();

            if (canvas == null)
            {
                Debug.LogError("Sahnede Canvas bulunamadı, el paneli kurulamadı.");
            }
            else
            {
                GameObject handRoot = new GameObject("HandRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                Undo.RegisterCreatedObjectUndo(handRoot, "HandRoot");
                handRoot.transform.SetParent(canvas.transform, false);

                RectTransform hr = handRoot.GetComponent<RectTransform>();
                hr.anchorMin = new Vector2(0.5f, 0f);
                hr.anchorMax = new Vector2(0.5f, 0f);
                hr.pivot = new Vector2(0.5f, 0f);
                hr.anchoredPosition = new Vector2(0f, 10f);
                hr.sizeDelta = new Vector2(900f, 230f);

                HorizontalLayoutGroup layout = handRoot.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 12f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                GameObject ui = new GameObject("CardHandUI", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(ui, "CardHandUI");
                ui.transform.SetParent(canvas.transform, false);
                CardHandUI handUI = ui.AddComponent<CardHandUI>();

                SerializedObject uiSo = new SerializedObject(handUI);
                uiSo.FindProperty("handRoot").objectReferenceValue = handRoot;
                uiSo.FindProperty("container").objectReferenceValue = handRoot.transform;
                uiSo.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
                uiSo.ApplyModifiedProperties();
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("<color=green>Kart sistemi kuruldu.</color> Sahneyi kaydetmeyi (Ctrl+S) unutma.");
    }

    // ---------- Yardımcılar ----------

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static CardData GetOrCreateCard(string path, string cardName, CardType type, int damage, int range, int shield)
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
        if (card != null) return card;

        card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = cardName;
        card.cardType = type;
        card.damage = damage;
        card.range = range;
        card.shield = shield;
        card.cpuCost = 1;
        AssetDatabase.CreateAsset(card, path);
        return card;
    }

    private static CardView GetOrCreateCardPrefab(string path)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<CardView>();

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardView));
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 210f);

        Image bg = card.GetComponent<Image>();
        bg.color = Color.white;
        card.GetComponent<Button>().targetGraphic = bg;

        Image icon = CreateImage(card.transform, "Icon", new Vector2(0.15f, 0.58f), new Vector2(0.85f, 0.85f));
        TMP_Text costText = CreateText(card.transform, "Cost", new Vector2(0.02f, 0.86f), new Vector2(0.2f, 0.98f), 24f, new Color(0.7f, 1f, 1f));
        TMP_Text nameText = CreateText(card.transform, "Name", new Vector2(0.2f, 0.86f), new Vector2(0.98f, 0.98f), 16f, Color.white);
        TMP_Text valueText = CreateText(card.transform, "Value", new Vector2(0.25f, 0.34f), new Vector2(0.75f, 0.58f), 40f, Color.white);
        TMP_Text descText = CreateText(card.transform, "Description", new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.33f), 14f, Color.white);

        CardView view = card.GetComponent<CardView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("button").objectReferenceValue = card.GetComponent<Button>();
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("valueText").objectReferenceValue = valueText;
        so.FindProperty("costText").objectReferenceValue = costText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.ApplyModifiedProperties();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(card, path);
        Object.DestroyImmediate(card);
        return saved.GetComponent<CardView>();
    }

    private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false; // ikon verilince CardView açar
        return img;
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; // tıklamayı butona bırak
        return t;
    }
}
#endif
