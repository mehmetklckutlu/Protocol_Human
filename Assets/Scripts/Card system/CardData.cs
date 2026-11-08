using UnityEngine;

public enum CardType { Attack, Defense, Summon, Utility }

[CreateAssetMenu(fileName = "YeniKart", menuName = "Kartlar/Kart")]
public class CardData : ScriptableObject
{
    /// <summary>Kritik vuruşta hasar bu kadarla çarpılır.</summary>
    public const float CritMultiplier = 1.5f;

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
    [Tooltip("Her vuruşta bu aralıktan rastgele hasar atılır.")]
    public IntRange damage = new IntRange(18, 26);
    [Tooltip("Kaç kare uzaktaki hedefe vurulabilir (1 = yakın dövüş).")]
    [Min(1)] public int range = 1;
    [Tooltip("İsabet şansı (1 = her zaman, 0.5 = %50).")]
    [Range(0f, 1f)] public float hitChance = 1f;
    [Tooltip("Kritik vuruş şansı (0.1 = %10). Kritik hasar x1.5.")]
    [Range(0f, 1f)] public float critChance = 0.1f;
    [Tooltip("Vurunca hedefi stunlama şansı (0.7 = %70).")]
    [Range(0f, 1f)] public float stunChance = 0f;
    [Tooltip("Stun kaç tur sürer (hedef o kadar turunu kaçırır).")]
    [Min(1)] public int stunTurns = 1;

    [Header("Savunma Kartı")]
    [Tooltip("Oynanınca bu aralıktan rastgele kalkan kazanılır.")]
    public IntRange shield = new IntRange(12, 18);
    [Tooltip("0 değilse: kartı oynayan birim bu kadar tur düşmanların hedefi olur (taunt).")]
    [Min(0)] public int tauntTurns = 0;

    [Header("Özel Kart (Utility)")]
    [Tooltip("Kartı oynayan birimin canını bu aralıktan rastgele yeniler (en fazla Max HP'ye kadar).")]
    public IntRange heal = 0;
    [Tooltip("Ana drone'un CPU'sunu bu kadar yeniler.")]
    [Min(0)] public int cpuRestore = 0;
    [Tooltip("Tüm dost drone'ların şarjını bu kadar tur uzatır.")]
    [Min(0)] public int chargeAll = 0;
    [Tooltip("Açık: kartı oynayan birim kendini patlatır. Ana drone bu kartı oynayamaz.")]
    public bool selfDestruct = false;
    [Min(0)] public int blastRadius = 2;
    [Tooltip("Patlamada alandaki her birime bu aralıktan ayrı ayrı hasar atılır.")]
    public IntRange blastDamage = new IntRange(35, 50);

    [Header("Blueprint (Summon)")]
    [Tooltip("Çağrılacak dost drone prefab'ı.")]
    public GameObject summonPrefab;

    private void OnValidate()
    {
        damage = new IntRange(Mathf.Max(0, damage.min), damage.max);
        shield = new IntRange(Mathf.Max(0, shield.min), shield.max);
        blastDamage = new IntRange(Mathf.Max(0, blastDamage.min), blastDamage.max);
        heal = new IntRange(Mathf.Max(0, heal.min), heal.max);
    }

    /// <summary>
    /// Saldırı hasarı atar. Kritik gelirse hasar x1.5 olur.
    /// roll01: atılan değerin aralıktaki yeri (0 = en düşük, 1 = en yüksek), hasar yazısının boyutu için.
    /// </summary>
    public int RollDamage(out bool crit, out float roll01)
    {
        int dmg = damage.Roll();
        roll01 = damage.Normalize(dmg);
        crit = critChance > 0f && Random.value < critChance;
        if (crit) dmg = Mathf.RoundToInt(dmg * CritMultiplier);
        return dmg;
    }

    public int RollShield() => shield.Roll();

    /// <summary>Kartın ortasında büyük yazan değer ("18-26" gibi). Boşsa gösterilmez.</summary>
    public string MainValueText
    {
        get
        {
            switch (cardType)
            {
                case CardType.Attack: return damage.IsZero ? "" : damage.ToString();
                case CardType.Defense: return shield.IsZero ? "" : shield.ToString();
                case CardType.Utility:
                    if (selfDestruct) return blastDamage.ToString();
                    if (!heal.IsZero) return heal.ToString();
                    if (cpuRestore > 0) return "+" + cpuRestore;
                    if (chargeAll > 0) return "+" + chargeAll;
                    return "";
                default: return "";
            }
        }
    }

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(description)) return description;

        string text = "";
        switch (cardType)
        {
            case CardType.Attack:
                text = $"{damage} hasar, menzil {range}";
                if (hitChance < 1f) text += $"\n%{Mathf.RoundToInt(hitChance * 100)} isabet";
                if (stunChance > 0f) text += $"\n%{Mathf.RoundToInt(stunChance * 100)} stun";
                if (critChance > 0f) text += $"\n%{Mathf.RoundToInt(critChance * 100)} kritik";
                break;
            case CardType.Defense:
                text = $"{shield} kalkan";
                if (tauntTurns > 0) text += $"\nTaunt {tauntTurns} tur";
                break;
            case CardType.Utility:
                if (selfDestruct) text = $"Kendini patlat\nAlan {blastRadius}, {blastDamage} hasar";
                else if (!heal.IsZero) text = $"{heal} can tamir eder";
                else if (cpuRestore > 0) text = $"+{cpuRestore} CPU";
                else if (chargeAll > 0) text = $"Dronelara +{chargeAll} şarj";
                break;
            case CardType.Summon:
                text = "Drone çağırır";
                break;
        }
        return text;
    }
}
