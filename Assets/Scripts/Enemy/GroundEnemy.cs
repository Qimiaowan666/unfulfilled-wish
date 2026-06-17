using UnityEngine;

// 地面型小怪共享基类：状态机(Idle/Move/Chase/Attack/Stunned/Dead) + 攻击池 + 命中盒 + 统一 Anim.Play。
// AoTengu(最基础，1 招)与 DemonSamurai(多招 + 变身)都继承它；boss 不走这套(自有 BossStateMachine)。
public abstract class GroundEnemy : EnemyBase
{
    // 一招的配置：动画状态名 + 触发距离 + 伤害/韧性 + 命中盒
    [System.Serializable]
    public class EnemyAttack
    {
        public string  id = "attack";                       // animator 状态名(= Anim.Play 用)
        public float   weight = 1f;                          // 抽招权重
        public float   minRange = 0f;                        // 可触发的水平距离区间
        public float   maxRange = 1.5f;
        public float   damageMultiplier = 1f;
        public float   poiseDamage = 10f;
        public Vector2 hitboxOffset = new Vector2(0.7f, 0f); // 相对身体(x 会按朝向翻转)
        public Vector2 hitboxSize   = new Vector2(1f, 0.8f);
        public float   lungeSpeed   = 0f;                    // >0：攻击中朝玩家突进(jumpattack 拉近用)
        public float   cooldownOverride = 0f;                // >0：这招专属冷却(覆盖敌人 attackCooldown)；0=用默认
        public bool    showGizmo = true;
    }

    [Header("Attacks (攻击池)")]
    public EnemyAttack[] attacks = { new EnemyAttack() };

    [Header("Movement")]
    public float patrolMoveSpeed         = 2f;
    public float battleMoveSpeed         = 3.5f;
    public float patrolDistance          = 3f;
    public float preferredCombatDistance = 1.05f;
    public float retreatDistance         = 0.75f;

    [Header("Idle / Battle")]
    public float idleTime          = 1.5f;
    public float battleTimeDuration = 5f;

    [Header("Stun")]
    public float stunDuration = 3f;

    [Header("Detection")]
    [Tooltip("横向探测半宽(以本体为中心，左右各 detectionRange)")]
    public float detectionRange = 6f;
    [Tooltip("横向探测框高度(竖直方向，太高/太低的玩家不算 → 横向探测)")]
    public float detectionHeight = 3f;
    [Tooltip("玩家与本体的垂直高差超过此值 → 视为够不到(不同台阶)，计入脱战计时")]
    public float chaseVerticalLimit = 2.5f;
    [Tooltip("发起攻击要求的最大垂直高差(只有大致同台阶才出手)")]
    public float attackVerticalTolerance = 1.2f;

    [Header("Patrol Sensing")]
    public float wallCheckDistance  = 0.35f;   // 前方探墙距离(碰撞体半宽之外)
    public float ledgeCheckDistance = 0.9f;    // 前脚外侧向下探地深度

    public Vector2 PatrolOrigin { get; private set; }

    // 玩家与本体的垂直高差(绝对值)。player 为空时返回很大值。
    public float VerticalDistToPlayer => player != null ? Mathf.Abs(player.position.y - transform.position.y) : 999f;

    Collider2D bodyColCache;
    Collider2D BodyCol => bodyColCache != null ? bodyColCache : (bodyColCache = GetComponent<Collider2D>());
    Bounds BodyBounds => BodyCol != null ? BodyCol.bounds : new Bounds(transform.position, Vector3.one * 0.5f);

    LayerMask GroundMask
    {
        get
        {
            if (groundLayer.value != 0) return groundLayer;
            int gi = LayerMask.NameToLayer("Ground");
            return gi >= 0 ? (LayerMask)(1 << gi) : default;
        }
    }

    // 前方(dir = ±1)身体高度有墙 → true
    public bool WallAhead(float dir)
    {
        var mask = GroundMask;
        if (mask.value == 0) return false;
        var b = BodyBounds;
        return Physics2D.Raycast(b.center, new Vector2(dir, 0f), b.extents.x + wallCheckDistance, mask);
    }

    // 前脚外侧下方探不到地(悬崖) → true
    public bool LedgeAhead(float dir)
    {
        var mask = GroundMask;
        if (mask.value == 0) return false;
        var b = BodyBounds;
        Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x + 0.1f), b.min.y + 0.05f);
        return !Physics2D.Raycast(origin, Vector2.down, ledgeCheckDistance, mask);
    }

    // ── 动画 clip 名(子类可覆盖；统一走 Anim.Play) ──
    public virtual string IdleClip  => "idle";
    public virtual string MoveClip  => "walk";
    public virtual string HurtClip  => "hit";
    public virtual string DeathClip => "death";
    protected virtual string ResolveClip(string baseName) => baseName;   // 变身等可加后缀
    public void PlayClip(string baseName) { if (Anim != null && !string.IsNullOrEmpty(baseName)) Anim.Play(ResolveClip(baseName), 0, 0f); }

    public void PlayIdle()          => PlayClip(IdleClip);
    public void PlayMove()          => PlayClip(MoveClip);
    public void PlayHurt()          => PlayClip(HurtClip);
    public void PlayDeath()         => PlayClip(DeathClip);
    public void PlayCurrentAttack() => PlayClip(CurrentAttack != null ? CurrentAttack.id : "attack");

    // ── 状态机 ──
    public EnemyStateMachine  stateMachine { get; private set; }
    public Enemy_IdleState    idleState    { get; private set; }
    public Enemy_MoveState    moveState    { get; private set; }
    public Enemy_ChaseState   chaseState   { get; private set; }
    public Enemy_AttackState  attackState  { get; private set; }
    public Enemy_StunnedState stunnedState { get; private set; }
    public Enemy_DeadState    deadState    { get; private set; }

    public EnemyAttack CurrentAttack { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        PatrolOrigin = transform.position;
        stateMachine = new EnemyStateMachine();
        idleState    = new Enemy_IdleState(this, stateMachine);
        moveState    = new Enemy_MoveState(this, stateMachine);
        chaseState   = new Enemy_ChaseState(this, stateMachine);
        attackState  = new Enemy_AttackState(this, stateMachine);
        stunnedState = new Enemy_StunnedState(this, stateMachine);
        deadState    = new Enemy_DeadState(this, stateMachine);
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentHP <= 0f) return;
        if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
        stateMachine.Update();
    }

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        stunnedState.SetDuration(stunDuration);
        stateMachine.ChangeState(stunnedState);
    }
    protected override void OnDeath()   => stateMachine.ChangeState(deadState);
    protected override void OnRespawn() { ResetForm(); stateMachine.ChangeState(idleState); }

    // 取 animator 里某 clip 的时长(没有则 0)
    public float ClipLength(string clipName)
    {
        if (Anim != null && Anim.runtimeAnimatorController != null)
            foreach (var c in Anim.runtimeAnimatorController.animationClips)
                if (c.name == clipName) return c.length;
        return 0f;
    }

    // 当前选中招式对应 clip 的时长(含变身 _flame 后缀解析)——攻击态兜底超时用
    public float CurrentAttackClipLength()
    {
        string id = CurrentAttack != null ? CurrentAttack.id : "attack";
        return ClipLength(ResolveClip(id));
    }

    // 死亡后等死亡动画播完再消失
    protected override float DeathDisableDelay
    {
        get { float l = ClipLength(DeathClip); return l > 0.01f ? l + 0.2f : 1f; }
    }

    protected virtual void ResetForm() { }   // 子类(变身)复活时重置形态

    // 动画事件 → 当前状态(攻击状态用它结束)
    public void AnimationTrigger() => stateMachine.currentState?.AnimationTrigger();

    // ── 选招：按距离区间 + 权重抽一个 ──
    public bool TryPickAttack(float dist)
    {
        if (attacks == null || attacks.Length == 0) return false;
        var pick = PickAttack(attacks, a => (dist >= a.minRange && dist <= a.maxRange) ? Mathf.Max(0f, a.weight) : 0f);
        if (pick == null) return false;
        CurrentAttack = pick;
        return true;
    }

    // ── 命中：攻击 clip 上的动画事件 AttackTrigger 调用(可多帧多次) ──
    int attackSwing;
    public void ResetAttackSwings() => attackSwing = 0;
    public virtual void AttackTrigger()
    {
        var a = CurrentAttack ?? (attacks != null && attacks.Length > 0 ? attacks[0] : null);
        if (a != null) PerformAttack(attack * a.damageMultiplier, a.hitboxOffset, a.hitboxSize);
        AudioManager.Instance?.PlayEnemySwing(attackSwing);   // 挥剑声(分段)
        attackSwing++;
    }

    // 横向矩形探测：宽 = detectionRange×2(左右各 detectionRange)，高 = detectionHeight
    public bool DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapBox(transform.position, new Vector2(detectionRange * 2f, detectionHeight), 0f, playerLayer);
        if (hit != null) player = hit.transform;
        return hit != null;
    }

    protected override void DrawDetectionGizmo()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRange * 2f, detectionHeight, 0.1f));   // 横向探测框
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(attackRange * 2f, detectionHeight, 0.1f));      // 攻击范围(横向)
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (attacks == null) return;
        int dir = FacingDir;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.9f);
        foreach (var a in attacks)
            if (a != null && a.showGizmo)
                Gizmos.DrawWireCube(transform.position + new Vector3(a.hitboxOffset.x * dir, a.hitboxOffset.y, 0f),
                                    new Vector3(a.hitboxSize.x, a.hitboxSize.y, 0.1f));
    }
}
