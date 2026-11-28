using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardHandUI : MonoBehaviour
{
    [SerializeField] private GameObject handRoot;
    [SerializeField] private Transform container;
    [SerializeField] private CardView cardPrefab;

    [Header("Yerleşim")]
    [Tooltip("Elin ekranın iki yanında boş bırakacağı alan (CPU yazısı ve Turu Bitir butonu için).")]
    [SerializeField] private float sideMargin = 270f;
    [Tooltip("Kartların eldeki büyüklüğü (1 = prefab boyutu).")]
    [SerializeField, Range(0.8f, 1.8f)] private float cardScale = 1.3f;
    [Tooltip("Kart merkezleri arası mesafe, kart genişliğine oranla. 1'den küçükse kartlar iç içe geçer.")]
    [SerializeField, Range(0.5f, 1.1f)] private float overlap = 0.78f;
    [Tooltip("Kartların ekranın altından ne kadar yukarıda duracağı (büyütülmüş kartların altı ekrandan taşmasın).")]
    [SerializeField] private float bottomLift = 44f;

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

    /// <summary>
    /// Kartları elle dizer: büyütülmüş kartlar yan yana iç içe geçer (Slay the Spire gibi), el her zaman
    /// ekrana sığar (9 kart dahil). Kavis/eğim ve hover hareketleri CardView'da.
    /// </summary>
    private void LayoutCards(List<CardView> views)
    {
        var rt = container as RectTransform;
        int count = views.Count;
        if (rt == null || count <= 0) return;

        // Yerleşimi biz yapıyoruz; layout grubu kartları düz sıraya geri çekmesin.
        var layout = container.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;

        var canvasRt = rt.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        float available = Mathf.Max(400f, canvasRt.rect.width - sideMargin * 2f);
        float cardW = ((RectTransform)cardPrefab.transform).rect.width * cardScale;

        float step = cardW * overlap;
        if (count > 1 && cardW + (count - 1) * step > available)
            step = (available - cardW) / (count - 1);

        rt.sizeDelta = new Vector2(Mathf.Min(available, cardW + (count - 1) * step), rt.sizeDelta.y);

        for (int i = 0; i < count; i++)
        {
            RectTransform crt = (RectTransform)views[i].transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * step, bottomLift);
            crt.localScale = Vector3.one * cardScale;
        }
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
        List<int> handIndices = new List<int>();
        List<CardData> hand = cm.CurrentHand;
        for (int h = 0; h < hand.Count; h++)
        {
            if (hand[h] == null) continue;
            validCards.Add(hand[h]);
            handIndices.Add(h);
        }

        bool playIntro = !wasInCombat || validCards.Count > lastCount;
        wasInCombat = true;
        lastCount = validCards.Count;

        var views = new List<CardView>(validCards.Count);
        for (int i = 0; i < validCards.Count; i++)
        {
            CardData card = validCards[i];
            CardView view = Instantiate(cardPrefab, container);
            view.SetHandSlot(i, validCards.Count, playIntro);
            int handIndex = handIndices[i];
            // Sadece tıklanan kopya seçili görünsün; aynı türden diğer kart yerinde kalsın.
            bool selected = cm.PendingCard == card && cm.PendingIndex == handIndex;
            view.Setup(card, cm.CanPlay(card), selected, handIndex);
            views.Add(view);
        }
        LayoutCards(views);
    }
}
