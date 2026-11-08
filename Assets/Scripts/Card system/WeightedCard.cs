using System;
using UnityEngine;

/// <summary>Rastgele kart havuzundaki bir kart ve gelme ağırlığı (büyük = daha sık).</summary>
[Serializable]
public class WeightedCard
{
    public CardData card;
    [Min(0)] public int weight = 10;
}
