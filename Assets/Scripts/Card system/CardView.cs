using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Tek bir kartın arayüzü (Card prefab'ının üzerine eklenir)
public class CardView : MonoBehaviour
{
    [Header("Bağlantılar")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Renkler")]
    [SerializeField] private Color attackColor = new Color(0.85f, 0.3f, 0.3f);
    [SerializeField] private Color defenseColor = new Color(0.3f, 0.55f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.3f);

    public void Setup(CardData card, bool interactable, bool selected)
    {
        if (nameText != null) nameText.text = card.cardName;
        if (valueText != null) valueText.text = card.MainValue.ToString();
        if (costText != null) costText.text = card.cpuCost.ToString();
        if (descriptionText != null) descriptionText.text = card.GetDescription();

        if (iconImage != null)
        {
            iconImage.sprite = card.icon;
            iconImage.enabled = card.icon != null;
        }

        if (background != null)
        {
            background.color = selected ? selectedColor
                : (card.cardType == CardType.Attack ? attackColor : defenseColor);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => CardManager.Instance.OnCardClicked(card));
            button.interactable = interactable;
        }
    }
}
