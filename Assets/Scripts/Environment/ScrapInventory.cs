using System;

// Toplanan hurda miktarı (şimdilik sadece sayaç)
public static class ScrapInventory
{
    public static int Count { get; private set; }
    public static event Action OnChanged;

    public static void Add(int amount)
    {
        Count += amount;
        OnChanged?.Invoke();
    }

    public static bool TrySpend(int amount)
    {
        if (Count < amount) return false;
        Count -= amount;
        OnChanged?.Invoke();
        return true;
    }

    // Sahne yeniden yüklenince sıfırlamak için
    public static void Reset()
    {
        Count = 0;
        OnChanged?.Invoke();
    }
}
