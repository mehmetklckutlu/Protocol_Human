using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardHandUI : MonoBehaviour
{
    [SerializeField] private GameObject handRoot;
    [SerializeField] private Transform container;
    [SerializeField] private CardView cardPrefab;

    [Header("Yerleşim")]
    [Tooltip("Kartlar arası normal boşluk. Kartlar sığmazsa bu küçülür, gerekirse kartlar üst üste biner.")]
    [SerializeField] private float preferredSpacing = 12f;
    [Tooltip("Elin ekranın iki yanında boş bırakacağı alan (CPU yazısı ve Turu Bitir butonu için).")]
    [SerializeField] private float sideMargin = 270f;

    private int lastCount = -1;
    private bool wasInCombat = false;

    private void Start()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnHandChanged += Rebuild;
            Rebuild();
        }
    }

    private void OnDestroy()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnHandChanged -= Rebuild;
    }

    /// <summary>Kart sayısına göre aralığı ayarlar: el her zaman ekrana sığar (9 kart dahil).</summary>
    private void FitToWidth(int count)
    {
        var layout = container.GetComponent<HorizontalLayoutGroup>();
        var rt = container as RectTransform;
        if (layout == null || rt == null || count <= 0) return;

        var canvasRt = rt.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        float available = Mathf.Max(400f, canvasRt.rect.width - sideMargin * 2f);
        float cardW = ((RectTransform)cardPrefab.transform).rect.width;

        float spacing = preferredSpacing;
        if (count > 1 && count * cardW + (count - 1) * spacing > available)
            spacing = (available - count * cardW) / (count - 1);

        layout.spacing = spacing;
        rt.sizeDelta = new Vector2(Mathf.Min(available, count * cardW + (count - 1) * spacing), rt.sizeDelta.y);
    }

    private void Rebuild()
    {
        CardManager cm = CardManager.Instance;
        if (cm == null || container == null || cardPrefab == null) return;

        bool inCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
        if (handRoot != null) handRoot.SetActive(inCombat);
        if (!inCombat)
        {
            wasInCombat = false;
            lastCount = -1;
            return;
        }

        // Önce ayır, sonra yok et: Destroy kare sonunda çalışır, ayrılmazsa eski kartlar da layout'a girer.
        var old = new List<Transform>();
        foreach (Transform child in container) old.Add(child);
        foreach (Transform child in old) { child.SetParent(null, false); Destroy(child.gameObject); }

        List<CardData> validCards = new List<CardData>();
        foreach (CardData card in cm.CurrentHand)
            if (card != null) validCards.Add(card);

        FitToWidth(validCards.Count);

        bool playIntro = !wasInCombat || validCards.Count > lastCount;
        wasInCombat = true;
        lastCount = validCards.Count;

        for (int i = 0; i < validCards.Count; i++)
        {
            CardData card = validCards[i];
            CardView view = Instantiate(cardPrefab, container);
            view.SetHandSlot(i, validCards.Count, playIntro);
            view.Setup(card, cm.CanPlay(card), cm.PendingCard == card);
        }
    }
}
