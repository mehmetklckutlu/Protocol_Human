using UnityEngine;
using System;
using System.Collections;

public class Unit : MonoBehaviour
{
    [Header("Taraf Ayarı")]
    public bool isPlayerOwned = true;

    [Header("Sağlık (HP)")]
    public int maxHealth = 120;
    public int currentHealth;

    [Header("Hareket (Movement)")]
    public int maxMovement = 5;
    public int currentMovement;

    [Header("Aksiyon (CPU)")]
    public int maxCPU = 4;
    public int currentCPU;
    [Tooltip("Savaşta her tur başında kazanılan CPU (en fazla Max CPU'ya kadar). Savaş dışında CPU tamamen dolar.")]
    [Min(0)] public int cpuPerTurn = 2;

    [Header("Kalkan")]
    public int currentShield;

    [Header("Rol")]
    [Tooltip("Açık: oyuncunun ana drone'u. Ölürse oyun kaybedilir.")]
    public bool isMainDrone = false;
    [Tooltip("Kapalı: CPU kullanmaz, tur başına 'Cards Per Turn' kadar kart oynar (dost drone'lar).")]
    public bool usesCpu = true;
    [Min(1)] public int cardsPerTurn = 1;
    [HideInInspector] public int cardsPlayedThisTurn;

    [Header("Durum Etkileri (çalışırken)")]
    public int stunTurns;
    public int tauntTurns;

    public event Action OnStatsChanged;
    public event Action<Unit> OnDeath;
    private bool dead;
    public bool IsDead => dead;

    [Header("Animasyon")]
    [Tooltip("Boş bırakılırsa bu objede veya çocuklarında Animator aranır.")]
    [SerializeField] private Animator animator;
    [Tooltip("Ölüm animasyonu için en fazla beklenecek süre (saniye).")]
    [SerializeField, Min(0f)] private float maxDeathAnimTime = 2f;

    private Coroutine hitFlashCoroutine;
    private SpriteRenderer mainSpriteRenderer;
    private Color baseSpriteColor = Color.white;
    private Vector3 baseLocalScale = Vector3.one;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (animator == null) animator = AnimUtil.Find(this);
        mainSpriteRenderer = GetComponent<SpriteRenderer>();
        if (mainSpriteRenderer == null) mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (mainSpriteRenderer != null) baseSpriteColor = mainSpriteRenderer.color;
        baseLocalScale = transform.localScale;
    }

    public void PlayAttack(bool isRanged = false)
    {
        if (dead) return;
        if (isRanged && AnimUtil.Trigger(animator, AnimUtil.RangedAttack)) return;
        if (!isRanged && AnimUtil.Trigger(animator, AnimUtil.MeleeAttack)) return;
        AnimUtil.Trigger(animator, AnimUtil.Attack);
    }

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterUnit(this);
        }
    }

    public void OnTurnStart()
    {
        currentMovement = maxMovement;
        bool inCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
        currentCPU = (usesCpu && inCombat) ? Mathf.Min(maxCPU, currentCPU + cpuPerTurn) : maxCPU;
        currentShield = 0;
        cardsPlayedThisTurn = 0;
        NotifyUI();
    }

    public void AddShield(int amount)
    {
        if (amount <= 0) return;
        currentShield += amount;
        FloatingText.Spawn(transform.position, "+" + amount + " KALKAN", new Color(0.25f, 0.78f, 1f));
        NotifyUI();
    }

    public void RestoreCPU(int amount)
    {
        currentCPU = Mathf.Min(maxCPU, currentCPU + amount);
        NotifyUI();
    }

    public void Kill()
    {
        if (dead) return;
        currentHealth = 0;
        NotifyUI();
        Die();
    }

    public void TakeDamage(int damage)
    {
        if (dead) return;
        if (currentShield > 0)
        {
            int absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
            if (damage <= 0) FloatingText.Spawn(transform.position, "KALKAN", new Color(0.3f, 0.8f, 1f));
        }

        if (damage > 0) FloatingText.Spawn(transform.position, "-" + damage, new Color(1f, 0.35f, 0.3f));

        TacticalCamera.Shake(damage > 0 ? 0.13f : 0.06f, 0.16f);
        if (gameObject.activeInHierarchy)
        {
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine(damage > 0));
        }

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        NotifyUI();
    }

    private IEnumerator HitFlashRoutine(bool healthDamage)
    {
        Color flashColor = healthDamage ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.45f, 0.85f, 1f, 1f);
        if (mainSpriteRenderer != null) mainSpriteRenderer.color = flashColor;
        transform.localScale = baseLocalScale * 1.12f;

        float elapsed = 0f;
        const float duration = 0.16f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (mainSpriteRenderer != null)
                mainSpriteRenderer.color = Color.Lerp(flashColor, baseSpriteColor, t);
            transform.localScale = Vector3.Lerp(baseLocalScale * 1.12f, baseLocalScale, t);
            yield return null;
        }

        if (mainSpriteRenderer != null) mainSpriteRenderer.color = baseSpriteColor;
        transform.localScale = baseLocalScale;
        hitFlashCoroutine = null;
    }

    public void UseMovement(int amount)
    {
        currentMovement -= amount;
        NotifyUI();
    }

    public void UseCPU(int amount)
    {
        currentCPU -= amount;
        NotifyUI();
    }

    private void NotifyUI()
    {
        OnStatsChanged?.Invoke();
    }

    private void Die()
    {
        if (dead) return;
        dead = true;
        Debug.Log(gameObject.name + " yok edildi!");

        OnDeath?.Invoke(this);

        bool hasAnim = AnimUtil.Trigger(animator, AnimUtil.Die);
        bool lost = false;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterUnit(this);
            if (isMainDrone)
            {
                TurnManager.Instance.GameOver(!hasAnim);
                lost = true;
            }
        }

        if (hasAnim)
        {
            UnitMovement move = GetComponent<UnitMovement>();
            if (move != null) move.enabled = false;
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;
            StartCoroutine(DeathRoutine(lost));
        }
        else
            Destroy(gameObject);
    }

    private IEnumerator DeathRoutine(bool showGameOver)
    {
        yield return AnimUtil.WaitForState(animator, AnimUtil.Die, maxDeathAnimTime);
        if (showGameOver)
        {
            GameOverUI.Show();
            yield break;
        }
        Destroy(gameObject);
    }
}
