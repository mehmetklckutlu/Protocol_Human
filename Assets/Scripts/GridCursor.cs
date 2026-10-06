using UnityEngine;

public class GridCursor : MonoBehaviour
{
    private SpriteRenderer cursorSpriteRenderer;

    private void Awake()
    {
        // Kendi üzerindeki SpriteRenderer'ı otomatik bulur (sürüklemene gerek kalmaz)
        cursorSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (GridManager.Instance == null) return;

        // Z derinliği tıklamayı bozmasın diye farenin pozisyonunu sabitliyoruz
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(Camera.main.transform.position.z);
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Vector2Int gridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

        if (GridManager.Instance.IsInsideGrid(gridPos))
        {
            cursorSpriteRenderer.enabled = true;

            // İmleci tam karenin merkezine oturt
            Vector3 worldPos = GridManager.Instance.GetWorldPosition(gridPos);
            worldPos.z = -1f; // Haritanın ve karakterin biraz üstünde görünsün
            transform.position = worldPos;
        }
        else
        {
            cursorSpriteRenderer.enabled = false;
        }
    }
}