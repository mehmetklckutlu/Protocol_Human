using UnityEngine;

// Attack  : hedef seçilen saldırı kartı (hasar, menzil, isabet şansı, stun)
// Defense : anında kullanılan kart (kalkan, taunt)
// Summon  : blueprint; savaşta drone çağırır
// Utility : anında kullanılan diğer kartlar (CPU yenileme, şarj, kendini patlatma)
public enum CardType { Attack, Defense, Summon, Utility }

// Project penceresinde: sağ tık > Create > Kartlar > Kart  ile yeni kart oluşturulur
[CreateAssetMenu(fileName = "YeniKart", menuName = "Kartlar/Kart")]
public class CardData : ScriptableObject
{
    [Header("Genel")]
    public string cardName = "Yeni Kart";
    public CardType cardType = CardType.Attack;
    public Sprite icon;
    [TextArea] public string description;
    [Tooltip("Kartı oynayan birimden harcanan CPU (dost drone'lar CPU kullanmaz, onlar için yok sayılır).")]
    [Min(0)] public int cpuCost = 1;
    [Tooltip("Her kullanımda harcanan hurda. 0 = bedelsiz.")]
    [Min(0)] public int scrapCost = 0;

    [Header("Saldırı Kartı")]
    [Min(0)] public int damage = 20;
    [Tooltip("Kaç kare uzaktaki hedefe vurulabilir (1 = yakın dövüş).")]
    [Min(1)] public int range = 1;
    [Tooltip("İsabet şansı (1 = her zaman, 0.5 = %50).")]
    [Range(0f, 1f)] public float hitChance = 1f;
    [Tooltip("Vurunca hedefi stunlama şansı (0.7 = %70).")]
    [Range(0f, 1f)] public float stunChance = 0f;
    [Tooltip("Stun kaç tur sürer (hedef o kadar turunu kaçırır).")]
    [Min(1)] public int stunTurns = 1;

    [Header("Savunma Kartı")]
    [Min(0)] public int shield = 15;
    [Tooltip("0 değilse: kartı oynayan birim bu kadar tur düşmanların hedefi olur (taunt).")]
    [Min(0)] public int tauntTurns = 0;

    [Header("Özel Kart (Utility)")]
    [Tooltip("Ana drone'un CPU'sunu bu kadar yeniler.")]
    [Min(0)] public int cpuRestore = 0;
    [Tooltip("Tüm dost drone'ların şarjını bu kadar tur uzatır.")]
    [Min(0)] public int chargeAll = 0;
    [Tooltip("Açık: kartı oynayan birim kendini patlatır. Ana drone ise oyun kaybedilir.")]
    public bool selfDestruct = false;
    [Min(0)] public int blastRadius = 2;
    [Min(0)] public int blastDamage = 50;

    [Header("Blueprint (Summon)")]
    [Tooltip("Çağrılacak dost drone prefab'ı.")]
    public GameObject summonPrefab;

    // Kartın ana sayısı (UI'da büyük yazılır)
    public int MainValue
    {
        get
        {
            switch (cardType)
            {
                case CardType.Attack: return damage;
                case CardType.Defense: return shield;
                case CardType.Utility:
                    if (selfDestruct) return blastDamage;
                    if (cpuRestore > 0) return cpuRestore;
                    return chargeAll;
                default: return 0;
            }
        }
    }

    // Açıklama boşsa otomatik üretilir
    public string GetDescription()
    {
        string text;
        if (!string.IsNullOrEmpty(description)) text = description;
        else
        {
            text = "";
            switch (cardType)
            {
                case CardType.Attack:
                    text = $"{damage} hasar, menzil {range}";
                    if (hitChance < 1f) text += $"\n%{Mathf.RoundToInt(hitChance * 100)} isabet";
                    if (stunChance > 0f) text += $"\n%{Mathf.RoundToInt(stunChance * 100)} stun";
                    break;
                case CardType.Defense:
                    text = $"{shield} kalkan";
                    if (tauntTurns > 0) text += $"\nTaunt {tauntTurns} tur";
                    break;
                case CardType.Utility:
                    if (selfDestruct) text = $"Kendini patlat\nAlan {blastRadius}, {blastDamage} hasar";
                    else if (cpuRestore > 0) text = $"+{cpuRestore} CPU";
                    else if (chargeAll > 0) text = $"Dronelara +{chargeAll} şarj";
                    break;
                case CardType.Summon:
                    text = "Drone çağırır";
                    break;
            }
        }
        if (scrapCost > 0) text += $"\nHurda: {scrapCost}";
        return text;
    }
}
