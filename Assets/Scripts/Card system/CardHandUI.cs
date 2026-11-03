using System.Collections.Generic;
using UnityEngine;

public class CardHandUI : MonoBehaviour
{
    [SerializeField] private GameObject handRoot;
    [SerializeField] private Transform container;
    [SerializeField] private CardView cardPrefab;

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
        if (!inCombat) return;

        foreach (Transform child in container) Destroy(child.gameObject);

        foreach (CardData card in cm.CurrentHand)
        {
            if (card == null) continue;
            CardView view = Instantiate(cardPrefab, container);
            view.Setup(card, cm.CanPlay(card), cm.PendingCard == card);
        }
    }
}
