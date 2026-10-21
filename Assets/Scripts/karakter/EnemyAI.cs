using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyTargetMode { Nearest, LowestHealth }

[RequireComponent(typeof(Unit))]
public class EnemyAI : MonoBehaviour
{
    private Unit myUnit;

    [Header("Yapay Zeka Ayarları")]
    public float moveSpeed = 5f;
    public int attackDamage = 20;
    [Tooltip("Kaç kare uzaktan saldırabilir (1 = yakın dövüş).")]
    [Min(1)] public int attackRange = 1;
    [Tooltip("Nearest: en yakın rakip. LowestHealth: canı en düşük rakip. (Taunt varsa her zaman taunt'layan hedeflenir.)")]
    public EnemyTargetMode targetMode = EnemyTargetMode.Nearest;

    [Header("Görüş (Vision) ve Uyanma")]
    public int visionRange = 15;
    public bool isAwake = false;
    public LayerMask obstacleLayer;

    private void Awake()
    {
        myUnit = GetComponent<Unit>();
    }

    private void Start()
    {
        if (GridManager.Instance != null)
        {
            Vector2Int startGridPos = GridManager.Instance.GetGridPosition(transform.position);
            Vector3 snappedPos = GridManager.Instance.GetWorldPosition(startGridPos);
            snappedPos.z = snappedPos.y * 0.01f;
            transform.position = snappedPos;
        }
    }

    private void Update()
    {
        if (isAwake) return;

        // Sahnede TurnManager veya GridManager yoksa (eksik kurulum) hata vermeden bekle
        if (TurnManager.Instance == null || GridManager.Instance == null) return;

        // Sahnedeki HERHANGİ bir oyuncu birimi menzildeyse ve arada duvar yoksa uyan
        foreach (Unit target in TurnManager.Instance.playerUnits)
        {
            if (target == null) continue;

            Vector2Int currentPos = GridManager.Instance.GetGridPosition(transform.position);
            Vector2Int targetPos = GridManager.Instance.GetGridPosition(target.transform.position);
            int distanceToTarget = Mathf.Abs(currentPos.x - targetPos.x) + Mathf.Abs(currentPos.y - targetPos.y);

            if (distanceToTarget > visionRange) continue;

            RaycastHit2D hit = Physics2D.Linecast(transform.position, target.transform.position, obstacleLayer);
            if (hit.collider != null) continue; // Arada duvar var

            WakeUp();
            return;
        }
    }

    private void WakeUp()
    {
        isAwake = true;
        Debug.Log($"<color=red>{gameObject.name} oyuncuyu FARK ETTİ!</color>");

        TurnManager.Instance.UpdateCombatState();

        // İlk uyanan düşman zaten UpdateCombatState içinde kuyruğa eklenir; sonrakiler burada eklenir
        if (TurnOrderUI.Instance != null)
            TurnOrderUI.Instance.AddUnitToQueue(myUnit);

        StartCoroutine(TurnManager.Instance.ShowEnemyAlert());
    }

    private int Dist(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // Hedef seçimi: taunt > hedef moduna göre
    private Unit PickTarget()
    {
        TurnManager tm = TurnManager.Instance;
        Vector2Int me = GridManager.Instance.GetGridPosition(transform.position);

        Unit best = null;
        int bestDist = int.MaxValue;

        foreach (Unit p in tm.playerUnits)
        {
            if (p == null || p.tauntTurns <= 0) continue;
            int d = Dist(me, GridManager.Instance.GetGridPosition(p.transform.position));
            if (d < bestDist) { bestDist = d; best = p; }
        }
        if (best != null) return best;

        if (targetMode == EnemyTargetMode.LowestHealth)
        {
            int bestHp = int.MaxValue;
            foreach (Unit p in tm.playerUnits)
            {
                if (p == null) continue;
                int d = Dist(me, GridManager.Instance.GetGridPosition(p.transform.position));
                if (p.currentHealth < bestHp || (p.currentHealth == bestHp && d < bestDist))
                {
                    bestHp = p.currentHealth;
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        return tm.GetClosestPlayer(transform.position);
    }

    public IEnumerator TakeTurnRoutine()
    {
        if (!isAwake) yield break;

        // Stun: bu turu geçer
        if (myUnit.stunTurns > 0)
        {
            myUnit.stunTurns--;
            FloatingText.Spawn(transform.position, "STUN", new Color(1f, 0.9f, 0.3f));
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        bool hasAttackedThisTurn = false;
        bool hasMovedThisTurn = false;

        while (myUnit.currentCPU > 0)
        {
            // Her adımda hedefi yeniden seç (hedef ölmüş olabilir)
            Unit target = PickTarget();
            if (target == null) break;

            Vector2Int currentPos = GridManager.Instance.GetGridPosition(transform.position);
            Vector2Int targetPos = GridManager.Instance.GetGridPosition(target.transform.position);
            int distanceToTarget = Dist(currentPos, targetPos);

            if (distanceToTarget <= attackRange && !hasAttackedThisTurn)
            {
                Debug.Log($"<color=orange>{gameObject.name} SALDIRDI!</color>");
                myUnit.UseCPU(1);
                target.TakeDamage(attackDamage);
                hasAttackedThisTurn = true;
                yield return new WaitForSeconds(0.5f);
            }
            else if (distanceToTarget > attackRange)
            {
                if (myUnit.currentMovement <= 0)
                {
                    Debug.Log($"<color=cyan>{gameObject.name} Dash yaptı (Hareket Yeniledi)!</color>");
                    myUnit.UseCPU(1);
                    myUnit.currentMovement = myUnit.maxMovement;
                }
                else if (!hasMovedThisTurn)
                {
                    myUnit.UseCPU(1);
                    hasMovedThisTurn = true;
                }

                List<Node> path = AStarPathfinding.FindPath(currentPos, targetPos, false);
                if (path == null || path.Count == 0) break;

                if (path[path.Count - 1].gridPosition == targetPos) path.RemoveAt(path.Count - 1);
                if (path.Count > myUnit.currentMovement) path = path.GetRange(0, myUnit.currentMovement);
                if (path.Count == 0) break;

                foreach (Node node in path)
                {
                    Vector3 targetWorldPos = node.worldPosition;
                    while (Vector2.Distance(new Vector2(transform.position.x, transform.position.y), new Vector2(targetWorldPos.x, targetWorldPos.y)) > 0.01f)
                    {
                        Vector3 newPos = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
                        newPos.z = newPos.y * 0.01f;
                        transform.position = newPos;
                        yield return null;
                    }
                    targetWorldPos.z = targetWorldPos.y * 0.01f;
                    transform.position = targetWorldPos;
                    currentPos = node.gridPosition;
                    myUnit.UseMovement(1);

                    // Menzile girdiyse daha fazla yaklaşma
                    if (target != null && Dist(currentPos, targetPos) <= attackRange) break;
                }
            }
            else
            {
                break;
            }
        }

        yield return new WaitForSeconds(0.5f);
    }
}
