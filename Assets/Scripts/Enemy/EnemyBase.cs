using System.Collections;
using UnityEngine;
using System;

[RequireComponent(typeof(PoiseMeter))]
public class EnemyBase : Entity
{
    [Header("Save")]
    public string saveID;
    public bool permanentDeath;

    [Header("Boss")]
    public bool   isBoss;                // 标记为关卡 boss → 常驻 LevelManager 自动接管 BGM / 胜利检测
    public string nextSceneOnDefeat;     // 击败后切到的场景（留空 = 不切场景）

    [Header("Stats")]
    public float maxHP    = 50f;
    public float attack   = 8f;
    public int   goldDrop = 10;

    [Header("Detection")]
    public float     detectionRange = 6f;
    public float     attackRange    = 1.2f;
    public LayerMask playerLayer;

    [Header("Movement")]
    public float patrolMoveSpeed         = 2f;
    public float battleMoveSpeed         = 3.5f;
    public float patrolDistance          = 3f;
    public float preferredCombatDistance = 1.05f;
    public float retreatDistance         = 0.75f;

    [Header("Attack")]
    public float   attackCooldown        = 1.5f;
    public float   specialAttackCooldown = 5f;
    public float   poiseDamagePerHit     = 15f;

    [Header("Special Attack Hit Reaction")]
    public float   specialHitStunDuration = 0.6f;   // 玩家被特殊攻击命中后的硬直时长
    public float   specialHitKnockback    = 8f;     // 击退强度（普通攻击是 5f）

    [System.Serializable]
    public class AttackHitbox
    {
        public Vector2 offset     = new Vector2(0.5f, 0f);
        public Vector2 size       = new Vector2(0.8f, 0.6f);
        public Color   gizmoColor = new Color(1f, 0.3f, 0f, 0.4f);
        public bool    showGizmo  = true;
    }

    public virtual AttackHitbox GetHitbox(string id) => null;
    public AttackHitbox GetHitbox(System.Enum key) => GetHitbox(key.ToString());

    protected void DrawHitboxGizmo(AttackHitbox hb)
    {
        if (hb == null || !hb.showGizmo) return;
        int dir = FacingDir;
        Vector3 center   = transform.position + new Vector3(hb.offset.x * dir, hb.offset.y, 0f);
        Vector3 cubeSize = new Vector3(hb.size.x, hb.size.y, 0.1f);

        Gizmos.color = hb.gizmoColor;
        Gizmos.DrawCube(center, cubeSize);

        var c = hb.gizmoColor;
        Gizmos.color = new Color(c.r, c.g, c.b, 1f);
        Gizmos.DrawWireCube(center, cubeSize);
    }

    [Header("Special Attack")]
    public float specialAttackWarningDuration = 0.5f;
    public float dashAttackSpeed             = 8f;
    public float dashAttackDuration          = 0.35f;

    [Header("Idle / Battle")]
    public float idleTime          = 1.5f;
    public float battleTimeDuration = 5f;

    [Header("Stun")]
    public float stunDuration = 3f;

    // ── Runtime State ────────────────────────────────────────────────
    public float     CurrentHP            { get; protected set; }
    public bool      Invincible           { get; set; }   // 位移帧（瞬移/跳跃）期间不可被伤
    public Transform player               { get; protected set; }
    public float     attackCooldownTimer  { get; protected set; }
    public float     specialCooldownTimer { get; protected set; }
    public Vector2   PatrolOrigin         { get; private set; }

    public bool IsDefeated          => CurrentHP <= 0f;
    public bool SavesPermanentDeath => permanentDeath || GetComponent<MinotaurBoss>() != null;
    public bool RespawnsAtCheckpoint => !SavesPermanentDeath;
    public bool IsExecutable        => GetComponent<PoiseMeter>().IsBroken && CurrentHP > 0f;
    public string SaveID            => SaveIdUtility.GetSceneObjectID(this, saveID);

    // ── Events ───────────────────────────────────────────────────────
    public event Action<float, float> OnHPChanged;
    public event Action OnDied;
    public event Action OnHit;

    // 是否已经初始化过（Awake 跑过）。预留的 inactive 敌人从未 Awake → false，
    // 存档刷新时跳过它们，避免被 Respawn 到未记录的 initialPosition(0,0,0) 飞出去。
    public bool Initialized { get; private set; }

    protected PoiseMeter poiseMeter;
    Vector3    initialPosition;
    Quaternion initialRotation;
    Vector3    initialScale;
    Coroutine  deathRoutine;

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale    = transform.localScale;
        PatrolOrigin    = transform.position;
        Initialized     = true;
        CurrentHP       = maxHP;
        poiseMeter      = GetComponent<PoiseMeter>();
        poiseMeter.OnPoiseBroken += OnPoiseBroken;

        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex >= 0 && (playerLayer.value & (1 << playerLayerIndex)) == 0)
            playerLayer = 1 << playerLayerIndex;

        specialCooldownTimer = specialAttackCooldown;
    }

    // ── AI Utilities (used by enemy states) ──────────────────────────
    public bool DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
        if (hit != null) player = hit.transform;
        return hit != null;
    }

    public void LoseTarget() => player = null;

    public float GetHorizontalDistToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    public void HitPlayer(float damage, float poiseDamage, bool isSpecialAttack = false)
    {
        if (player == null) return;
        ApplyHitToCollider(player.GetComponent<Collider2D>() ?? player.GetComponentInChildren<Collider2D>(), damage, isSpecialAttack);
    }

    public bool PerformAttack(float damage, Vector2 offset, Vector2 size, bool isSpecialAttack = false)
    {
        Vector2 origin = (Vector2)transform.position +
                         new Vector2(offset.x * FacingDir, offset.y);
        var hits = Physics2D.OverlapBoxAll(origin, size, 0f, playerLayer);
        foreach (var hit in hits)
            ApplyHitToCollider(hit, damage, isSpecialAttack);
        return hits.Length > 0;
    }

    // ── 通用按权重选招（所有敌人共用）─────────────────────────────────
    // 基类只含 weight，各敌人继承加自己的 enum id 和扩展字段
    [System.Serializable]
    public class AttackWeight
    {
        public float weight = 1f;
    }

    // options：候选攻击列表；getWeight：每个攻击的权重（不可用返回 0）
    // 返回中签的 option，全部为 0 时返回 null
    public T PickAttack<T>(T[] options, System.Func<T, float> getWeight) where T : class
    {
        if (options == null || options.Length == 0) return null;
        float[] weights = new float[options.Length];
        for (int i = 0; i < options.Length; i++)
            weights[i] = getWeight(options[i]);
        int idx = WeightedPicker.Pick(weights);
        return idx >= 0 ? options[idx] : null;
    }

    void ApplyHitToCollider(Collider2D hit, float damage, bool isSpecialAttack = false)
    {
        if (hit == null) return;
        var ctrl  = hit.GetComponent<PlayerController>()  ?? hit.GetComponentInParent<PlayerController>();
        var stats = hit.GetComponent<PlayerStats>()       ?? hit.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.IsInvulnerable) return;

        // 被命中（无论格挡与否）→ 转身面向攻击来源
        if (ctrl != null)
            ctrl.SetFacing(Mathf.Sign(transform.position.x - ctrl.transform.position.x));

        bool isBlocking   = ctrl != null && ctrl.IsBlocking;
        bool isCountering = ctrl != null && ctrl.IsCountering;
        var  feedback     = hit.GetComponent<DamageFeedback>() ?? hit.GetComponentInParent<DamageFeedback>();

        if (isSpecialAttack)
        {
            // 特殊攻击：识破成功 → 不受伤，给boss poise伤害
            if (isCountering && ctrl.TryCounter(this)) return;
            // 特殊攻击无视格挡 → 全伤 + 击退 + 硬直（阻断后续段识破）
            stats.TakeDamage(damage);
            ctrl?.Stun(specialHitStunDuration);                                   // 先切到 stunnedState（Enter 会归零速度）
            feedback?.ApplyKnockback(transform.position, specialHitKnockback);    // 再设击退速度（KnockbackRoutine 在 stunnedState 之后接管）
            return;
        }

        // 普通攻击：识破状态无法防御 → 全伤
        if (isCountering)
        {
            stats.TakeDamage(damage);
            feedback?.ApplyKnockback(transform.position, 5f);
            return;
        }

        if (feedback != null && !isBlocking) feedback.ApplyKnockback(transform.position, 5f);
        if (isBlocking) ctrl.ReceiveBlockHit(damage);
        else            stats.TakeDamage(damage);
    }

    public bool TryTriggerCounter()
    {
        if (player == null) return false;
        var ctrl = player.GetComponent<PlayerController>();
        return ctrl != null && ctrl.TryCounter(this);
    }

    public void StartAttackCooldown()  => attackCooldownTimer  = attackCooldown;
    public void StartSpecialCooldown() => specialCooldownTimer = specialAttackCooldown;
    public void ResetPoise()           => poiseMeter?.ResetPoise();

    // ── Damage / Death ───────────────────────────────────────────────
    public virtual void TakeDamage(float damage) => TakeDamage(damage, 0f);

    public virtual void TakeDamage(float damage, float poiseDamage)
    {
        if (CurrentHP <= 0f) return;
        if (Invincible) return;   // 瞬移/跳劈飞行中免伤
        CurrentHP = Mathf.Max(CurrentHP - damage, 0f);
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余 HP：{CurrentHP}/{maxHP}");
        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnHit?.Invoke();
        AudioManager.Instance?.PlayEnemyHit();
        if (poiseDamage > 0f) poiseMeter.TakePoiseDamage(poiseDamage);

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

    protected virtual void Die()
    {
        if (SavesPermanentDeath)
            SaveSystem.Instance?.MarkEnemyDefeated(SaveID);

        AudioManager.Instance?.PlayEnemyDeath();
        var playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null) playerStats.AddGold(goldDrop);
        OnDied?.Invoke();

        OnDeath();

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DisableAfterDeath());
    }

    // Override in subclasses to handle death animation via state machine
    protected virtual void OnDeath() => SetAnimBool("isDead");

    // Override in subclasses to reset state machine
    protected virtual void OnRespawn() => SetAnimBool("isIdle");

    protected virtual void OnPoiseBroken() { }

    // 被玩家识破时调用（子类重写来打断当前攻击 / 进入硬直）
    public virtual void OnCountered() { }

    IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
        deathRoutine = null;
    }

    // ── Save System ──────────────────────────────────────────────────
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

        transform.position   = initialPosition;
        transform.rotation   = initialRotation;
        transform.localScale = initialScale;
        gameObject.SetActive(true);

        CurrentHP = hp > 0f ? Mathf.Clamp(hp, 1f, maxHP) : maxHP;
        poiseMeter = poiseMeter != null ? poiseMeter : GetComponent<PoiseMeter>();
        poiseMeter?.ResetPoise();

        Rb.linearVelocity    = Vector2.zero;
        attackCooldownTimer  = 0f;
        specialCooldownTimer = specialAttackCooldown;
        player               = null;

        OnHPChanged?.Invoke(CurrentHP, maxHP);
        OnRespawn();
    }

    // ── Animation Bool Helper (used by BossAI) ───────────────────────
    public virtual void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var param in Anim.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool)
                Anim.SetBool(param.name, param.name == boolName);
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        // hitboxes 由具体子类 OnDrawGizmos 调 DrawHitboxGizmo 绘制
    }
}
