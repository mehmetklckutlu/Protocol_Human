using UnityEngine;

public enum CardType { Attack, Defense }

// Project penceresinde: sağ tık > Create > Kartlar > Kart  ile yeni kart oluşturulur
[CreateAssetMenu(fileName = "YeniKart", menuName = "Kartlar/Kart")]
public class CardData : ScriptableObject
{
    [Header("Genel")]
    public string cardName = "Yeni Kart";
    public CardType cardType = CardType.Attack;
    public Sprite icon;
    [TextArea] public string description;
    [Min(0)] public int cpuCost = 1;

    [Header("Saldırı Kartı Ayarları")]
    [Tooltip("Sadece Attack kartlarında kullanılır: verilen hasar.")]
    [Min(0)] public int damage = 20;
    [Tooltip("Sadece Attack kartlarında kullanılır: kaç kare uzaktaki düşmana vurulabilir (1 = yakın dövüş).")]
    [Min(1)] public int range = 1;

    [Header("Savunma Kartı Ayarları")]
    [Tooltip("Sadece Defense kartlarında kullanılır: verilen kalkan miktarı.")]
    [Min(0)] public int shield = 15;

    // Kartın ana sayısı (UI'da büyük yazılır)
    public int MainValue => cardType == CardType.Attack ? damage : shield;

    // Açıklama boşsa otomatik üretilir
    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(description)) return description;
        return cardType == CardType.Attack
            ? $"{damage} hasar\nMenzil: {range}"
            : $"{shield} kalkan";
    }
}
