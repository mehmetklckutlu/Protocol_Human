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
    public string nextScene = "MainLevel";

    [Header("Yazı")]
    [SerializeField, Min(0f)] private float charDelay = 0.012f;
    [Tooltip("Test Kontrol Ünitesi'nin konuşma sesinin perdesi (1 = normal, düşük = daha kalın).")]
    [SerializeField, Range(0.5f, 2f)] private float voicePitch = 1f;

    private const string Speaker = "TEST KONTROL ÜNİTESİ";
    private static readonly Color Cyan = new Color(0.35f, 0.95f, 1f);
    private static readonly Color Green = new Color(0.45f, 1f, 0.55f);

    private Canvas canvas;
    private SubtitleBox subtitle;
    private GameObject objectiveRoot;
    private TextMeshProUGUI objectiveTitle;
    private TextMeshProUGUI objectiveText;

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
                if (node != null) { node.isWalkable = true; node.isWall = false; }
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
    }

    // ------------------------------------------------------------------ diyalog

    // Konuşma ekranın altında alt yazı olarak akar; drone bu sırada hareket edebilir.
    // Ekrana tıklamak (ya da Boşluk/Enter) yazıyı tamamlar / sonraki sayfaya geçer.
    private IEnumerator Dialog(params string[] pages)
    {
        if (CardManager.Instance != null) CardManager.Instance.CancelTargeting();
        yield return subtitle.Play(Speaker, Cyan, pages, true, 0f, charDelay, voicePitch, page =>
        {
            if (page.Contains("DOĞRULANDI")) Sfx.Play("test_passed", 0.7f, 0f, 0.3f);
        });
        yield return null;
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

        // Arka planı karartan katman (tıklamayı da tutar)
        GameObject root = new GameObject("TestComplete", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        HudUI.Stretch((RectTransform)root.transform);
        root.GetComponent<Image>().color = new Color(0f, 0.02f, 0.04f, 0.85f);

        // HUD temalı pencere
        Image frame = HudUI.Panel(root.transform, "Frame", new Color32(40, 150, 80, 255));
        frame.raycastTarget = true;
        HudUI.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 560f));
        HudUI.Accent(frame.transform, (Color)HudArt.Green, 120f);

        TMP_Text title = HudUI.Text(frame.transform, "Title", "TEST PROTOKOLÜ TAMAMLANDI", 52, (Color)HudArt.Green,
                                    TextAlignmentOptions.Center, 30f);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        HudUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 70f));

        Image line = HudUI.Panel(frame.transform, "Line", new Color32(40, 150, 80, 255));
        line.sprite = HudArt.Solid;
        line.type = Image.Type.Simple;
        line.color = new Color(HudArt.Green.r / 255f, HudArt.Green.g / 255f, HudArt.Green.b / 255f, 0.45f);
        HudUI.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(880f, 3f));

        TMP_Text body = HudUI.Text(frame.transform, "Body",
            "PH-01 tüm test aşamalarını başarıyla geçti.\n\n" +
            "<color=#78A0AA>Hareket · Hurda Toplama · Savaş · Savunma · Çevre Kullanımı · Destek Drone</color>\n" +
            "<color=#7CFF8A>DOĞRULANDI</color>\n\n" +
            "Durum: <b><color=#7CFF8A>GÖREVE HAZIR</color></b>\n\n" +
            "Görev: İnsanlığa ait verileri bul ve kurtar.\n" +
            "<i>İyi şanslar, PH-01. İnsanlığın hafızası artık sana emanet.</i>",
            26, (Color)HudArt.White, TextAlignmentOptions.Center, 16f);
        HudUI.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(880f, 280f));

        Button b = HudUI.MakeButton(frame.transform, "StartMission", "GÖREVE BAŞLA", 30,
            HudArt.Green, new Color32(14, 60, 34, 255), (Color)HudArt.White, StartMission, out _);
        HudUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(420f, 76f));
    }

    private void StartMission()
    {
        Sfx.Play("ui_click", 0.6f);
        ScrapInventory.Reset();
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else if (Application.CanStreamedLevelBeLoaded("MainLevel"))
        {
            Debug.LogWarning("Tutorial: '" + nextScene + "' sahnesi Build Settings'te yok, MainLevel açılıyor.");
            SceneManager.LoadScene("MainLevel");
        }
        else
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(next);
            else Debug.LogWarning("Tutorial: '" + nextScene + "' sahnesi Build Settings'te yok.");
        }
    }

    // ------------------------------------------------------------------ arayüz

    private void BuildUI()
    {
        HudUI.EnsureEventSystem();
        canvas = HudUI.MakeCanvas("TutorialCanvas", 450);
        Transform ct = canvas.transform;

        // Görev kutusu (sağ üst, HUD temasında): başlık + hedef, ikisi de kutunun içinde hizalı
        Image obj = HudUI.Panel(ct, "Objective", HudArt.CyanMid);
        HudUI.Place(obj.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(480f, 96f));
        HudUI.Accent(obj.transform, (Color)HudArt.Cyan, 70f);
        objectiveRoot = obj.gameObject;

        objectiveTitle = (TextMeshProUGUI)HudUI.Text(obj.transform, "Title", "", 18, (Color)HudArt.Cyan,
                                                     TextAlignmentOptions.MidlineLeft, 12f);
        objectiveTitle.fontStyle = FontStyles.Bold;
        objectiveTitle.textWrappingMode = TextWrappingModes.NoWrap;
        HudUI.Stretch(objectiveTitle.rectTransform, 22f, 18f, 12f, 58f);

        objectiveText = (TextMeshProUGUI)HudUI.Text(obj.transform, "Text", "", 22, (Color)HudArt.White,
                                                    TextAlignmentOptions.MidlineLeft, 14f);
        HudUI.Stretch(objectiveText.rectTransform, 22f, 18f, 40f, 12f);
        objectiveRoot.SetActive(false);

        // Görev kutusunun altında her zaman erişilebilir "Eğitimi Geç" butonu
        Button skip = HudUI.MakeButton(ct, "SkipTutorial", "EĞİTİMİ GEÇ >>", 20,
            new Color32(150, 92, 18, 255), new Color32(40, 26, 8, 235), (Color)HudArt.Amber, StartMission, out _);
        HudUI.Place((RectTransform)skip.transform, new Vector2(1f, 1f), new Vector2(-24f, -126f), new Vector2(220f, 50f));

        // Diyalog: ekranın altında alt yazı kutusu (HUD temasında). Tıklamaları engellemez.
        subtitle = SubtitleBox.Create(ct);
    }
}
