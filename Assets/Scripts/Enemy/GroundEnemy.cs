using UnityEngine;

public enum AttackDelivery { Melee, Ranged }              // 招的出伤方式：近战命中盒 / 远程发射箭矢
public enum LungeDir       { None, Forward, Backward }    // 出招时移动：不动 / 朝玩家前冲 / 背对后撤
public enum StepMove       { None, Approach, Retreat, TeleportBehind, TeleportFront, TeleportOtherSide, Jump }   // 连段段前位移(Jump=跳劈接近)

// 地面型小怪共享基类：状态机(Idle/Move/Chase/Attack/Stunned/Dead) + 巡逻/探测/位移。
// 攻击池/连段/命中/动画 已上移到 EnemyBase(小怪与 boss 共用)。
// AoTengu(最基础，1 招)与 DemonSamurai(多招 + 变身)都继承它。
public abstract class GroundEnemy : EnemyBase
{
    [Header("Movement")]
    public float patrolMoveSpeed         = 2f;
    public float battleMoveSpeed         = 3.5f;
    public override float AttackMoveSpeed => battleMoveSpeed;   // 攻击运行器段前位移用
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

    // 被动态：原地待命,不感知 / 不巡逻 / 不攻击。运行时由外部设(如 TutorialEnemy),
    // 不序列化、不在 Inspector 显示、正常怪默认 false —— 不让教程概念污染这个核心类。
    public bool Passive { get; set; }

    // 钉死态：照常感知 / 攻击(放箭),但绝不移动、绝不播跑步动画(训练射手:站定放箭)。同样运行时设、不序列化。
    public bool Stationary { get; set; }
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
            int gi = LayerMask.NameToLayer(Layers.Ground);
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
    public override bool LedgeAhead(float dir)
    {
        var mask = GroundMask;
        if (mask.value == 0) return false;
        var b = BodyBounds;
        Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x + 0.1f), b.min.y + 0.05f);
        return !Physics2D.Raycast(origin, Vector2.down, ledgeCheckDistance, mask);
    }

    // ── 状态机 ──
    public EnemyStateMachine  stateMachine { get; private set; }
    public Enemy_IdleState    idleState    { get; private set; }
    public Enemy_MoveState    moveState    { get; private set; }
    public Enemy_ChaseState   chaseState   { get; private set; }
    public Enemy_AttackState  attackState  { get; private set; }
    public Enemy_StunnedState stunnedState { get; private set; }
    public Enemy_DeadState    deadState    { get; private set; }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ValidateAttacks();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 开发期自检：把"招配错却静默不报错"的坑变成显式警告(id 对不上 animator 状态 / 远程招缺箭矢预制)
    void ValidateAttacks()
    {
        if (attacks == null) return;
        for (int i = 0; i < attacks.Length; i++)
        {
            var a = attacks[i];
            if (a == null) { Debug.LogWarning($"[{name}] attacks[{i}] 为空", this); continue; }
            string clip = ResolveClip(a.id);
            if (Anim == null)
                Debug.LogWarning($"[{name}] 没有 Animator,招 '{a.id}' 播不出来", this);
            else if (string.IsNullOrEmpty(clip) || !Anim.HasState(0, Animator.StringToHash(clip)))
                Debug.LogWarning($"[{name}] 招 '{a.id}' 在 Animator 里找不到同名状态 → 出招放不出动画(Fire 事件也不会触发)", this);
            if (a.hits == null || a.hits.Length == 0)
                Debug.LogWarning($"[{name}] 招 '{a.id}' 没有任何 hit → Fire 打不出东西", this);
            else for (int j = 0; j < a.hits.Length; j++)
                if (a.hits[j] != null && a.hits[j].delivery == AttackDelivery.Ranged && a.hits[j].projectilePrefab == null)
                    Debug.LogWarning($"[{name}] 招 '{a.id}' 第 {j} 下是远程(Ranged)却没配 projectilePrefab → 不会出箭", this);
        }

        // 连段自检:每段引用的 id 必须在 attacks 里存在
        if (combos != null)
            foreach (var c in combos)
            {
                if (c == null || c.steps == null) continue;
                foreach (var s in c.steps)
                    if (s != null && FindAttack(s.attackId) == null)
                        Debug.LogWarning($"[{name}] 连段 '{c.name}' 引用了不存在的招 id '{s.attackId}' → 该段会中断连段", this);
            }
    }
#endif

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

    // 死亡后等死亡动画播完再消失
    protected override float DeathDisableDelay
    {
        get { float l = ClipLength(DeathClip); return l > 0.01f ? l + 0.2f : 1f; }
    }

    protected virtual void ResetForm() { }   // 子类(变身)复活时重置形态

    // 动画事件 → 统一攻击运行器(攻击 clip 放完 → 推进连段 / 退出)
    public void AnimationTrigger() => Attack.OnAnimEnd();

    // 动画事件：死亡动画"摔倒落地"那一帧 → 播倒地音(从 Die 挪来，和受击音错开)
    public void AnimDeathFall() => AudioManager.Instance?.PlayEnemyDeath();

    // 横向矩形探测：宽 = detectionRange×2(左右各 detectionRange)，高 = detectionHeight
    public bool DetectPlayer()
    {
        if (Passive) return false;   // 被动态永不感知 → 不追击、不攻击
        Collider2D hit = Physics2D.OverlapBox(transform.position, new Vector2(detectionRange * 2f, detectionHeight), 0f, playerLayer);
        if (hit != null) player = hit.transform;
        return hit != null;
    }

    protected override void DrawDetectionGizmo()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRange * 2f, detectionHeight, 0.1f));   // 横向探测框
        // 不画 attackRange 红框：小怪不用它，真实射程看每招 Min/Max Range（橙色命中盒另由 OnDrawGizmos 画）
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (attacks == null) return;
        int dir = FacingDir;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.9f);
        foreach (var a in attacks)
            if (a != null && a.showGizmo && a.hits != null)
                foreach (var h in a.hits)
                    if (h != null && h.delivery == AttackDelivery.Melee)
                        Gizmos.DrawWireCube(transform.position + new Vector3(h.hitboxOffset.x * dir, h.hitboxOffset.y, 0f),
                                            new Vector3(h.hitboxSize.x, h.hitboxSize.y, 0.1f));
    }
}
