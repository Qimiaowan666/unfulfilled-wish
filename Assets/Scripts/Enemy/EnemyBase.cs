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
    public bool   isBoss;                // 标记为关卡 boss → 常驻 LevelManager 自动接管 BGM / 胜利检测；血条/名牌按此识别
    public string nextSceneOnDefeat;     // 击败后切到的场景（留空 = 不切场景）
    public BossProfile profile;          // 名牌/血条展示数据（名字、罗马名、单字图、配色）
    [Tooltip("false = boss 沉睡，不主动进战；由 BossIntroTrigger 登场后 Activate() 唤醒。普通敌人保持 true")]
    public bool   combatEnabled = true;
    public virtual void Activate() => combatEnabled = true;

    // boss 战进行中：任意 boss 已被唤醒(combatEnabled)且存活 → 此期间禁止存档(火堆/暂停菜单)
    public static bool AnyBossInCombat()
    {
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e.isBoss && e.combatEnabled && e.CurrentHP > 0f) return true;
        return false;
    }

    [Header("Stats")]
    public float maxHP    = 50f;
    public float attack   = 8f;
    public int   goldDrop = 10;

    [Header("Detection")]
    public float     attackRange = 1.2f;   // 出手距离(boss 用；GroundEnemy 也用作攻击范围 gizmo)
    public LayerMask playerLayer;

    [Header("Attack")]
    public float   attackCooldown        = 1.5f;

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

    // ── Runtime State ────────────────────────────────────────────────
    public float     CurrentHP            { get; protected set; }
    public bool      Invincible           { get; set; }   // 位移帧（瞬移/跳跃）期间不可被伤
    public Transform player               { get; protected set; }
    public float     attackCooldownTimer  { get; protected set; }

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
    bool       initialFacingRight;
    Coroutine  deathRoutine;

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale    = transform.localScale;
        initialFacingRight = FacingRight;
        Initialized     = true;
        CurrentHP       = maxHP;
        poiseMeter      = GetComponent<PoiseMeter>();
        poiseMeter.OnPoiseBroken += OnPoiseBroken;

        int playerLayerIndex = LayerMask.NameToLayer(Layers.Player);
        if (playerLayerIndex >= 0 && (playerLayer.value & (1 << playerLayerIndex)) == 0)
            playerLayer = 1 << playerLayerIndex;
    }

    // ── AI Utilities (used by enemy states) ──────────────────────────
    // 感知玩家由 GroundEnemy.DetectPlayer 实现(横向矩形)；boss 不感知，靠 tag 锁定。

    public void LoseTarget() => player = null;

    // 一旦开战(combatEnabled)就锁定玩家、永不脱战；玩家真不在场(死亡/不在)才返回 false
    public bool EnsurePlayer()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(Tags.Player);
            if (p != null) player = p.transform;
        }
        return player != null;
    }

    // 是否继续交战(boss 用)：靠 tag 锁定玩家，开战后永不脱战
    public bool KeepEngaged() => EnsurePlayer();

    public float GetHorizontalDistToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    // boss / 简单调用：isSpecial 决定击退/硬直(普通 5/0；特殊用 specialHit*)
    public bool PerformAttack(float damage, Vector2 offset, Vector2 size, bool isSpecialAttack = false)
        => PerformAttack(damage, offset, size, isSpecialAttack,
                         isSpecialAttack ? specialHitKnockback : 5f,
                         isSpecialAttack ? specialHitStunDuration : 0f);

    // 地面怪逐招：显式 unblockable + 击退 + 硬直
    public bool PerformAttack(float damage, Vector2 offset, Vector2 size, bool unblockable, float knockback, float hitStun)
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(offset.x * FacingDir, offset.y);
        var hits = Physics2D.OverlapBoxAll(origin, size, 0f, playerLayer);
        foreach (var hit in hits)
            ApplyHitToCollider(hit, damage, unblockable, knockback, hitStun);
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

    // public：近战 PerformAttack 与远程 Arrow 共用同一套命中处理(格挡/识破/击退)
    public void ApplyHitToCollider(Collider2D hit, float damage, bool unblockable, float knockback, float hitStun)
    {
        if (hit == null) return;
        var ctrl  = hit.GetComponent<PlayerController>();
        var stats = hit.GetComponent<PlayerStats>();
        if (stats == null || stats.IsInvulnerable) return;

        // 被命中（无论格挡与否）→ 转身面向攻击来源
        if (ctrl != null)
            ctrl.FaceToward(transform.position);

        bool isBlocking   = ctrl != null && ctrl.IsBlocking;
        bool isCountering = ctrl != null && ctrl.IsCountering;
        var  feedback     = hit.GetComponent<DamageFeedback>();

        // ── 防御判定（剪刀石头布）：普通招只能「格挡」防、危招(unblockable)只能「识破」防 ──
        if (unblockable)
        {
            // 危招无视格挡；识破成功 → 免伤(并给敌人 poise)，否则继续往下吃实打
            if (isCountering && ctrl.TryCounter(this)) return;
        }
        else if (isBlocking)
        {
            // 普通招被格挡 → 走格挡处理(削韧/掉耐久)，不吃伤不击退
            ctrl.ReceiveBlockHit(damage, this);
            return;
        }

        // ── 没防住：扣血 + 击退 ──
        // 击退是 DamageFeedback 给的一段会衰减的水平速度，但 idle/move 每帧会重设速度盖掉它；
        // 所以推人前必须先把玩家切进硬直态(StunnedState 不清速度)，击退才滑得出来。
        stats.TakeDamage(damage);
        float stun = hitStun;
        if (knockback > 0f && feedback != null)
            stun = Mathf.Max(stun, feedback.knockbackDuration);            // 至少硬直到击退滑完
        if (stun > 0f) ctrl?.Stun(stun);                                   // 先进硬直(Enter 归零速度)
        feedback?.ApplyKnockback(transform.position, knockback);           // 再给击退速度
    }

    public void StartAttackCooldown()           => attackCooldownTimer = attackCooldown;
    public void StartAttackCooldown(float secs) => attackCooldownTimer = secs;   // 指定时长(per-招冷却用)
    public void ResetPoise()           => poiseMeter?.ResetPoise();

    // 只削韧性、不掉血（完美格挡反震用）：累计破韧 → OnPoiseBroken → 硬直
    public void ApplyPoiseDamage(float poise)
    {
        if (CurrentHP <= 0f || Invincible || poise <= 0f) return;
        poiseMeter?.TakePoiseDamage(poise);
    }

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

        // 死亡音改由死亡动画"摔倒落地"帧的 AnimDeathFall 事件触发，避免和受击音同帧叠掉
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

    // 死亡后多久消失(子类按死亡动画时长覆盖，避免动画没播完就消失)
    protected virtual float DeathDisableDelay => 1f;

    IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(DeathDisableDelay);
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
        FacingRight          = initialFacingRight;   // 跟 localScale 一起复位，避免读档后朝向逻辑与视觉脱节、攻击打反方向
        gameObject.SetActive(true);

        CurrentHP = hp > 0f ? Mathf.Clamp(hp, 1f, maxHP) : maxHP;
        poiseMeter = poiseMeter != null ? poiseMeter : GetComponent<PoiseMeter>();
        poiseMeter?.ResetPoise();

        Rb.linearVelocity    = Vector2.zero;
        attackCooldownTimer  = 0f;
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
        DrawDetectionGizmo();
        // hitboxes 由具体子类 OnDrawGizmos 调 DrawHitboxGizmo 绘制
    }

    // boss 不用感知(开战后靠 tag 锁定玩家、永不脱战)，只画攻击距离；GroundEnemy 覆盖为横向探测框
    protected virtual void DrawDetectionGizmo()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
