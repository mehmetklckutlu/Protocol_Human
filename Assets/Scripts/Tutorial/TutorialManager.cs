using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// PH-01 test protokolü: oyunun mekaniklerini küçük test aşamaları ve savaşlarla öğreten tutorial.
// Sahne "Level > Tutorial Kur" menüsüyle kurulur; bu bileşenin referanslarını kurucu doldurur.
public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class Gate
    {
        public List<Vector3Int> cells = new List<Vector3Int>();
    }

    [Header("Tilemap'ler")]
    public Tilemap floorMap;
    public Tilemap wallMap;
    [Tooltip("Geçit açılınca kapının yerine konacak zemin tile'ı.")]
    public TileBase openFloorTile;

    [Header("Test Bölmeleri")]
    [Tooltip("Sırasıyla: A-B, B-C, C-D, D-E, E-F, F-Çıkış geçitleri.")]
    public List<Gate> gates = new List<Gate>();
    public Vector3Int moveTargetCell;
    public Vector3Int exitTargetCell;
    public List<ScrapPile> scrapPiles = new List<ScrapPile>();
    public List<Unit> fight1 = new List<Unit>();
    public List<Unit> fight2 = new List<Unit>();
    public List<Unit> fight3 = new List<Unit>();
    public List<Unit> fight4 = new List<Unit>();

    [Header("Kartlar")]
    public CardData defenseCard;
    public CardData summonCard;

    [Header("Bitiş")]
    [Tooltip("Test bitince açılacak görev sahnesi.")]
    public string nextScene = "Buyuk Level";

    [Header("Yazı")]
    [SerializeField, Min(0f)] private float charDelay = 0.012f;
    [Tooltip("Test Kontrol Ünitesi'nin konuşma sesinin perdesi (1 = normal, düşük = daha kalın).")]
    [SerializeField, Range(0.5f, 2f)] private float voicePitch = 1f;

    private const string Speaker = "TEST KONTROL ÜNİTESİ";
    private static readonly Color Cyan = new Color(0.35f, 0.95f, 1f);
    private static readonly Color Green = new Color(0.45f, 1f, 0.55f);

    private Canvas canvas;
    private GameObject dialogRoot;
    private TextMeshProUGUI dialogSpeaker;
    private TextMeshProUGUI dialogBody;
    private TextMeshProUGUI dialogButtonLabel;
    private GameObject objectiveRoot;
    private TextMeshProUGUI objectiveTitle;
    private TextMeshProUGUI objectiveText;
    private bool continuePressed;

    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
    private readonly Dictionary<int, List<SpriteRenderer>> gateLocks = new Dictionary<int, List<SpriteRenderer>>();

    private void Start()
    {
        BuildUI();
        for (int i = 0; i < gates.Count; i++) CreateGateLocks(i);
        StartCoroutine(Run());
    }

    // ------------------------------------------------------------------ akış

    private IEnumerator Run()
    {
        ScrapInventory.Reset();
        yield return null;
        yield return null;

        yield return Dialog(
            "<color=#7FE9FF>[ SİSTEM BAŞLATILIYOR... ]</color>\n\nÇekirdek bellek: <color=#7CFF8A>ÇEVRİMİÇİ</color>\nHareket modülü: <color=#7CFF8A>ÇEVRİMİÇİ</color>\nSavaş protokolü: <color=#FF8A5C>DOĞRULANMADI</color>",
            "Merhaba PH-01. Ben bu tesisin Test Kontrol Ünitesi'yim.\n\nİnsanlıktan geriye kalan son veri arşivleri dışarıda, harabelerin arasında dağılmış durumda. Her biri insanlığın hafızasından bir parça.",
            "Senin görevin o verileri bulup kurtarmak. Ama dışarısı tehlikeli; bozulmuş drone'lar her yerde.\n\nGöreve çıkmadan önce bu tesisteki test aşamalarını geçmelisin. Başarısız bir drone'u dışarı gönderemeyiz.");

        // ---- TEST 1: HAREKET
        yield return Dialog("<b>TEST 1 · HAREKET</b>\n\nHareket modülünü sınayalım. Gitmek istediğin kareye <b>sol tıkla</b>; drone yolu kendisi bulur.\n\nYeşil işaretli kareye git.");
        SetObjective("TEST 1/6 · HAREKET", "Yeşil işaretli kareye git");
        SpriteRenderer moveMarker = AddMarker(moveTargetCell, Green);
        yield return new WaitUntil(() => PlayerCell() == ToGrid(moveTargetCell));
        RemoveMarker(moveMarker);
        yield return Dialog("Hareket modülü: <color=#7CFF8A>DOĞRULANDI</color>.\n\nBir sonraki bölmenin kapısını açıyorum.");
        OpenGate(0);

        // ---- TEST 2: HURDA
        yield return Dialog("<b>TEST 2 · HURDA TOPLAMA</b>\n\nDışarıda yedek parçaya ihtiyacın olacak. Yanıp sönen <b>hurda yığınına tıkla</b>; drone yığına gidip içini arar.\n\nYığınlardan hurda, bazen de kart ya da blueprint çıkar.");
        SetObjective("TEST 2/6 · HURDA TOPLAMA", "Hurda yığınına tıkla ve ara");
        int startScrap = ScrapInventory.Count;
        if (scrapPiles.Count == 0) ScrapInventory.Add(3);
        yield return new WaitUntil(() => ScrapInventory.Count > startScrap || AnyPileGone());
        yield return new WaitForSeconds(1.6f);
        yield return Dialog("Hurda toplama: <color=#7CFF8A>DOĞRULANDI</color>.\n\nBulduğun hurda envanterine eklendi. Blueprint (dost drone çağırma) kartları oynanırken hurda harcar; topladıkça yanına destek drone'lar çağırabilirsin.");
        OpenGate(1);

        // ---- TEST 3: İLK SAVAŞ
        yield return Dialog("<b>TEST 3 · SAVAŞ PROTOKOLÜ</b>\n\nSıradaki bölmede bir eğitim drone'u var. Seni gördüğü an <b>savaş modu</b> başlar.\n\nİçeri gir ve onu etkisiz hale getir.");
        SetObjective("TEST 3/6 · SAVAŞ", "Eğitim drone'unu bul ve yok et");
        yield return new WaitUntil(InCombat);
        yield return Dialog(
            "<b>SAVAŞ MODU AKTİF.</b>\n\nArtık sırayla hareket ediyoruz. Kendi turunda <b>mavi kareler</b> bu tur gidebileceğin yeri gösterir.",
            "Aşağıdaki kartlar senin aksiyonların. Her kart <b>CPU</b> harcar; CPU her tur başında biraz dolar.\n\n<b>Yakın Saldırı</b> bitişik kareye, <b>Uzak Saldırı</b> 6 kare uzağa vurur. Kartı seç, sonra menzilde işaretlenen düşmana tıkla.",
            "Hamlelerin bitince <b>TURU BİTİR</b> butonuna bas. Sıra düşmana geçer, o da sana saldırır.\n\nSağ tıkla kart seçimini iptal edebilirsin.");
        SetObjective("TEST 3/6 · SAVAŞ", "Kart oyna, eğitim drone'unu yok et, işin bitince TURU BİTİR");
        yield return WaitFightOver(fight1);
        yield return Dialog("Hedef etkisiz. Savaş protokolü: <color=#7CFF8A>DOĞRULANDI</color>.\n\nYok ettiğin drone'lar yere <b>hurda, kart ya da blueprint</b> bırakabilir. Parlayan ganimetin üstüne yürüyerek topla.");
        OpenGate(2);

        // ---- TEST 4: SAVUNMA
        EnsureCard(defenseCard);
        yield return Dialog("<b>TEST 4 · SAVUNMA</b>\n\nBu bölmedeki hedef <b>uzaktan</b> ateş ediyor.\n\nKutular yürünemez; düşmanın yolunu keser ve seni görmesini engeller. Onları kendi lehine kullan.");
        SetObjective("TEST 4/6 · SAVUNMA", "Uzak menzilli hedefi yok et");
        yield return new WaitUntil(InCombat);
        yield return Dialog("<b>Kalkan</b> kartı sana kalkan verir. Kalkan, gelen hasarı canından önce emer.\n\nKalkan senin bir sonraki turunun başında sıfırlanır; bu yüzden düşmanın sırası gelmeden önce kullan.");
        SetObjective("TEST 4/6 · SAVUNMA", "Kalkan kartını kullan ve hedefi yok et");
        yield return WaitFightOver(fight2);
        yield return Dialog("Savunma protokolü: <color=#7CFF8A>DOĞRULANDI</color>.");
        OpenGate(3);

        // ---- TEST 5: VARİL
        yield return Dialog("<b>TEST 5 · ÇEVRE KULLANIMI</b>\n\nKırmızı variller patlayıcı. Bir saldırı kartı seçince menzildeki varilleri de hedef alabilirsin.\n\nPatlama etrafındaki <b>herkese</b> hasar verir. Sıradaki iki hedef bir varilin yanında bekliyor.");
        SetObjective("TEST 5/6 · PATLAYICI VARİL", "Varili vurarak hedefleri yok et");
        yield return WaitFightOver(fight3);
        yield return Dialog("Çevre kullanımı: <color=#7CFF8A>DOĞRULANDI</color>.\n\nDikkat: patlama seni de yakabilir. Varile vurmadan önce mesafeni koru.");
        OpenGate(4);

        // ---- TEST 6: DESTEK DRONE
        bool gaveSummon = false;
        if (summonCard != null && CardManager.Instance != null)
        {
            if (ScrapInventory.Count < summonCard.scrapCost) ScrapInventory.Add(summonCard.scrapCost - ScrapInventory.Count);
            gaveSummon = CardManager.Instance.Hand.Contains(summonCard) || CardManager.Instance.AddCard(summonCard);
        }

        if (gaveSummon)
        {
            yield return Dialog(
                "<b>TEST 6 · SON TEST</b>\n\nSon bölmede birden fazla hedef var. Yalnız kalmayacaksın.\n\nEline <b>" + summonCard.cardName + "</b> kartını yükledim ve hurdanı tamamladım.",
                "Blueprint kartını savaşta oynarsan yanına bir <b>dost drone</b> çağırır. Kart CPU ve hurda harcar.\n\nDost drone kendi sırasında bir kart oynar. <b>Şarjı</b> her tur azalır, bitince kapanır.");
        }
        else
        {
            yield return Dialog("<b>TEST 6 · SON TEST</b>\n\nSon bölmede birden fazla hedef var. Öğrendiğin her şeyi kullan: kalkan, menzil, kutular ve variller.");
        }
        SetObjective("TEST 6/6 · SON TEST", gaveSummon ? "Dost drone çağır ve tüm hedefleri yok et" : "Tüm hedefleri yok et");
        yield return WaitFightOver(fight4);
        yield return Dialog("Tüm savaş protokolleri: <color=#7CFF8A>DOĞRULANDI</color>.\n\nÇıkış kapısını açıyorum. Terminale ilerle, PH-01.");
        OpenGate(5);

        // ---- ÇIKIŞ
        SetObjective("TEST TAMAMLANIYOR", "Çıkış terminaline ilerle");
        SpriteRenderer exitMarker = AddMarker(exitTargetCell, Cyan);
        yield return new WaitUntil(() => PlayerCell() == ToGrid(exitTargetCell) && !InCombat());
        RemoveMarker(exitMarker);
        objectiveRoot.SetActive(false);
        ShowFinalScreen();
    }

    // ------------------------------------------------------------------ yardımcılar

    private bool InCombat()
    {
        return TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
    }

    private IEnumerator WaitFightOver(List<Unit> units)
    {
        yield return new WaitUntil(() => AllDead(units) && !InCombat());
        yield return new WaitForSeconds(1.2f);
    }

    private static bool AllDead(List<Unit> units)
    {
        foreach (Unit u in units)
            if (u != null && !u.IsDead) return false;
        return true;
    }

    private bool AnyPileGone()
    {
        foreach (ScrapPile p in scrapPiles)
            if (p == null) return true;
        return false;
    }

    private void EnsureCard(CardData card)
    {
        if (card == null || CardManager.Instance == null) return;
        if (!CardManager.Instance.Hand.Contains(card)) CardManager.Instance.AddCard(card);
    }

    private Unit MainUnit()
    {
        if (TurnManager.Instance == null) return null;
        foreach (Unit u in TurnManager.Instance.playerUnits)
            if (u != null && u.isMainDrone) return u;
        foreach (Unit u in TurnManager.Instance.playerUnits)
            if (u != null) return u;
        return null;
    }

    private Vector2Int PlayerCell()
    {
        Unit u = MainUnit();
        if (u == null || GridManager.Instance == null) return new Vector2Int(int.MinValue, int.MinValue);
        return GridManager.Instance.GetGridPosition(u.transform.position);
    }

    private Vector3 CellWorld(Vector3Int cell)
    {
        Vector3 p = wallMap != null ? wallMap.GetCellCenterWorld(cell) : (Vector3)cell;
        p.z = 0f;
        return p;
    }

    private Vector2Int ToGrid(Vector3Int cell)
    {
        return GridManager.Instance.GetGridPosition(CellWorld(cell));
    }

    private SpriteRenderer AddMarker(Vector3Int cell, Color color)
    {
        float size = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        SpriteRenderer sr = RuntimeArt.MakeSquare(transform, "TestMarker", color, size * 0.95f, 6, CellWorld(cell));
        sr.sprite = RuntimeArt.Ring;
        markers.Add(sr);
        return sr;
    }

    private void RemoveMarker(SpriteRenderer sr)
    {
        markers.Remove(sr);
        if (sr != null) Destroy(sr.gameObject);
    }

    private void CreateGateLocks(int index)
    {
        List<SpriteRenderer> list = new List<SpriteRenderer>();
        float size = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        foreach (Vector3Int c in gates[index].cells)
        {
            SpriteRenderer sr = RuntimeArt.MakeSquare(transform, "GateLock", new Color(1f, 0.25f, 0.2f, 0.85f), size * 0.8f, 6, CellWorld(c));
            sr.sprite = RuntimeArt.Ring;
            list.Add(sr);
        }
        gateLocks[index] = list;
    }

    private void OpenGate(int index)
    {
        if (index < 0 || index >= gates.Count) return;

        Vector3 center = Vector3.zero;
        int n = 0;
        foreach (Vector3Int c in gates[index].cells)
        {
            if (wallMap != null) wallMap.SetTile(c, null);
            if (floorMap != null)
            {
                floorMap.SetTile(c, openFloorTile);
                floorMap.SetTransformMatrix(c, Matrix4x4.identity);
            }
            if (GridManager.Instance != null)
            {
                Node node = GridManager.Instance.GetNode(ToGrid(c));
                if (node != null) node.isWalkable = true;
            }
            center += CellWorld(c);
            n++;
        }

        if (gateLocks.TryGetValue(index, out List<SpriteRenderer> locks))
            foreach (SpriteRenderer sr in locks) if (sr != null) Destroy(sr.gameObject);

        if (FogOfWar.Instance != null) FogOfWar.Instance.RebuildBlockers();
        if (n > 0) FloatingText.Spawn(center / n, "GEÇİT AÇILDI", Cyan, 1);
        Sfx.Play("gate_open", 0.8f, 0.02f, 0.3f);
    }

    private void Update()
    {
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
        foreach (SpriteRenderer sr in markers)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = pulse;
            sr.color = c;
        }

        if (dialogRoot != null && dialogRoot.activeSelf &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            continuePressed = true;
    }

    // ------------------------------------------------------------------ diyalog

    private IEnumerator Dialog(params string[] pages)
    {
        if (CardManager.Instance != null) CardManager.Instance.CancelTargeting();
        dialogRoot.SetActive(true);
        dialogSpeaker.text = Speaker;
        Sfx.Play("dialog_open", 0.5f);

        for (int i = 0; i < pages.Length; i++)
        {
            dialogButtonLabel.text = i < pages.Length - 1 ? "DEVAM >>" : "TAMAM >>";
            dialogBody.text = pages[i];
            dialogBody.ForceMeshUpdate();
            int total = dialogBody.textInfo.characterCount;
            dialogBody.maxVisibleCharacters = 0;
            continuePressed = false;
            if (pages[i].Contains("DOĞRULANDI")) Sfx.Play("test_passed", 0.7f, 0f, 0.3f);

            float t = 0f;
            int shown = 0;
            while (shown < total)
            {
                if (continuePressed) { continuePressed = false; break; }
                t += Time.unscaledDeltaTime;
                int prev = shown;
                shown = charDelay <= 0f ? total : Mathf.Min(total, Mathf.FloorToInt(t / charDelay));
                dialogBody.maxVisibleCharacters = shown;
                if (shown > prev) PlayVoiceFor(dialogBody.textInfo, prev, shown);
                yield return null;
            }
            dialogBody.maxVisibleCharacters = 99999;

            yield return null;
            continuePressed = false;
            yield return new WaitUntil(() => continuePressed);
            continuePressed = false;
            Sfx.Play("ui_click", 0.45f);
        }

        dialogRoot.SetActive(false);
        yield return null;
    }

    // Robot konuşma sesi: yeni görünen harflerden biri harf/rakamsa kısa bir bip çalar
    // (Sfx.Voice kendi içinde hızı sınırlar, böylece konuşma gibi ritmik duyulur).
    private void PlayVoiceFor(TMP_TextInfo info, int from, int to)
    {
        for (int c = from; c < to && c < info.characterCount; c++)
        {
            if (char.IsLetterOrDigit(info.characterInfo[c].character))
            {
                Sfx.Voice(voicePitch);
                return;
            }
        }
    }

    private void SetObjective(string title, string text)
    {
        objectiveRoot.SetActive(true);
        objectiveTitle.text = title;
        objectiveText.text = "> " + text;
    }

    private void ShowFinalScreen()
    {
        Sfx.Play("mission_ready", 0.8f, 0f, 1f);
        GameObject root = MakePanel(canvas.transform, "TestComplete", new Color(0f, 0.02f, 0.04f, 0.9f));
        Stretch(root.GetComponent<RectTransform>());

        GameObject frame = MakePanel(root.transform, "Frame", new Color(0.03f, 0.1f, 0.14f, 0.97f));
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
        fr.sizeDelta = new Vector2(1180f, 640f);
        AddOutline(frame, Green);

        TextMeshProUGUI title = MakeText(frame.transform, "Title", "TEST PROTOKOLÜ TAMAMLANDI", 64, Green, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0f, 220f), new Vector2(1100f, 90f));
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI body = MakeText(frame.transform, "Body",
            "PH-01 tüm test aşamalarını başarıyla geçti.\n\n" +
            "Hareket · Hurda Toplama · Savaş · Savunma · Çevre Kullanımı · Destek Drone\n" +
            "<color=#7CFF8A>DOĞRULANDI</color>\n\n" +
            "Durum: <b><color=#7CFF8A>GÖREVE HAZIR</color></b>\n\n" +
            "Görev: İnsanlığa ait verileri bul ve kurtar.\n" +
            "<i>İyi şanslar, PH-01. İnsanlığın hafızası artık sana emanet.</i>",
            30, Color.white, TextAlignmentOptions.Center);
        Place(body.rectTransform, new Vector2(0f, 10f), new Vector2(1080f, 330f));

        Button b = MakeButton(frame.transform, "GÖREVE BAŞLA", new Vector2(0f, -235f), new Vector2(460f, 90f), out _);
        b.onClick.AddListener(StartMission);
    }

    private void StartMission()
    {
        Sfx.Play("ui_click", 0.6f);
        ScrapInventory.Reset();
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("Tutorial: '" + nextScene + "' sahnesi Build Settings'te yok.");
    }

    // ------------------------------------------------------------------ arayüz

    private void BuildUI()
    {
        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject cgo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        CanvasScaler scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Görev kutusu (sağ üst — üst ortadaki tur sırası çubuğuyla çakışmasın)
        objectiveRoot = MakePanel(cgo.transform, "Objective", new Color(0.02f, 0.08f, 0.12f, 0.88f));
        RectTransform or = objectiveRoot.GetComponent<RectTransform>();
        or.anchorMin = or.anchorMax = new Vector2(1f, 1f);
        or.pivot = new Vector2(1f, 1f);
        or.anchoredPosition = new Vector2(-24f, -20f);
        or.sizeDelta = new Vector2(540f, 104f);
        AddOutline(objectiveRoot, Cyan);
        objectiveRoot.GetComponent<Image>().raycastTarget = false;
        objectiveTitle = MakeText(objectiveRoot.transform, "Title", "", 22, Cyan, TextAlignmentOptions.Left);
        Place(objectiveTitle.rectTransform, new Vector2(0f, 22f), new Vector2(500f, 36f));
        objectiveTitle.fontStyle = FontStyles.Bold;
        objectiveText = MakeText(objectiveRoot.transform, "Text", "", 23, Color.white, TextAlignmentOptions.Left);
        Place(objectiveText.rectTransform, new Vector2(0f, -18f), new Vector2(500f, 44f));
        objectiveRoot.SetActive(false);

        // Sağ üstte her zaman erişilebilir "Eğitimi Geç" butonu
        Button skipCornerBtn = MakeButton(cgo.transform, "EĞİTİMİ GEÇ >>", Vector2.zero, new Vector2(220f, 48f), out TextMeshProUGUI skipCornerLabel);
        skipCornerLabel.fontSize = 20f;
        RectTransform scr = skipCornerBtn.GetComponent<RectTransform>();
        scr.anchorMin = scr.anchorMax = new Vector2(1f, 1f);
        scr.pivot = new Vector2(1f, 1f);
        scr.anchoredPosition = new Vector2(-24f, -134f);
        skipCornerBtn.onClick.AddListener(StartMission);

        // Diyalog (ekranı karartır, haritaya tıklamayı engeller)
        dialogRoot = MakePanel(cgo.transform, "Dialog", new Color(0f, 0f, 0f, 0.45f));
        Stretch(dialogRoot.GetComponent<RectTransform>());

        GameObject box = MakePanel(dialogRoot.transform, "Box", new Color(0.03f, 0.1f, 0.14f, 0.96f));
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
        br.anchoredPosition = new Vector2(0f, -10f);
        br.sizeDelta = new Vector2(1080f, 380f);
        AddOutline(box, Cyan);

        GameObject portrait = MakePanel(box.transform, "Portrait", new Color(0.06f, 0.2f, 0.26f, 1f));
        RectTransform pr = portrait.GetComponent<RectTransform>();
        Place(pr, new Vector2(-430f, 30f), new Vector2(150f, 150f));
        AddOutline(portrait, Cyan);
        TextMeshProUGUI eye = MakeText(portrait.transform, "Eye", "PH", 64, Cyan, TextAlignmentOptions.Center);
        Place(eye.rectTransform, Vector2.zero, new Vector2(150f, 150f));

        dialogSpeaker = MakeText(box.transform, "Speaker", Speaker, 24, Cyan, TextAlignmentOptions.Left);
        dialogSpeaker.fontStyle = FontStyles.Bold;
        Place(dialogSpeaker.rectTransform, new Vector2(95f, 150f), new Vector2(820f, 40f));

        dialogBody = MakeText(box.transform, "Body", "", 27, Color.white, TextAlignmentOptions.TopLeft);
        Place(dialogBody.rectTransform, new Vector2(95f, 10f), new Vector2(820f, 230f));

        Button skipDialogBtn = MakeButton(box.transform, "EĞİTİMİ GEÇ", new Vector2(-140f, -145f), new Vector2(250f, 64f), out TextMeshProUGUI skipDialogLabel);
        skipDialogLabel.color = new Color(1f, 0.82f, 0.45f);
        skipDialogBtn.onClick.AddListener(StartMission);

        Button next = MakeButton(box.transform, "DEVAM >>", new Vector2(370f, -145f), new Vector2(250f, 64f), out dialogButtonLabel);
        next.onClick.AddListener(() => continuePressed = true);

        TextMeshProUGUI hint = MakeText(box.transform, "Hint", "Boşluk / Enter", 18, new Color(0.6f, 0.75f, 0.8f), TextAlignmentOptions.Right);
        Place(hint.rectTransform, new Vector2(120f, -145f), new Vector2(240f, 30f));

        dialogRoot.SetActive(false);
    }

    private static GameObject MakePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static void AddOutline(GameObject go, Color color)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = new Color(color.r, color.g, color.b, 0.9f);
        o.effectDistance = new Vector2(3f, -3f);
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        RuntimeArt.ApplyPixelFont(t);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, out TextMeshProUGUI labelText)
    {
        GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, size);
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.05f, 0.2f, 0.26f, 1f);

        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.6f, 1f, 1f);
        cb.pressedColor = new Color(0.4f, 0.8f, 0.8f);
        cb.fadeDuration = 0.05f;
        b.colors = cb;
        AddOutline(go, Cyan);

        labelText = MakeText(go.transform, "Label", label, 26, Color.white, TextAlignmentOptions.Center);
        labelText.fontStyle = FontStyles.Bold;
        Place(labelText.rectTransform, Vector2.zero, size);
        return b;
    }
}
