using UnityEngine;
using System;
using System.Collections;

public class Unit : MonoBehaviour
{
    [Header("Taraf Ayarı")]
    public bool isPlayerOwned = true;

    [Header("Sağlık (HP)")]
    public int maxHealth = 160;
    public int currentHealth;

    [Header("Hareket (Movement)")]
    public int maxMovement = 5;
    public int currentMovement;

    [Header("Aksiyon (CPU)")]
    public int maxCPU = 6;
    public int currentCPU;
    [Tooltip("Savaşta her tur başında kazanılan CPU (en fazla Max CPU'ya kadar). Savaş dışında CPU tamamen dolar.")]
    [Min(0)] public int cpuPerTurn = 3;

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
    private Sprite initialSprite;
    private Color baseSpriteColor = Color.white;
    private Vector3 baseLocalScale = Vector3.one;

    public Sprite PortraitSprite => initialSprite != null ? initialSprite : (mainSpriteRenderer != null ? mainSpriteRenderer.sprite : null);

    private void Awake()
    {
        if (isMainDrone)
        {
            maxHealth = 160;
            maxCPU = 6;
            cpuPerTurn = 3;
        }
        currentHealth = maxHealth;
        if (animator == null) animator = AnimUtil.Find(this);
        mainSpriteRenderer = GetComponent<SpriteRenderer>();
        if (mainSpriteRenderer == null) mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (mainSpriteRenderer != null)
        {
            baseSpriteColor = mainSpriteRenderer.color;
            initialSprite = mainSpriteRenderer.sprite;
        }
        baseLocalScale = transform.localScale;
    }

    public void PlayAttack(bool isRanged = false)
    {
        if (dead) return;
        Sfx.Play(isRanged ? "attack_ranged" : "attack_melee", 0.8f);
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
        Sfx.Play("shield", 0.7f);
        DamageNumber.SpawnShieldGain(transform.position, amount);
        NotifyUI();
    }

    public void RestoreCPU(int amount)
    {
        if (amount > 0) Sfx.Play("cpu_restore", 0.6f, 0.04f, 0.15f);
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

    /// <param name="crit">Kritik vuruş: sayı büyük ve sarı çıkar, kamera daha çok sallanır.</param>
    /// <param name="roll01">Hasarın aralıktaki yeri (0 = en düşük, 1 = en yüksek); yazının boyutunu etkiler.</param>
    public void TakeDamage(int damage, bool crit = false, float roll01 = 0.5f)
    {
        if (dead) return;
        int absorbed = 0;
        if (currentShield > 0)
        {
            absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
        }

        if (absorbed > 0) DamageNumber.SpawnBlocked(transform.position, absorbed, damage > 0);
        if (damage > 0) Sfx.Play(crit ? "crit" : (isPlayerOwned && isMainDrone ? "player_hurt" : "hit"), 0.85f, 0.08f, 0.03f);
        else if (absorbed > 0) Sfx.Play("shield_hit", 0.7f, 0.06f, 0.03f);
        if (damage > 0) DamageNumber.Spawn(transform.position, damage, crit, roll01);

        float shake = damage <= 0 ? 0.06f : crit ? 0.24f : Mathf.Lerp(0.1f, 0.17f, roll01);
        TacticalCamera.Shake(shake, crit ? 0.22f : 0.16f);
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
        Sfx.Play(isMainDrone ? "game_over" : "unit_death", 0.85f, 0.06f, 0.05f);

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
