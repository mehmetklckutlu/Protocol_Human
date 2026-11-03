using System.Collections.Generic;
using UnityEngine;

public class CardHandUI : MonoBehaviour
{
    [SerializeField] private GameObject handRoot;
    [SerializeField] private Transform container;
    [SerializeField] private CardView cardPrefab;

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

        foreach (Transform child in container) Destroy(child.gameObject);

        List<CardData> validCards = new List<CardData>();
        foreach (CardData card in cm.CurrentHand)
            if (card != null) validCards.Add(card);

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
