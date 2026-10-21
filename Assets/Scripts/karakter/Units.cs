using UnityEngine;
using System;

public class Unit : MonoBehaviour
{
    [Header("Taraf Ayarı")]
    public bool isPlayerOwned = true; // True ise senin/dost, False ise düşman

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

    // UI'ın değerleri anında güncellemesi için olay (Event)
    public event Action OnStatsChanged;
    public event Action<Unit> OnDeath;
    private bool dead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // Karakter doğduğunda kendini TurnManager'a kaydettirir
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterUnit(this);
        }
    }

    // Tur bu karaktere geldiğinde (TurnManager çağırır)
    public void OnTurnStart()
    {
        currentMovement = maxMovement;
        bool inCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat();
        currentCPU = (usesCpu && inCombat) ? Mathf.Min(maxCPU, currentCPU + cpuPerTurn) : maxCPU;
        currentShield = 0; // Kalkan her tur başında sıfırlanır
        cardsPlayedThisTurn = 0;
        NotifyUI();
    }

    public void AddShield(int amount)
    {
        currentShield += amount;
        NotifyUI();
    }

    public void RestoreCPU(int amount)
    {
        currentCPU = Mathf.Min(maxCPU, currentCPU + amount);
        NotifyUI();
    }

    // Anında yok eder (şarj bitti, kendini patlatma)
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
        // Önce kalkan eriyor, kalan hasar cana gidiyor
        if (currentShield > 0)
        {
            int absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
            if (damage <= 0) FloatingText.Spawn(transform.position, "KALKAN", new Color(0.5f, 0.75f, 1f));
        }

        if (damage > 0) FloatingText.Spawn(transform.position, "-" + damage, new Color(1f, 0.35f, 0.3f));

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        NotifyUI();
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

        OnDeath?.Invoke(this); // loot vb.

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterUnit(this);
            if (isMainDrone) TurnManager.Instance.GameOver();
        }
        Destroy(gameObject);
    }
}