using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool allowDiagonalMovement = false;

    public Vector2Int CurrentGridPosition { get; private set; }
    private bool isMoving = false;

    private void Start()
    {
        CurrentGridPosition = GridManager.Instance.GetGridPosition(transform.position);
        transform.position = GridManager.Instance.GetWorldPosition(CurrentGridPosition);
    }

    private void Update()
    {
        if (isMoving) return;

        // Sol tık ile hedef seçimi
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int targetGridPos = GridManager.Instance.GetGridPosition(mouseWorldPos);

            List<Node> path = AStarPathfinding.FindPath(CurrentGridPosition, targetGridPos, allowDiagonalMovement);

            if (path != null && path.Count > 0)
            {
                StartCoroutine(MoveAlongPath(path));
            }
        }
    }

    private IEnumerator MoveAlongPath(List<Node> path)
    {
        isMoving = true;

        foreach (Node node in path)
        {
            Vector3 targetWorldPos = node.worldPosition;

            // DÜZELTME 1: Sadece X ve Y mesafesine bak (Z eksenini görmezden gel)
            while (Vector2.Distance(new Vector2(transform.position.x, transform.position.y), new Vector2(targetWorldPos.x, targetWorldPos.y)) > 0.01f)
            {
                float step = moveSpeed * Time.deltaTime;
                Vector3 newPos = Vector3.MoveTowards(transform.position, targetWorldPos, step);

                // Z derinliğini ayarla
                newPos.z = newPos.y * 0.01f;
                transform.position = newPos;

                yield return null;
            }

            // DÜZELTME 2: Döngü bitince karakteri tam oturturken Z'sini de koru
            targetWorldPos.z = targetWorldPos.y * 0.01f;
            transform.position = targetWorldPos;
            CurrentGridPosition = node.gridPosition;
        }

        isMoving = false; // Artık karakter tekrar yürümeye hazır!
    }
}