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
    [Tooltip("Her saldırıda bu aralıktan rastgele hasar atılır.")]
    public IntRange attackDamage = new IntRange(10, 16);
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
        // Kontroller bütün düşmanlarda aynı karede yığılmasın.
        nextWakeCheck = Time.time + Random.value * WakeCheckInterval;
        if (GridManager.Instance != null)
        {
            Vector2Int startGridPos = GridManager.Instance.GetGridPosition(transform.position);
            Vector3 snappedPos = GridManager.Instance.GetWorldPosition(startGridPos);
            snappedPos.z = snappedPos.y * 0.01f;
            transform.position = snappedPos;
        }
    }

    // Uyuyan düşmanlar oyuncuyu her karede değil, saniyede ~10 kez kontrol eder (çok düşmanlı bölgelerde takılmayı önler).
    private const float WakeCheckInterval = 0.1f;
    private float nextWakeCheck;

    private void Update()
    {
        if (isAwake) return;

        if (TurnManager.Instance == null || GridManager.Instance == null) return;
        if (Time.time < nextWakeCheck) return;
        nextWakeCheck = Time.time + WakeCheckInterval;

        Vector2Int currentPos = GridManager.Instance.GetGridPosition(transform.position);
        if (FogOfWar.Instance != null && !FogOfWar.Instance.IsSeenByPlayer(currentPos)) return;

        foreach (Unit target in TurnManager.Instance.playerUnits)
        {
            if (target == null) continue;

            Vector2Int targetPos = GridManager.Instance.GetGridPosition(target.transform.position);
            int distanceToTarget = Mathf.Abs(currentPos.x - targetPos.x) + Mathf.Abs(currentPos.y - targetPos.y);

            if (distanceToTarget > visionRange) continue;

            RaycastHit2D hit = Physics2D.Linecast(transform.position, target.transform.position, obstacleLayer);
            if (hit.collider != null) continue;

            WakeUp();
            return;
        }
    }

    private void WakeUp()
    {
        isAwake = true;
        Sfx.Play("enemy_alert", 0.6f, 0.03f, 0.6f);
        Debug.Log($"<color=red>{gameObject.name} oyuncuyu FARK ETTİ!</color>");

        TurnManager.Instance.UpdateCombatState();

        if (TurnOrderUI.Instance != null)
            TurnOrderUI.Instance.AddUnitToQueue(myUnit);

        StartCoroutine(TurnManager.Instance.ShowEnemyAlert());
    }

    private int Dist(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    /// <summary>Hedef menzilde mi ve araya duvar girmiyor mu?</summary>
    private bool CanHit(Vector2Int from, Vector2Int to)
    {
        return Dist(from, to) <= attackRange && GridManager.Instance.HasLineOfSight(from, to);
    }

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

    private List<Node> FindPathToTarget(Vector2Int currentPos, Vector2Int targetPos)
    {
        List<Node> path = AStarPathfinding.FindPath(currentPos, targetPos, false, myUnit, allowOccupiedTarget: true);
        if (path != null && path.Count > 0)
        {
            if (path[path.Count - 1].gridPosition == targetPos)
                path.RemoveAt(path.Count - 1);
            if (path.Count > 0)
                return path;
        }

        // Hedefin etrafı başka dronlarla/duvarlarla tamamen kapandıysa ulaşılabilir en yakın boş kareye yürü
        int currentDist = Dist(currentPos, targetPos);
        List<Node> reachable = GridManager.Instance.GetNodesInRange(currentPos, 40, false, myUnit);
        Node bestNode = null;
        int bestTargetDist = currentDist;
        int bestStepDist = int.MaxValue;

        foreach (Node n in reachable)
        {
            if (n == null || n.gridPosition == currentPos) continue;
            if (GridManager.Instance.IsCellOccupied(n.gridPosition, myUnit)) continue;

            int td = Dist(n.gridPosition, targetPos);
            int sd = Dist(currentPos, n.gridPosition);
            if (td < bestTargetDist || (td == bestTargetDist && td < currentDist && sd < bestStepDist))
            {
                bestTargetDist = td;
                bestStepDist = sd;
                bestNode = n;
            }
        }

        if (bestNode != null)
            return AStarPathfinding.FindPath(currentPos, bestNode.gridPosition, false, myUnit, allowOccupiedTarget: false);

        return null;
    }

    public IEnumerator TakeTurnRoutine()
    {
        if (!isAwake) yield break;

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
            Unit target = PickTarget();
            if (target == null) break;

            Vector2Int currentPos = GridManager.Instance.GetGridPosition(transform.position);
            Vector2Int targetPos = GridManager.Instance.GetGridPosition(target.transform.position);
            // Menzilde olmak yetmez: araya duvar giriyorsa saldıramaz, görüş açısı bulana kadar yürür.
            bool canHit = CanHit(currentPos, targetPos);

            if (canHit && !hasAttackedThisTurn)
            {
                Debug.Log($"<color=orange>{gameObject.name} SALDIRDI!</color>");
                myUnit.UseCPU(1);
                myUnit.PlayAttack(attackRange > 1);
                int dmg = attackDamage.Roll();
                target.TakeDamage(dmg, false, attackDamage.Normalize(dmg));
                hasAttackedThisTurn = true;
                yield return new WaitForSeconds(0.75f);
            }
            else if (!canHit)
            {
                List<Node> path = FindPathToTarget(currentPos, targetPos);
                if (path == null || path.Count == 0) break;

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

                if (path.Count > myUnit.currentMovement) path = path.GetRange(0, myUnit.currentMovement);
                if (path.Count == 0) break;

                int stepsMoved = 0;
                foreach (Node node in path)
                {
                    if (GridManager.Instance.IsCellOccupied(node.gridPosition, myUnit))
                        break;

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
                    stepsMoved++;

                    if (target != null && CanHit(currentPos, targetPos)) break;
                }

                if (stepsMoved == 0) break;
            }
            else
            {
                break;
            }
        }

        yield return new WaitForSeconds(0.5f);
    }
}
