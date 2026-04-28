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

    public event Action<float, float> OnHPChanged;
    public event Action<float, float> OnGhostHPChanged;
    public event Action<float> OnDamaged;
    public event Action OnDeath;

    void Awake()
    {
        CurrentHP = maxHP;
        CurrentGhostHP = 0f;
    }

    public void OnNormalBlock(float incomingDamage)
    {
        float ghostGain = incomingDamage * ghostHPBlockRatio;
        CurrentGhostHP = Mathf.Min(CurrentGhostHP + ghostGain, maxGhostHP);
        OnGhostHPChanged?.Invoke(CurrentGhostHP, maxGhostHP);
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

        if (CurrentHP <= 0f) OnDeath?.Invoke();
    }

    void Heal(float amount)
    {
        CurrentHP = Mathf.Min(CurrentHP + amount, maxHP);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }
}
