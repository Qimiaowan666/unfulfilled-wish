using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 100f;
    public float maxGhostHP = 50f;

    [Header("Combat")]
    public float attack = 10f;
    public float defense = 5f;

    [Header("Ghost HP")]
    public float ghostHPBlockRatio = 0.5f;
    public float perfectBlockHealAmount = 20f;
    public float attackHealAmount = 5f;

    [Header("Economy")]
    public int gold = 0;

    public float CurrentHP { get; private set; }
    public float CurrentGhostHP { get; private set; }
    public bool IsInvulnerable { get; private set; }
    public bool IsDead => deathTriggered;

    public event Action<float, float> OnHPChanged;
    public event Action<float, float> OnGhostHPChanged;
    public event Action<float> OnDamaged;
    public event Action OnDeath;

    bool deathTriggered;

    void Awake()
    {
        CurrentHP = maxHP;
        CurrentGhostHP = 0f;
        deathTriggered = false;
    }

    public void OnNormalBlock(float incomingDamage)
    {
        float ghostGain = incomingDamage * ghostHPBlockRatio;
        float previousGhostHP = CurrentGhostHP;
        CurrentGhostHP += ghostGain;
        float actualGhostGain = CurrentGhostHP - previousGhostHP;

        if (actualGhostGain > 0f)
        {
            CurrentHP = Mathf.Max(CurrentHP - actualGhostGain, 0f);
            OnHPChanged?.Invoke(CurrentHP, maxHP);
            OnDamaged?.Invoke(actualGhostGain);
        }

        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);

        if (CurrentHP <= 0f) Die();
    }

    public void OnPerfectBlock()
    {
        float amount = Mathf.Min(perfectBlockHealAmount, CurrentGhostHP);
        CurrentGhostHP -= amount;
        Heal(amount);
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);
    }

    public void OnAttackHit()
    {
        if (CurrentGhostHP <= 0f) return;
        float amount = Mathf.Min(attackHealAmount, CurrentGhostHP);
        CurrentGhostHP -= amount;
        Heal(amount);
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);
    }

    public void TakeDamage(float damage)
    {
        if (damage < 0f) { Heal(-damage); return; }
        if (IsInvulnerable || deathTriggered) return;

        float penalty = CurrentGhostHP;
        CurrentGhostHP = 0f;
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);

        float total = damage + penalty;
        CurrentHP = Mathf.Max(CurrentHP - total, 0f);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnDamaged?.Invoke(total);

        var feedback = GetComponent<DamageFeedback>();
        if (feedback != null) feedback.Flash();
        CameraShake.Instance?.Shake();

        if (CurrentHP <= 0f) Die();
    }

    public void SetInvulnerable(bool value)
    {
        IsInvulnerable = value;
    }

    public void RestoreAll()
    {
        deathTriggered = false;
        IsInvulnerable = false;
        CurrentHP = maxHP;
        CurrentGhostHP = 0f;
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);
    }

    public void Kill()
    {
        if (deathTriggered) return;

        IsInvulnerable = false;
        CurrentGhostHP = 0f;
        CurrentHP = 0f;
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnDamaged?.Invoke(maxHP);
        Die();
    }

    void Heal(float amount)
    {
        if (deathTriggered) return;

        CurrentHP = Mathf.Min(CurrentHP + amount, maxHP);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }

    void Die()
    {
        if (deathTriggered) return;

        deathTriggered = true;
        OnDeath?.Invoke();
    }
}
