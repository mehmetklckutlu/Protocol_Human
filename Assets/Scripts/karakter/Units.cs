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

    // UI'ın değerleri anında güncellemesi için olay (Event)
    public event Action OnStatsChanged;

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
        currentCPU = maxCPU;
        NotifyUI();
    }

    public void TakeDamage(int damage)
    {
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
        Debug.Log(gameObject.name + " yok edildi!");
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterUnit(this);
        }
        Destroy(gameObject);
    }
}