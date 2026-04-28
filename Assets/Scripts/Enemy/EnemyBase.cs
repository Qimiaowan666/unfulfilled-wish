using UnityEngine;
using System;

[RequireComponent(typeof(PoiseMeter))]
public class EnemyBase : MonoBehaviour
{
    public float maxHP = 50f;
    public float attack = 8f;
    public int goldDrop = 10;

    public float CurrentHP { get; protected set; }
    public bool IsExecutable => GetComponent<PoiseMeter>().IsBroken && CurrentHP > 0f;

    public event Action<float, float> OnHPChanged;
    public event Action OnDied;

    protected PoiseMeter poiseMeter;

    protected virtual void Awake()
    {
        CurrentHP = maxHP;
        poiseMeter = GetComponent<PoiseMeter>();
        poiseMeter.OnPoiseBroken += OnPoiseBroken;
    }

    public void SetAnimationState(int state)
    {
        var anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
            anim.SetInteger("State", state);
    }

    public virtual void TakeDamage(float damage)
    {
        if (CurrentHP <= 0f) return;

        CurrentHP = Mathf.Max(CurrentHP - damage, 0f);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        poiseMeter.TakePoiseDamage(damage * 0.5f);

        var feedback = GetComponent<DamageFeedback>();
        if (feedback != null) feedback.Flash();
        CameraShake.Instance?.Shake(0.06f, 0.04f);

        if (CurrentHP <= 0f) Die();
    }

    public virtual void OnExecuted(float damage)
    {
        CurrentHP = 0f;
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        Die();
    }

    protected virtual void OnPoiseBroken() { }

    protected virtual void Die()
    {
        var player = FindAnyObjectByType<PlayerStats>();
        if (player != null) player.gold += goldDrop;
        OnDied?.Invoke();
        SetAnimationState(5);
        Destroy(gameObject, 1f);
    }
}
