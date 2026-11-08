using System;
using UnityEngine;

/// <summary>
/// Min-max arası (ikisi de dahil) rastgele tam sayı. Hasar, kalkan, hurda gibi tüm değerler bunu kullanır.
/// Inspector'da tek satırda "Min – Max" olarak görünür.
/// </summary>
[Serializable]
public struct IntRange
{
    public int min;
    public int max;

    public IntRange(int min, int max)
    {
        this.min = min;
        this.max = Mathf.Max(min, max);
    }

    /// <summary>Sabit değer: min = max.</summary>
    public static implicit operator IntRange(int value) => new IntRange(value, value);

    public bool IsZero => min <= 0 && max <= 0;
    public bool IsFixed => min == max;
    public float Average => (min + Mathf.Max(min, max)) * 0.5f;

    public int Roll() => UnityEngine.Random.Range(min, Mathf.Max(min, max) + 1);

    /// <summary>Atılan değerin aralıktaki yeri (0 = en düşük, 1 = en yüksek).</summary>
    public float Normalize(int value) => IsFixed ? 1f : Mathf.InverseLerp(min, max, value);

    public override string ToString() => IsFixed ? min.ToString() : $"{min}-{max}";
}
