using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum DroneType { Melee, Ranged, Boss }

// Drone'lara eklenen ortak bileşen (Unit ile birlikte durur).
// - Dost drone: şarj sayacı ve kendi kart seti (kit)
// - Düşman drone: ölünce düşenler (hurda, kart, blueprint)
[RequireComponent(typeof(Unit))]
public class DroneUnit : MonoBehaviour
{
    [Header("Genel")]
    public DroneType droneType = DroneType.Melee;
    [Tooltip("Açık: bizim drone'umuz. Kapalı: düşman drone.")]
    public bool isAlly = false;

    [Header("Dost Drone")]
    [Tooltip("Kalan şarj (tur). Her tam tur sonunda 1 azalır, 0 olunca drone ölür.")]
    [Min(1)] public int charge = 3;
    [Tooltip("Bu drone'un elindeki kartlar. Her tur hepsi yeniden kullanılabilir, turda 1 tanesi oynanır.")]
    public List<CardData> kit = new List<CardData>();

    [Header("Düşman Drone: Ölünce Düşenler")]
    [Min(0)] public int scrapMin = 2;
    [Min(0)] public int scrapMax = 5;
    [Tooltip("Kart düşme ihtimali (0.35 = %35). Düşen kart aşağıdaki listeden rastgele seçilir.")]
    [Range(0f, 1f)] public float cardDropChance = 0.35f;
    [Tooltip("Düşebilecek kartlar (yerden bulunan, hurda bedelli sürümler).")]
    public List<CardData> dropCards = new List<CardData>();
    [Tooltip("Bu drone'un blueprint kartı.")]
    public CardData blueprint;
    [Tooltip("Blueprint düşme ihtimali (0.25 = %25).")]
    [Range(0f, 1f)] public float blueprintChance = 0.25f;

    private Unit unit;
    private TextMeshPro chargeLabel;

    private void Awake()
    {
        unit = GetComponent<Unit>();
    }

    private void Start()
    {
        unit.OnDeath += HandleDeath;
        if (isAlly) CreateChargeLabel();
    }

    // ---------- Şarj ----------

    public void AddCharge(int amount)
    {
        charge += amount;
        UpdateLabel();
        FloatingText.Spawn(transform.position, "+" + amount + " SARJ", new Color(0.4f, 1f, 1f));
    }

    // Her tam tur sonunda TurnManager çağırır
    public void TickCharge()
    {
        if (!isAlly) return;
        charge--;
        UpdateLabel();
        if (charge <= 0)
        {
            Debug.Log(name + " şarjı bitti, devre dışı kaldı.");
            FloatingText.Spawn(transform.position, "SARJ BITTI", Color.gray);
            unit.Kill();
        }
    }

    private void CreateChargeLabel()
    {
        GameObject go = new GameObject("ChargeLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        chargeLabel = go.AddComponent<TextMeshPro>();
        chargeLabel.fontSize = 3f;
        chargeLabel.fontStyle = FontStyles.Bold;
        chargeLabel.alignment = TextAlignmentOptions.Center;
        chargeLabel.color = new Color(0.4f, 1f, 1f);
        chargeLabel.sortingOrder = 20;
        chargeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (chargeLabel != null) chargeLabel.text = charge.ToString();
    }

    // ---------- Loot ----------

    private void HandleDeath(Unit u)
    {
        if (isAlly) return;

        // Loot otomatik gelmez: ölen dronun yerine düşer, gidip toplanır
        int scrap = Random.Range(scrapMin, Mathf.Max(scrapMin, scrapMax) + 1);

        List<CardData> cards = new List<CardData>();
        if (dropCards.Count > 0 && Random.value < cardDropChance)
        {
            CardData c = dropCards[Random.Range(0, dropCards.Count)];
            if (c != null) cards.Add(c);
        }

        bool bp = false;
        if (blueprint != null && Random.value < blueprintChance)
        {
            cards.Add(blueprint);
            bp = true;
        }

        LootPickup.Spawn(transform.position, scrap, cards, bp ? blueprint : null);
    }
}
