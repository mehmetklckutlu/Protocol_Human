using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Vurulan birimin üstünde çıkan hasar sayısı: patlayarak büyür, hafif kavisle yükselir, yavaşça kaybolur.
/// Normal hasar kırmızı, kritik sarı ve büyük, kalkanın emdiği kısım mavi.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    private enum Kind { Damage, Crit, Blocked, ShieldGain, Heal }

    private static readonly Color DamageColor = new Color(1f, 0.32f, 0.27f);
    private static readonly Color HeavyColor = new Color(1f, 0.15f, 0.12f);
    private static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color BlockColor = new Color(0.35f, 0.8f, 1f);
    private static readonly Color ShieldColor = new Color(0.25f, 0.78f, 1f);
    private static readonly Color HealColor = new Color(0.4f, 1f, 0.5f);

    // Aynı birime arka arkaya gelen sayılar üst üste binmesin diye kısa süreli yığın takibi.
    private static readonly List<(Vector3 pos, float time, float height)> recent = new List<(Vector3, float, float)>();
    private const float StackWindow = 0.45f;

    private TextMeshPro tmp;
    private Material mat;
    private Color baseColor;
    private Vector3 velocity;
    private float life;
    private float duration;
    private float peakScale;
    private Vector3 baseScale;

    public static void Spawn(Vector3 worldPos, int amount, bool crit, float roll01 = 0.5f)
    {
        if (crit) Create(worldPos, $"<size=55%>KRITIK!</size>\n{amount}", Kind.Crit, 1f);
        else Create(worldPos, amount.ToString(), Kind.Damage, Mathf.Clamp01(roll01));
    }

    /// <summary>Kalkanın emdiği hasar. healthAlsoHit = kalkan delindi, can da gitti.</summary>
    public static void SpawnBlocked(Vector3 worldPos, int absorbed, bool healthAlsoHit)
    {
        string text = healthAlsoHit ? $"-{absorbed}" : $"<size=60%>ENGEL</size>\n-{absorbed}";
        Create(worldPos, text, Kind.Blocked, 0.3f);
    }

    public static void SpawnShieldGain(Vector3 worldPos, int amount)
    {
        Create(worldPos, $"+{amount} <size=65%>KALKAN</size>", Kind.ShieldGain, 0.4f);
    }

    public static void SpawnHeal(Vector3 worldPos, int amount)
    {
        Create(worldPos, amount > 0 ? $"+{amount} <size=65%>CAN</size>" : "<size=70%>CAN DOLU</size>", Kind.Heal, 0.4f);
    }

    private static void Create(Vector3 worldPos, string text, Kind kind, float intensity)
    {
        if (FogOfWar.Instance != null && GridManager.Instance != null &&
            !FogOfWar.Instance.IsVisible(GridManager.Instance.GetGridPosition(worldPos)))
            return;

        float stackOffset = NextStackOffset(worldPos, HeightOf(kind, text));
        float jitterX = Random.Range(-0.12f, 0.12f);

        GameObject go = new GameObject("DamageNumber");
        go.transform.position = new Vector3(worldPos.x + jitterX, worldPos.y + 0.65f + stackOffset, -1.1f);

        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.sortingOrder = kind == Kind.Crit ? 320 : 310;
        t.lineSpacing = -20f;

        // Kenar çizgisi: her yazıya kendi materyal kopyası (OnDestroy'da silinir).
        // Saydamlık köşe renginden geldiği için kenar da yazıyla birlikte solar.
        Material mat = t.fontMaterial;
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, kind == Kind.Crit ? 0.28f : 0.24f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(18, 8, 8, 255));
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
        t.UpdateMeshPadding();

        DamageNumber dn = go.AddComponent<DamageNumber>();
        dn.tmp = t;
        dn.mat = mat;

        switch (kind)
        {
            case Kind.Crit:
                t.fontSize = 6.2f;
                dn.baseColor = CritColor;
                dn.peakScale = 1.8f;
                dn.duration = 1.45f;
                dn.velocity = new Vector3(jitterX * 2f, 2.6f, 0f);
                break;
            case Kind.Damage:
                t.fontSize = Mathf.Lerp(3.8f, 5f, intensity);
                dn.baseColor = Color.Lerp(DamageColor, HeavyColor, intensity);
                dn.peakScale = Mathf.Lerp(1.35f, 1.6f, intensity);
                dn.duration = 1.15f;
                dn.velocity = new Vector3(jitterX * 2.5f, 2.1f, 0f);
                break;
            case Kind.Blocked:
                t.fontSize = 3.4f;
                dn.baseColor = BlockColor;
                dn.peakScale = 1.25f;
                dn.duration = 1.0f;
                dn.velocity = new Vector3(-0.4f, 1.7f, 0f);
                break;
            case Kind.Heal:
                t.fontSize = 3.8f;
                dn.baseColor = HealColor;
                dn.peakScale = 1.3f;
                dn.duration = 1.2f;
                dn.velocity = new Vector3(0f, 1.5f, 0f);
                break;
            default:
                t.fontSize = 3.4f;
                dn.baseColor = ShieldColor;
                dn.peakScale = 1.2f;
                dn.duration = 1.1f;
                dn.velocity = new Vector3(0f, 1.4f, 0f);
                break;
        }

        t.color = dn.baseColor;
        dn.baseScale = Vector3.one;
        go.transform.localScale = Vector3.zero;
    }

    /// <summary>Aynı noktada kısa süre içinde çıkan yazıları üst üste değil, alt alta dizer.</summary>
    private static float NextStackOffset(Vector3 pos, float height)
    {
        float now = Time.unscaledTime;
        recent.RemoveAll(r => now - r.time > StackWindow);
        float offset = 0f;
        foreach (var r in recent)
            if (Vector2.Distance(r.pos, pos) < 0.8f) offset += r.height;
        recent.Add((pos, now, height));
        return offset;
    }

    private static float HeightOf(Kind kind, string text) =>
        kind == Kind.Crit ? 1.2f : text.Contains("\n") ? 0.8f : 0.55f;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        life += dt;
        float t = life / duration;

        // 1) Pop: 0 -> peak -> 1 (ilk %18'lik kısımda)
        const float popEnd = 0.18f;
        float s;
        if (t < popEnd * 0.5f) s = Mathf.Lerp(0f, peakScale, EaseOut(t / (popEnd * 0.5f)));
        else if (t < popEnd) s = Mathf.Lerp(peakScale, 1f, EaseOut((t - popEnd * 0.5f) / (popEnd * 0.5f)));
        else s = 1f;
        transform.localScale = baseScale * s;

        // 2) Hareket: hızlı çıkar, yavaşlar (sürtünme), hafif yana kayar.
        transform.position += velocity * dt;
        velocity *= Mathf.Exp(-4.2f * dt);

        // 3) Solma: son %45'te yavaşça kaybolur.
        float fade = t < 0.55f ? 1f : 1f - EaseIn((t - 0.55f) / 0.45f);
        Color c = baseColor;
        c.a = Mathf.Clamp01(fade);
        tmp.color = c;

        if (life >= duration) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x); }
    private static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x; }
}
