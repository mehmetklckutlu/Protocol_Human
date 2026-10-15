using UnityEngine;
using System.Collections.Generic;

public class GridCursor : MonoBehaviour
{
    private SpriteRenderer cursorSpriteRenderer;

    private void Awake()
    {
        cursorSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (GridManager.Instance == null) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(Camera.main.transform.position.z);
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Vector2Int gridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

        if (GridManager.Instance.IsInsideGrid(gridPos))
        {
            cursorSpriteRenderer.enabled = true;

            Vector3 worldPos = GridManager.Instance.GetWorldPosition(gridPos);
            worldPos.z = -1f;
            transform.position = worldPos;
        }
        else
        {
            cursorSpriteRenderer.enabled = false;
        }
    }
}