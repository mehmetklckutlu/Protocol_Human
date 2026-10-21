using System.Collections.Generic;
using UnityEngine;

// Eldeki kartları ekranın altında gösterir.
// DİKKAT: Bu script hep aktif bir objede (ör. Canvas) durmalı; savaşta açılıp kapanacak panel "Hand Root" olmalı.
public class CardHandUI : MonoBehaviour
{
    [SerializeField] private GameObject handRoot;   // Sadece savaşta görünen panel
    [SerializeField] private Transform container;   // Kartların dizildiği Horizontal Layout Group
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
