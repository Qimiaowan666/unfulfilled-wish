using UnityEngine;

public enum AttackDelivery { Melee, Ranged }              // 招的出伤方式：近战命中盒 / 远程发射箭矢
public enum LungeDir       { None, Forward, Backward }    // 出招时移动：不动 / 朝玩家前冲 / 背对后撤
public enum StepMove       { None, Approach, Retreat }    // 连段段前位移：不动 / 朝玩家逼近 / 后撤

// 地面型小怪共享基类：状态机(Idle/Move/Chase/Attack/Stunned/Dead) + 攻击池 + 命中盒 + 统一 Anim.Play。
// AoTengu(最基础，1 招)与 DemonSamurai(多招 + 变身)都继承它；boss 不走这套(自有 BossStateMachine)。
public abstract class GroundEnemy : EnemyBase
{
    // 一招的配置：动画状态名 + 触发距离 + 伤害/韧性 + 命中盒
    // 一「下」命中:自包含。一招(EnemyAttack)挂一串 hits,动画事件 Fire(i) 引用第 i 下。
    [System.Serializable]
    public class HitProfile
    {
        public AttackDelivery delivery = AttackDelivery.Melee;   // 近战命中盒 / 远程发射
        public bool    red = false;                              // 红=不可格挡 + 红视觉;白=可格挡
        public float   damageMultiplier = 1f;                    // 伤害 = 敌人 attack × 此值
        public float   knockback = 5f;
        public float   hitStun   = 0f;
        [Header("近战(delivery=Melee)")]
        public Vector2 hitboxOffset = new Vector2(0.7f, 0f);
        public Vector2 hitboxSize   = new Vector2(1f, 0.8f);
        [Header("远程(delivery=Ranged)")]
        public Arrow   projectilePrefab;
    }

    [System.Serializable]
    public class EnemyAttack
    {
        public string  id = "attack";                       // animator 状态名(= Anim.Play 用)
        public float   weight = 1f;                          // 抽招权重
        public float   minRange = 0f;                        // 可触发的水平距离区间
        public float   maxRange = 1.5f;

        [Header("命中:这招打出的每一下(动画事件 Fire(i) 引用第 i 下)")]
        public HitProfile[] hits = new HitProfile[0];

        [Header("位移")]
        public LungeDir lungeDir   = LungeDir.None;          // 出招时移动：不动/前冲/后撤
        public float    lungeSpeed = 0f;                     // 移动速度(配合 lungeDir)

        [Header("节奏")]
        public float   cooldownOverride = 0f;                // >0：这招专属冷却(覆盖敌人 attackCooldown)
        public bool    showGizmo = true;
    }

    [Header("Attacks (攻击池)")]
    public EnemyAttack[] attacks = { new EnemyAttack() };

    // 连段里的一段:打哪招(引用 attacks[].id) + 出招前位移 + 段后停顿
    [System.Serializable]
    public class ComboStep
    {
        public string   attackId = "attack";              // 引用 attacks[].id
        public StepMove move     = StepMove.None;          // 挥刀前先逼近 / 后撤 / 不动
        [Tooltip("本段打完后的停顿(秒);<0 = 用连段的 stepGap")]
        public float    gap      = -1f;
    }

    // 连段:把 attacks 里的招按顺序串起来。一只怪可配多套,按权重 + 距离 + 血量抽。
    // 不配 combos → 走单招(TryPickAttack),老怪不受影响。某招 weight 设 0 → 只在连段里出、不被单抽。
    [System.Serializable]
    public class EnemyCombo
    {
        public string      name     = "combo";
        public float       weight   = 1f;       // 连段之间的权重
        public float       minRange = 0f;        // 可被选中的水平距离区间
        public float       maxRange = 2f;
        [Tooltip("可被选中的血量区间(0~1 百分比)。配低血段 → 残血才放,不必写阶段状态")]
        public float       minHPPercent = 0f;
        public float       maxHPPercent = 1f;
        [Tooltip("勾上 = 不连续抽到同一套(增加变化)")]
        public bool        noRepeat = false;
        [Tooltip("整套打完的冷却(秒);<=0 = 用敌人默认 attackCooldown")]
        public float       cooldownAfter = 0f;
        [Tooltip("段间默认停顿(秒),0 = 无缝;每段可用自己的 gap 覆盖")]
        public float       stepGap  = 0.08f;
        public ComboStep[] steps    = new ComboStep[0];
    }

    [Header("Combos (连段;留空=只用单招)")]
    public EnemyCombo[] combos;

    // 红色 hit(HitProfile.red=true)统一视觉:红染 + 放大,所有红 hit 长一个样(固定视觉约定);
    // 击退/硬直/伤害这些「数值」归 HitProfile 逐 hit 配。
    public static readonly Color DangerTint  = new Color(1f, 0.45f, 0.3f, 1f);
    public const           float DangerScale = 1.4f;

    [Header("远程通用 (delivery=Ranged 的招用)")]
    public Vector2 firePointOffset = new Vector2(0.7f, 0.2f);   // 出箭点(x 随朝向翻转)
    public float   aimHeightOffset = 0f;                         // 瞄准点相对玩家 pivot 的垂直偏移

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
    public EnemyCombo  CurrentCombo  { get; private set; }   // 非空 = 正在打连段
    public ComboStep   CurrentStep   { get; private set; }   // 当前段(含位移 / 停顿)
    EnemyCombo         lastCombo;                             // 上一套(noRepeat 用)
    int comboStep;

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

    // 动画事件：死亡动画"摔倒落地"那一帧 → 播倒地音(从 Die 挪来，和受击音错开)
    public void AnimDeathFall() => AudioManager.Instance?.PlayEnemyDeath();

    // ── 选招：按距离区间 + 权重抽一个单招 ──
    public bool TryPickAttack(float dist)
    {
        if (attacks == null || attacks.Length == 0) return false;
        var pick = PickAttack(attacks, a => (dist >= a.minRange && dist <= a.maxRange) ? Mathf.Max(0f, a.weight) : 0f);
        if (pick == null) return false;
        ResetCombo();           // 单招:清掉连段状态
        CurrentAttack = pick;
        return true;
    }

    // ── 选连段：按距离 + 血量 + 权重(可排除上一套)抽一套,载入第一段 ──
    public bool TryPickCombo(float dist)
    {
        if (combos == null || combos.Length == 0) return false;
        float hp = maxHP > 0f ? CurrentHP / maxHP : 1f;
        var c = PickAttack(combos, x =>
        {
            if (x == null || x.steps == null || x.steps.Length == 0) return 0f;
            if (dist < x.minRange || dist > x.maxRange)             return 0f;
            if (hp  < x.minHPPercent || hp > x.maxHPPercent)        return 0f;
            if (x.noRepeat && x == lastCombo)                       return 0f;
            return Mathf.Max(0f, x.weight);
        });
        if (c == null) return false;
        CurrentCombo = c;
        lastCombo    = c;
        comboStep    = 0;
        return AdvanceCombo();   // 载入第 0 段 → CurrentStep / CurrentAttack
    }

    // 推进连段到下一段;载入成功 = true,连段打完 / 配错 = false(并清空)
    public bool AdvanceCombo()
    {
        if (CurrentCombo == null || CurrentCombo.steps == null || comboStep >= CurrentCombo.steps.Length)
        {
            ResetCombo();
            return false;
        }
        var step = CurrentCombo.steps[comboStep];
        comboStep++;
        var atk = step != null ? FindAttack(step.attackId) : null;
        if (atk == null) { ResetCombo(); return false; }   // id 配错 → 结束,不卡死
        CurrentStep   = step;
        CurrentAttack = atk;
        return true;
    }

    public void ResetCombo() { CurrentCombo = null; CurrentStep = null; comboStep = 0; }

    EnemyAttack FindAttack(string id)
    {
        if (attacks == null) return null;
        foreach (var a in attacks)
            if (a != null && a.id == id) return a;
        return null;
    }

    // ── 命中：攻击 clip 上的动画事件 Fire(i) 调用(可多帧多次,i=hits 下标) ──
    int attackSwing;
    public void ResetAttackSwings() => attackSwing = 0;
    // 命中事件:打出当前招的第 i 下(读 CurrentAttack.hits[i])。red=true 的下 → 不可格挡 + 红视觉,否则白·可格挡。
    public virtual void Fire(int i)
    {
        var a = CurrentAttack ?? (attacks != null && attacks.Length > 0 ? attacks[0] : null);
        if (a == null || a.hits == null || i < 0 || i >= a.hits.Length || a.hits[i] == null) return;
        var h = a.hits[i];
        float dmg = attack * h.damageMultiplier;
        if (h.delivery == AttackDelivery.Ranged)
        {
            SpawnProjectile(h.projectilePrefab, dmg, h.red, h.knockback, h.hitStun,
                            h.red ? DangerTint : Color.white, h.red ? DangerScale : 1f);
            AudioManager.Instance?.PlayBow(h.red);
        }
        else
        {
            PerformAttack(dmg, h.hitboxOffset, h.hitboxSize, h.red, h.knockback, h.hitStun);
            AudioManager.Instance?.PlayEnemySwing(attackSwing);
            attackSwing++;
        }
    }

    // 红闪预警:成对事件。Warn=开始染红身体(保持)，WarnEnd=结束恢复。红的时长由两事件在时间轴上的位置决定，不用填数字。
    // 放在危招(某下 red=true)出手前预警(玩家看到红 → 准备识破)。攻击态退出会兜底清红，招式被打断也不会卡红。
    public void Warn()    => GetComponent<DamageFeedback>()?.HoldWarning();
    public void WarnEnd() => GetComponent<DamageFeedback>()?.ClearWarning();

    // 远程出箭(朝玩家瞄准带高低差)；Fire 的远程 hit 用
    protected void SpawnProjectile(Arrow prefab, float dmg, bool unblockable, float knockback, float hitStun, Color tint, float scale)
    {
        if (prefab == null) return;
        Vector3 spawn = transform.position + new Vector3(firePointOffset.x * FacingDir, firePointOffset.y, 0f);
        Vector2 aim = Vector2.right * FacingDir;
        if (player != null)
        {
            aim = ((Vector2)player.position + Vector2.up * aimHeightOffset) - (Vector2)spawn;
            if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * FacingDir;
        }
        var arrow = Instantiate(prefab, spawn, Quaternion.identity);
        if (!Mathf.Approximately(scale, 1f)) arrow.transform.localScale *= scale;
        var sr = arrow.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;
        arrow.Launch(this, aim, dmg, unblockable, knockback, hitStun);
    }

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
