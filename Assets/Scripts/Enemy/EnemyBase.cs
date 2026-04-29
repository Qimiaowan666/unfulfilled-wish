using System.Collections;
using UnityEngine;
using System;

[RequireComponent(typeof(PoiseMeter))]
public class EnemyBase : MonoBehaviour
{
    [Header("Save")]
    public string saveID;
    public bool permanentDeath;

    public float maxHP = 50f;
    public float attack = 8f;
    public int goldDrop = 10;

    public float CurrentHP { get; protected set; }
    public bool IsDefeated => CurrentHP <= 0f;
    public bool SavesPermanentDeath => permanentDeath || GetComponent<BossAI>() != null;
    public bool RespawnsAtCheckpoint => !SavesPermanentDeath;
    public bool IsExecutable => GetComponent<PoiseMeter>().IsBroken && CurrentHP > 0f;
    public string SaveID => SaveIdUtility.GetSceneObjectID(this, saveID);

    public event Action<float, float> OnHPChanged;
    public event Action OnDied;

    protected PoiseMeter poiseMeter;
    Vector3 initialPosition;
    Quaternion initialRotation;
    Vector3 initialScale;
    Coroutine deathRoutine;

    protected virtual void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
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
        TakeDamage(damage, 0f);
    }

    public virtual void TakeDamage(float damage, float poiseDamage)
    {
        if (CurrentHP <= 0f) return;

        CurrentHP = Mathf.Max(CurrentHP - damage, 0f);
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        AudioManager.Instance?.PlayEnemyHit();
        if (poiseDamage > 0f)
            poiseMeter.TakePoiseDamage(poiseDamage);

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

    public virtual void LoadSaveState(float savedHP, bool defeated)
    {
        if (defeated)
        {
            CurrentHP = 0f;
            OnHPChanged?.Invoke(CurrentHP, maxHP);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        Respawn(savedHP > 0f ? Mathf.Clamp(savedHP, 1f, maxHP) : maxHP);
    }

    public virtual void Respawn(float hp = -1f)
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        gameObject.SetActive(true);

        CurrentHP = hp > 0f ? Mathf.Clamp(hp, 1f, maxHP) : maxHP;
        poiseMeter = poiseMeter != null ? poiseMeter : GetComponent<PoiseMeter>();
        poiseMeter?.ResetPoise();

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        GetComponent<EnemyAI>()?.ResetAIState();
        GetComponent<BossAI>()?.ResetAIState();

        OnHPChanged?.Invoke(CurrentHP, maxHP);
        SetAnimationState(0);
    }

    protected virtual void OnPoiseBroken() { }

    protected virtual void Die()
    {
        if (SavesPermanentDeath)
            SaveSystem.Instance?.MarkEnemyDefeated(SaveID);

        AudioManager.Instance?.PlayEnemyDeath();
        var player = FindAnyObjectByType<PlayerStats>();
        if (player != null) player.AddGold(goldDrop);
        OnDied?.Invoke();
        SetAnimationState(5);

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DisableAfterDeath());
    }

    IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
        deathRoutine = null;
    }
}
