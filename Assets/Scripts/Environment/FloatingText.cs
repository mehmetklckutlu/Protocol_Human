using UnityEngine;
using TMPro;

// Dünyada yukarı süzülüp kaybolan yazı (hasar, ıska, stun, loot bildirimi)
public class FloatingText : MonoBehaviour
{
    private TextMeshPro tmp;
    private float life;
    private const float Duration = 1.1f;

    public static void Spawn(Vector3 worldPos, string text, Color color, int line = 0)
    {
        // Sisteki (görünmeyen) karelerde yazı gösterme
        if (FogOfWar.Instance != null && GridManager.Instance != null &&
            !FogOfWar.Instance.IsVisible(GridManager.Instance.GetGridPosition(worldPos)))
            return;

        GameObject go = new GameObject("FloatingText");
        go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.6f + line * 0.4f, -1f);

        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = 3.5f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.sortingOrder = 300;
        t.textWrappingMode = TextWrappingModes.NoWrap;

        FloatingText ft = go.AddComponent<FloatingText>();
        ft.tmp = t;
    }

    private void Update()
    {
        life += Time.unscaledDeltaTime;
        transform.position += Vector3.up * (0.6f * Time.unscaledDeltaTime);
        Color c = tmp.color;
        c.a = Mathf.Clamp01(1f - (life / Duration) * (life / Duration));
        tmp.color = c;
        if (life >= Duration) Destroy(gameObject);
    }
}
