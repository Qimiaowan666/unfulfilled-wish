using System.Collections;
using UnityEngine;

public class MinotaurBoss : EnemyBase
{
    // ── State Machine ────────────────────────────────────────────────
    public BossStateMachine        stateMachine        { get; private set; }
    public Boss_IdleState          idleState           { get; private set; }
    public Boss_BattleState        battleState         { get; private set; }
    public Boss_ChaseState         chaseState          { get; private set; }
    public Boss_WaitState          waitState           { get; private set; }
    public Boss_NormalAttackState  normalAttackState   { get; private set; }
    public Boss_SpecialAttackState specialAttackState  { get; private set; }
    public Boss_EnragedState       enragedState        { get; private set; }
    public Boss_StunnedState       stunnedState        { get; private set; }
    public Boss_StaggerState       staggerState        { get; private set; }
    public Boss_RepositionState    repositionState     { get; private set; }
    public Boss_JumpSlashState     jumpSlashState      { get; private set; }
    public Boss_Atk3State          atk3State           { get; private set; }
    public Boss_DeadState          deadState           { get; private set; }

    // ── Inspector Config ─────────────────────────────────────────────
    [Header("Boss Movement")]
    public float moveSpeed        = 2.2f;
    public float phase2SpeedBonus = 1.2f;

    [Header("Phase 2")]
    public float phase2HPThreshold      = 0.5f;
    public float phase2AttackMultiplier = 1.5f;

    [Header("Phase 2 过渡演出")]
    [Tooltip("进二阶段：双方定身一拍的时长")]       public float phase2FreezeBeat = 0.6f;
    [Tooltip("进二阶段：吼后蹲守(idle)蓄力时长")] public float phase2RoarHold   = 3.0f;
    [Tooltip("二阶段持久怒气染色（乘到 boss 精灵上）")] public Color phase2RageTint = new Color(1f, 0.42f, 0.4f, 1f);
    [Tooltip("二阶段常态怒气粒子(余烬+红烟+鼻息)整体缩放")] public float rageAuraScale = 1f;
    [Tooltip("吼叫把玩家击退的速度（越大滑得越远）")]  public float phase2RoarKnockback = 20f;
    [Tooltip("吼叫击退的驱动时长（配合速度决定距离）")] public float phase2RoarKnockTime = 0.55f;
    [Tooltip("吼叫顿帧时长（realtime，冲击感）")]      public float phase2RoarHitstop  = 0.12f;

    [Header("Counter Reaction")]
    public float staggerDuration        = 0.6f;   // 被识破后的停顿时长

    [Header("Combo Patterns")]
    public ComboPattern[] comboPatterns;

    [Header("Teleport (闪身)")]
    public float teleportDuration  = 0.35f;   // 瞬移总时长（淡出 + 淡入）
    public float teleportOffset    = 0.8f;    // 现身在玩家身后多远
    public float landingRecovery   = 0.12f;   // 闪身现身后到出招的硬直窗口
    public LayerMask teleportWallLayer;       // 闪身落点墙体检测层（Inspector 设成 Ground；留空则按名字找 "Ground"）
    public float teleportClearance = 0.6f;    // 落点距墙安全距离（≈ boss 碰撞半宽）

    [Header("Jump Slash (二段跳劈)")]
    public float jumpHeight        = 3f;      // 跳跃顶点高度
    public float jumpAirborneTime  = 0.75f;   // 起跳→落地用时（对齐 atk2 第9帧 AnimHitOpen = 砸地瞬间）
    public float jumpSlashStandoff = 2f;      // 落点比玩家靠后多少（留身位，避免砸正中心、精灵重叠）

    [Header("Atk3 (横劈挑飞 + 跳劈)")]
    public float    atk3RiseHeight     = 4f;     // 挑飞升空高度
    public float    atk3LaunchTime     = 0.4f;   // 升空用时（玩家与 boss 共用的上升时长）
    public float    atk3BossJumpDelay  = 0.25f;  // boss 比玩家晚多久起跳（玩家先飞，boss 后追）
    public float    atk3SlamTime       = 0.25f;  // 下劈砸回地面用时
    public float    atk3AnimSpeed      = 0.7f;   // atk3 动画播放速度（<1 变慢）
    public float    atk3HoldDistance   = 2f;     // 挑飞时把玩家固定在 boss 正前方多远（避免重叠）
    public float    atk3SlamMultiplier = 2f;     // 落地伤害倍率

    [Header("Attack Hitboxes")]
    public Hitbox[] hitboxes;

    [Header("Special Attack Indicator")]
    public bool           showAttackIndicator    = true;
    public SpriteRenderer indicatorRenderer;
    public Vector2        indicatorSize          = new Vector2(2f, 1.5f);
    public Vector2        indicatorOffset        = new Vector2(2f, 0f);
    public Color          indicatorWarningColor  = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color          indicatorImpactColor   = new Color(1f, 0.7f, 0.2f, 0.85f);

    // ── Aliases for state classes（语义化别名 → 父类字段）─────────────
    public float normalAttackCooldown   => attackCooldown;
    public float specialWarningDuration => specialAttackWarningDuration;

    // ── Runtime ──────────────────────────────────────────────────────
    public bool  IsPhase2     { get; private set; }

    DamageFeedback damageFeedback;
    Coroutine      indicatorRoutine;
    SpriteRenderer spriteRenderer;
    Color          baseSpriteColor = Color.white;   // 原始基色（怒气染色后复活还原用）

    // 二阶段过渡期间缓存的玩家/血条引用，供过渡被异常打断（boss 禁用/切场景）时复原，避免玩家卡死
    Rigidbody2D       phasePlayerRb;
    PlayerController   phasePlayerPc;
    PlayerHealthBarUI phasePlayerBar;
    BossHealthBarUI   phaseBossBar;

    GameObject rageAura;   // 二阶段常态怒气粒子（跟随 boss，非 parent 避免被 5x 缩放）

    public DamageFeedback DamageFeedback => damageFeedback;

    public enum AttackId { Atk1, Atk2, Special, JumpSlash, Atk3 }
    public enum HitboxKey { Atk1, Atk2, Special, Atk3 }

    public enum MovementType { None, TeleportBehind, TeleportOtherSide, TeleportFront }

    [System.Serializable]
    public class ComboStep
    {
        public AttackId     attack;
        public MovementType preMove = MovementType.None;   // 出招前的位移
    }

    [System.Serializable]
    public class ComboPattern
    {
        public string      patternName;       // 调试用
        public ComboStep[] steps;             // 按顺序：每段 = 位移 + 攻击
        public float       weight = 1f;       // pattern 之间的权重
        public bool        phase2Only = false;
    }

    [System.Serializable]
    public class Hitbox : AttackHitbox
    {
        public HitboxKey key;
    }

    public override AttackHitbox GetHitbox(string id)
    {
        if (hitboxes == null || !System.Enum.TryParse(id, out HitboxKey key)) return null;
        foreach (var hb in hitboxes)
            if (hb != null && hb.key == key) return hb;
        return null;
    }

    // ── SetAnimBool（所有 8 个 bool）──────────────────────────────────
    static readonly string[] boolNames =
    {
        "isIdle", "isMoving", "isAttacking", "isAttacking2",
        "isSpecialAttacking", "isDefending", "isHit", "isDead"
    };

    public override void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var name in boolNames)
            Anim.SetBool(name, name == boolName);
    }

    // combatEnabled / Activate() 已上移到 EnemyBase（所有 boss 通用的登场沉睡/唤醒）

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        damageFeedback = GetComponent<DamageFeedback>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseSpriteColor = spriteRenderer.color;

        if (NeedsDefaultPatterns())
            comboPatterns = DefaultComboPatterns();

        stateMachine        = new BossStateMachine();
        idleState           = new Boss_IdleState(this, stateMachine);
        battleState         = new Boss_BattleState(this, stateMachine);
        chaseState          = new Boss_ChaseState(this, stateMachine);
        waitState           = new Boss_WaitState(this, stateMachine);
        normalAttackState   = new Boss_NormalAttackState(this, stateMachine);
        specialAttackState  = new Boss_SpecialAttackState(this, stateMachine);
        enragedState        = new Boss_EnragedState(this, stateMachine);
        stunnedState        = new Boss_StunnedState(this, stateMachine);
        staggerState        = new Boss_StaggerState(this, stateMachine);
        repositionState     = new Boss_RepositionState(this, stateMachine);
        jumpSlashState      = new Boss_JumpSlashState(this, stateMachine);
        atk3State           = new Boss_Atk3State(this, stateMachine);
        deadState           = new Boss_DeadState(this, stateMachine);

        stateMachine.Initialize(idleState);

        if (indicatorRenderer != null) indicatorRenderer.gameObject.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentHP <= 0f) return;

        if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;

        CheckPhase2();
        UpdateRageAura();
        stateMachine.Update();
    }

    // 二阶段常态怒气粒子：进二阶段时挂上，每帧跟随 boss 中心 + 按朝向翻转(鼻息方向)
    void UpdateRageAura()
    {
        if (IsPhase2 && rageAura == null && combatEnabled)
            rageAura = VfxManager.PlayLoop("Vfx/RageAura", null, FxCenter(), rageAuraScale, null, spriteRenderer);
        if (rageAura != null)
        {
            rageAura.transform.position = FxCenter();
            var s = rageAura.transform.localScale;
            s.x = Mathf.Abs(s.x) * (FacingDir >= 0 ? 1f : -1f);
            rageAura.transform.localScale = s;
        }
    }

    void StopRageAura()
    {
        if (rageAura != null) { VfxManager.StopLoop(rageAura); rageAura = null; }
    }

    void CheckPhase2()
    {
        if (IsPhase2) return;
        if (!combatEnabled) return;   // 沉睡 / 读档未开战时不触发，避免演出误放
        if (CurrentHP / maxHP >= phase2HPThreshold) return;
        if (stateMachine.currentState == deadState || stateMachine.currentState == enragedState) return;

        IsPhase2 = true;
        // 二阶段开放的连段由 ComboPattern.phase2Only 控制，无需在此切换权重
        stateMachine.ChangeState(enragedState);
    }

    // ── 二阶段过渡：双方定身 → 咆哮强化 → 恢复 ───────────────────────
    // 演出期间屏蔽玩家输入（PlayerInput / PauseMenu 接此标志）
    public static bool PhaseTransition { get; private set; }

    public void BeginPhase2Transition() => StartCoroutine(Phase2TransitionRoutine());

    IEnumerator Phase2TransitionRoutine()
    {
        PhaseTransition = true;
        Invincible = true;                 // 强化蓄力期间不可被打断
        Rb.linearVelocity = Vector2.zero;

        // 冻结玩家：清速度 + 回 idle + 停物理，整段定身（蓄力）
        var pgo = GameObject.FindGameObjectWithTag(Tags.Player);
        var pc  = pgo != null ? pgo.GetComponent<PlayerController>() : null;
        var prb = pgo != null ? pgo.GetComponent<Rigidbody2D>() : null;
        if (prb != null) prb.linearVelocity = Vector2.zero;
        if (pc  != null) pc.stateMachine.ChangeState(pc.idleState);
        if (prb != null) prb.simulated = false;

        // 隐藏双方血条（演出期间）
        var playerBar = FindAnyObjectByType<PlayerHealthBarUI>();
        var bossBar   = FindAnyObjectByType<BossHealthBarUI>();
        if (playerBar != null) playerBar.SetHudHidden(true);
        if (bossBar   != null) bossBar.SetHidden(true);

        // 缓存引用，供 OnDisable 异常中断时复原（避免玩家停在 simulated=false / knocked）
        phasePlayerRb = prb; phasePlayerPc = pc; phasePlayerBar = playerBar; phaseBossBar = bossBar;

        // 1) 双方定身（snap）
        yield return new WaitForSeconds(phase2FreezeBeat);

        // 2) 吼开：声音 + 顿帧 + 强震 + 冲击波 + 迸发火花 + 把玩家吼飞(播 rest)（boss 位置/大小不变）
        AudioManager.Instance?.PlayBossRoar();
        Time.timeScale = 0.0001f;                               // 顿帧：世界骤停一瞬（“吼”的冲击感）
        yield return new WaitForSecondsRealtime(phase2RoarHitstop);
        Time.timeScale = 1f;
        CameraShake.Shake(0.4f, 0.28f);
        VfxManager.Play("Vfx/BossRoar", FxCenter(), Quaternion.identity, 1f, null, spriteRenderer);
        ScreenRoarFx.Burst(FxCenter(), 0.85f, 0.9f, Color.black);   // 全屏放射状吼叫爆发（以 boss 为中心，连放 4 波，黑色）
        if (prb != null) prb.simulated = true;
        if (pc  != null) { pc.knockedState.Duration = 10f; pc.stateMachine.ChangeState(pc.knockedState); }
        StartCoroutine(RoarKnockbackPlayer(prb, transform.position, phase2RoarKnockback, phase2RoarKnockTime));

        // 3) 蹲守蓄力：聚拢火花 + 渐强底光 + 充能音（boss 吼完蹲下攒劲，此时还没变红）
        AudioManager.Instance?.PlayBossCharge();
        var chargeVfx = VfxManager.PlayLoop("Vfx/BossCharge", null, FxCenter(), 1f, null, spriteRenderer);
        yield return new WaitForSeconds(phase2RoarHold);
        VfxManager.StopLoop(chargeVfx);

        // 3.5) 爆开：蓄满能量炸开（小顿帧 + 强震 + 释放冲击波 + 更大一波迸发火花；boss 自身不缩放/不位移）
        float explodeLen = AudioManager.Instance?.PlayBossExplode() ?? 0f;   // 先放爆炸声
        Time.timeScale = 0.0001f;
        yield return new WaitForSecondsRealtime(0.09f);
        Time.timeScale = 1f;
        CameraShake.Shake(1f, 0.35f);
        SpawnExplosion();
        StartCoroutine(RageTintFadeIn(0.6f));   // 染红（在等爆炸声期间淡入）
        yield return new WaitForSeconds(Mathf.Max(explodeLen * 0.5f, 0.6f));   // 爆炸声主体过去就接吼（不等长尾）

        // 3.6) 爆炸声放完 → 强化吼，吼完才解冻双方；同时切到二阶段 BGM（boss2）
        AudioManager.Instance?.PlayBossPhase2BGM();
        float roarLen = AudioManager.Instance?.PlayBossRageRoar() ?? 0f;
        yield return new WaitForSeconds(roarLen);

        // 5) 恢复 + 血条 + 玩家回神，带强化继续战斗；二阶段开场强制一套"二阶段开场"(闪到面前→挑飞接大招)
        RestorePhase2State();
        forcedPattern = "二阶段开场";
        stateMachine.ChangeState(battleState);
    }

    // 复原二阶段过渡的所有副作用（timeScale / 玩家物理与状态 / 血条 / 无敌 / 标志）。
    // 正常结束与 OnDisable 异常中断都调它，幂等。
    void RestorePhase2State()
    {
        Time.timeScale = 1f;
        if (phasePlayerRb != null) phasePlayerRb.simulated = true;
        if (phasePlayerPc != null && phasePlayerPc.stateMachine != null &&
            phasePlayerPc.stateMachine.currentState == phasePlayerPc.knockedState)
            phasePlayerPc.stateMachine.ChangeState(phasePlayerPc.GroundedOrFall);
        if (phasePlayerBar != null) phasePlayerBar.SetHudHidden(false);
        if (phaseBossBar   != null) phaseBossBar.SetHidden(false);
        Invincible      = false;
        PhaseTransition = false;
    }

    // 红色怒气染色淡入（持久，受击闪白后也保持）；位置/大小不变
    IEnumerator RageTintFadeIn(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            damageFeedback?.SetBaseColor(Color.Lerp(baseSpriteColor, phase2RageTint, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        damageFeedback?.SetBaseColor(phase2RageTint);
    }

    // 把玩家从 boss 朝外吼飞：初速 speed，dur 内线性衰减到 0（距离≈speed*dur/2，比瞬时击退远且平滑）
    IEnumerator RoarKnockbackPlayer(Rigidbody2D prb, Vector3 from, float speed, float dur)
    {
        if (prb == null) yield break;
        float dir = Mathf.Sign(prb.transform.position.x - from.x);
        if (Mathf.Approximately(dir, 0f)) dir = 1f;
        float t = 0f;
        while (t < dur && prb != null && prb.simulated)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            prb.linearVelocity = new Vector2(dir * speed * k, prb.linearVelocity.y);
            yield return null;
        }
        if (prb != null) prb.linearVelocity = new Vector2(0f, prb.linearVelocity.y);
    }

    // ── VFX 中心点（爆炸/吼叫/蓄力预制都定位到 boss 包围盒中心）─────────
    Vector3 FxCenter() => spriteRenderer != null ? spriteRenderer.bounds.center : transform.position;

    // 蓄满爆开 → 统一走 VfxManager 粒子预制（闪光 + 扩散环 + 碎屑飞溅都在 BossExplosion 预制里）
    void SpawnExplosion()
    {
        VfxManager.Play("Vfx/BossExplosion", FxCenter(), Quaternion.identity, 1f, null, spriteRenderer);
    }

    // 兜底：boss 被禁用（场景卸载 / 死亡收尾）时，若二阶段过渡正进行 → 复原全部副作用，避免玩家停在 simulated=false/knocked、新场景卡慢放
    void OnDisable()
    {
        if (PhaseTransition) RestorePhase2State();
        else PhaseTransition = false;
        StopRageAura();
    }

    // 开战：第一套固定跳劈接大招、清掉上次连段记录
    public override void Activate()
    {
        base.Activate();
        forcedPattern = "跳劈接大招";   // 开战首套
        lastPattern   = null;
    }

    protected override void OnDeath()
    {
        StopRageAura();
        stateMachine.ChangeState(deadState);
    }
    protected override void OnRespawn()
    {
        StopAllCoroutines();   // 清掉读档/复活前残留的协程（转阶段染色/击退、攻击指示器跟随等），否则会把刚复位的状态又改回去
        if (indicatorRenderer != null) indicatorRenderer.gameObject.SetActive(false);
        indicatorRoutine = null;
        IsPhase2 = false;
        PhaseTransition = false;   // 复活清掉过渡标志（localScale 由 EnemyBase.Respawn 已重置）
        StopRageAura();
        damageFeedback?.SetBaseColor(baseSpriteColor);   // 还原怒气染色回原始基色
        stateMachine.ChangeState(idleState);
        // 强制切回 idle clip：atk3 无回 idle 过渡、其余攻击 clip 都是 Anim.Play 强制进入的，
        // 仅靠 ChangeState 的 SetBool("isIdle") 切不回来 → 读档会卡在读档前那帧攻击动画
        Anim?.Play("idle", 0, 0f);
    }

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(stunnedState);
    }

    public override void OnCountered()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(staggerState);
    }

    // ── Animation Event Entry Points（动画事件直接调这里）─────────────
    public void AnimHitOpen()  => (stateMachine.currentState as BossBaseState)?.OnHitWindowOpen();
    public void AnimHitClose() => (stateMachine.currentState as BossBaseState)?.OnHitWindowClose();
    public void AnimFinish()   => (stateMachine.currentState as BossBaseState)?.OnAnimationFinished();
    public void AnimBodyFall() => AudioManager.Instance?.PlayBossLand();   // 死亡动画倒地帧 → 砸地声

    // ── Combo Runtime ────────────────────────────────────────────────
    ComboPattern currentPattern;
    int          comboIndex;
    string       forcedPattern;       // 非空=下一套强制用此名连段(开战首套/二阶段开场)；首段不缩距时会等贴近再必出
    ComboPattern lastPattern;         // 上一套连段；下次排除，避免连续重复同一套

    // 攻击状态打完一段后调：返回当前连段的下一段；连段结束则刷新冷却 + 回 battle 等间隔
    public ComboStep AdvanceCombo()
    {
        if (currentPattern != null && comboIndex < currentPattern.steps.Length)
        {
            var step = currentPattern.steps[comboIndex];
            comboIndex++;
            if (IsAttackAvailable(step.attack)) return step;
        }
        EndCombo();   // 连段打完（或下一段不可用）→ 刷新冷却，回 battle 间隔
        return null;
    }

    // BattleState：玩家在攻击范围外但够近 → 挑一套"首段能缩距"的连段(跳劈/闪身)主动逼近
    public ComboStep StartApproachCombo()
    {
        if (forcedPattern != null) { var f = ForcedFirst(); if (f != null) return f; }
        currentPattern = PickAttack(comboPatterns, p =>
        {
            if (p?.steps == null || p.steps.Length == 0) return 0f;
            if (p.phase2Only && !IsPhase2) return 0f;
            if (p == lastPattern) return 0f;                  // 不连续重复上一套
            if (!StepClosesDistance(p.steps[0])) return 0f;   // 只挑首段能缩距的
            return p.weight;
        });
        if (currentPattern == null) return null;
        comboIndex  = 1;
        lastPattern = currentPattern;
        Debug.Log($"[Boss] 突进连段 [{currentPattern.patternName}]");
        return currentPattern.steps[0];
    }

    // 首段能否缩距：跳劈(跳到玩家X) 或 带瞬移前摇
    static bool StepClosesDistance(ComboStep s) =>
        s != null && (s.attack == AttackId.JumpSlash || s.preMove != MovementType.None);

    // BattleState 冷却好后调：挑一套新连段，返回第一段
    public ComboStep StartNewCombo()
    {
        if (forcedPattern != null) { var f = ForcedFirst(); if (f != null) return f; }
        currentPattern = PickAttack(comboPatterns, p =>
        {
            if (p?.steps == null || p.steps.Length == 0) return 0f;
            if (p.phase2Only && !IsPhase2) return 0f;
            if (p == lastPattern) return 0f;                  // 不连续重复上一套
            foreach (var s in p.steps)
                if (!IsAttackAvailable(s.attack)) return 0f;
            return p.weight;
        });

        if (currentPattern == null) return null;

        comboIndex  = 1;
        lastPattern = currentPattern;
        Debug.Log($"[Boss] 连段 [{currentPattern.patternName}] · {currentPattern.steps.Length} 段");
        return currentPattern.steps[0];
    }

    // 强制下一套连段 = forcedPattern(找不到则返回 null 回退正常抽签)。
    // 强制的连段首段都是缩距招(JumpSlash / 闪身)，远近都能直接打出。
    ComboStep ForcedFirst()
    {
        if (forcedPattern == null) return null;
        ComboPattern p = null;
        if (comboPatterns != null)
            foreach (var c in comboPatterns)
                if (c != null && c.patternName == forcedPattern) { p = c; break; }
        forcedPattern = null;
        if (p == null || p.steps == null || p.steps.Length == 0) return null;
        currentPattern = p;
        comboIndex     = 1;
        lastPattern    = p;
        Debug.Log($"[Boss] 强制连段 → {p.patternName}");
        return p.steps[0];
    }

    void EndCombo()
    {
        ResetCombo();
        StartAttackCooldown();   // 连段结束 → 刷新冷却，保证下一套连段前有间隔
    }

    // 执行一段：有位移先走 RepositionState，无位移直接出招
    public void ExecuteStep(ComboStep step)
    {
        if (step.preMove != MovementType.None)
        {
            Vector2 target = GetMovementTarget(step.preMove);
            repositionState.Configure(step.preMove, target, step.attack);
            stateMachine.ChangeState(repositionState);
        }
        else
        {
            RefreshFacingToPlayer();
            EnterAttack(step.attack);
        }
    }

    public void ResetCombo()
    {
        currentPattern = null;
        comboIndex     = 0;
    }

    public void RefreshFacingToPlayer()
    {
        if (player == null) return;
        FaceToward(player.position);
    }

    bool IsAttackAvailable(AttackId id)
    {
        switch (id)
        {
            case AttackId.Atk1:      return true;
            case AttackId.Atk2:      return true;
            case AttackId.Special:   return true;   // 统一冷却：大招不再单独冷却
            case AttackId.JumpSlash: return true;
            case AttackId.Atk3:      return true;
        }
        return false;
    }

    // ── Reposition helpers（被 Boss_RepositionState 调用）──────────────
    public Vector2 GetMovementTarget(MovementType move)
    {
        if (player == null) return transform.position;
        float bossY = transform.position.y;

        switch (move)
        {
            case MovementType.TeleportBehind:
            {
                // 玩家面朝方向的反向 → 玩家身后
                int playerFacing = 1;
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) playerFacing = pc.FacingDir;
                return ClampTeleportTarget(new Vector2(player.position.x - playerFacing * teleportOffset, bossY));
            }
            case MovementType.TeleportOtherSide:
            {
                // 翻到玩家的另一侧（相对 boss 当前所在侧）
                float side = Mathf.Sign(transform.position.x - player.position.x);
                if (side == 0f) side = 1f;
                return ClampTeleportTarget(new Vector2(player.position.x - side * teleportOffset, bossY));
            }
            case MovementType.TeleportFront:
            {
                // 玩家面朝方向 → 玩家正前方（瞬移到玩家面前）
                int pf = 1;
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pf = pc.FacingDir;
                return ClampTeleportTarget(new Vector2(player.position.x + pf * teleportOffset, bossY));
            }
            default:
                return transform.position;
        }
    }

    // 把闪身落点限制在墙内侧：从玩家位置朝落点探墙，挡住就翻另一侧 / 贴墙前，避免 boss 被传送进墙。
    // 关键：安全距离用 boss 碰撞体的实际半宽（运行时读取），boss 越大留得越远，半身才不会插进墙。
    Vector2 ClampTeleportTarget(Vector2 desired)
    {
        if (player == null) return desired;

        LayerMask mask = teleportWallLayer.value != 0 ? teleportWallLayer : LayerMask.GetMask(Layers.Ground);
        if (mask.value == 0) return desired;   // 没有可用的墙层 → 不 clamp（安全降级）

        var col = GetComponent<Collider2D>();
        float halfW = (col != null ? col.bounds.extents.x : 0.5f) + teleportClearance;  // boss 半身宽 + 额外余量

        // 射线在 boss 中心高度水平探（避开脚下地面，只探竖直墙面）
        Vector2 origin = new Vector2(player.position.x, desired.y);
        float dx = desired.x - origin.x;
        float dist = Mathf.Abs(dx);
        if (dist < 0.01f) return desired;
        float sign = Mathf.Sign(dx);

        // 探测距离 = 位移距离 + 半身宽：覆盖 boss 朝向那一侧的整个身体宽度
        float probe = dist + halfW;

        // 目标侧（玩家身后）通畅 → 直接用
        if (!Physics2D.Raycast(origin, new Vector2(sign, 0f), probe, mask))
            return desired;

        // 目标侧被墙挡 → 翻到玩家另一侧（若那侧通畅）
        if (!Physics2D.Raycast(origin, new Vector2(-sign, 0f), probe, mask))
            return new Vector2(origin.x - sign * teleportOffset, desired.y);

        // 两侧都有墙（窄道）→ 落在目标侧墙前，留出整整一个半身宽，确保不插墙
        var hit = Physics2D.Raycast(origin, new Vector2(sign, 0f), probe, mask);
        return new Vector2(hit.point.x - sign * halfW, desired.y);
    }

    public void SetInvincible(bool on) => Invincible = on;

    public void SetSpriteVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }

    public void SetSpriteAlpha(float a)
    {
        if (spriteRenderer == null) return;
        var c = spriteRenderer.color;
        c.a = Mathf.Clamp01(a);
        spriteRenderer.color = c;
    }

    // 空数组、或所有 pattern 的 steps 都为空（旧 sequence 数据迁移失效）→ 用默认值
    bool NeedsDefaultPatterns()
    {
        if (comboPatterns == null || comboPatterns.Length == 0) return true;
        foreach (var p in comboPatterns)
            if (p != null && p.steps != null && p.steps.Length > 0) return false;
        return true;
    }

    // Inspector 数据结构变更后的兜底默认值
    ComboPattern[] DefaultComboPatterns()
    {
        ComboStep S(AttackId a, MovementType m = MovementType.None) =>
            new ComboStep { attack = a, preMove = m };

        return new ComboPattern[]
        {
            new ComboPattern { patternName = "单段普攻", weight = 5f, steps = new[] { S(AttackId.Atk1) } },
            new ComboPattern { patternName = "大招独立", weight = 3f, steps = new[] { S(AttackId.Special) } },
            new ComboPattern { patternName = "二段跳劈", weight = 3f, steps = new[] { S(AttackId.JumpSlash) } },
            new ComboPattern { patternName = "跳劈接大招", weight = 2f, steps = new[] { S(AttackId.JumpSlash), S(AttackId.Special) } },
            new ComboPattern { patternName = "挑飞接大招", weight = 2f, steps = new[] { S(AttackId.Atk3), S(AttackId.Special) } },
            new ComboPattern { patternName = "闪身斩",   weight = 3f, steps = new[] { S(AttackId.Atk1), S(AttackId.Atk1, MovementType.TeleportOtherSide), S(AttackId.Atk1, MovementType.TeleportOtherSide) } },
            new ComboPattern { patternName = "三段全连", weight = 2f, phase2Only = true,
                               steps = new[] { S(AttackId.Atk1), S(AttackId.Atk2), S(AttackId.Special) } },
            new ComboPattern { patternName = "瞬移大招", weight = 2f, phase2Only = true,
                               steps = new[] { S(AttackId.Atk1), S(AttackId.Special, MovementType.TeleportBehind) } },
        };
    }

    // 路由：根据选中的 AttackId 切到对应 state
    public void EnterAttack(AttackId id)
    {
        switch (id)
        {
            case AttackId.Atk1:
                normalAttackState.Configure(useAtk2: false);
                stateMachine.ChangeState(normalAttackState);
                break;
            case AttackId.Atk2:
                normalAttackState.Configure(useAtk2: true);
                stateMachine.ChangeState(normalAttackState);
                break;
            case AttackId.Special:
                stateMachine.ChangeState(specialAttackState);
                break;
            case AttackId.JumpSlash:
                stateMachine.ChangeState(jumpSlashState);
                break;
            case AttackId.Atk3:
                stateMachine.ChangeState(atk3State);
                break;
        }
    }

    // ── Attack Indicator ─────────────────────────────────────────────
    public void ShowIndicator(float duration)
    {
        if (!showAttackIndicator || indicatorRenderer == null) return;
        if (indicatorRoutine != null) StopCoroutine(indicatorRoutine);
        indicatorRoutine = StartCoroutine(IndicatorRoutine(duration));
    }

    IEnumerator IndicatorRoutine(float duration)
    {
        indicatorRenderer.gameObject.SetActive(true);
        indicatorRenderer.transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 worldOffset = new Vector3(
                indicatorOffset.x * FacingDir,
                indicatorOffset.y,
                0f
            );
            indicatorRenderer.transform.position = transform.position + worldOffset;

            float t = elapsed / duration;
            indicatorRenderer.color = Color.Lerp(indicatorWarningColor, indicatorImpactColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        indicatorRenderer.gameObject.SetActive(false);
        indicatorRoutine = null;
    }

    // ── Gizmos ───────────────────────────────────────────────────────
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();   // 父类画 detection / attack range

        // 每个攻击的 hitbox（按 showGizmo flag）
        if (hitboxes != null)
            foreach (var hb in hitboxes) DrawHitboxGizmo(hb);

        if (showAttackIndicator)
        {
            int dir = FacingDir;
            Vector3 center = transform.position + new Vector3(indicatorOffset.x * dir, indicatorOffset.y, 0f);
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawCube(center, new Vector3(indicatorSize.x, indicatorSize.y, 0.1f));
            Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
            Gizmos.DrawWireCube(center, new Vector3(indicatorSize.x, indicatorSize.y, 0.1f));
        }
    }
}
