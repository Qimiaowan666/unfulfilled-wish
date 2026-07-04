using System.Collections;
using UnityEngine;

public class MinotaurBoss : EnemyBase
{
    // ── State Machine(stateMachine 本体在 EnemyBase,这里只声明各状态)──
    public Boss_IdleState      idleState      { get; private set; }
    public Boss_BattleState    battleState    { get; private set; }
    public Boss_ChaseState     chaseState     { get; private set; }
    public Boss_WaitState      waitState      { get; private set; }
    public Boss_AttackState    attackState    { get; private set; }   // 薄壳,驱动统一运行器跑连段
    public Boss_EnragedState   enragedState   { get; private set; }
    public Boss_StunnedState   stunnedState   { get; private set; }
    public Boss_StaggerState   staggerState   { get; private set; }
    public Boss_DeadState      deadState      { get; private set; }

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

    // ── Runtime ──────────────────────────────────────────────────────
    public bool  IsPhase2     { get; private set; }

    DamageFeedback damageFeedback;
    SpriteRenderer spriteRenderer;
    Color          baseSpriteColor = Color.white;   // 原始基色（怒气染色后复活还原用）

    // 二阶段过渡期间缓存的玩家/血条引用，供过渡被异常打断（boss 禁用/切场景）时复原，避免玩家卡死
    Rigidbody2D       phasePlayerRb;
    PlayerController   phasePlayerPc;
    PlayerHealthBarUI phasePlayerBar;
    BossHealthBarUI   phaseBossBar;

    GameObject rageAura;   // 二阶段常态怒气粒子（跟随 boss，非 parent 避免被 5x 缩放）

    public DamageFeedback DamageFeedback => damageFeedback;

    // ── 统一攻击系统钩子(招/连段/驱动在 EnemyBase + AttackRunner)──────────
    public override float DamageMultiplier => IsPhase2 ? phase2AttackMultiplier : 1f;   // 二阶段加伤
    public override float AttackMoveSpeed  => moveSpeed * (IsPhase2 ? phase2SpeedBonus : 1f);

    // SetOnlyAnimBool 用基类即可（遍历 Anim 的 bool 参数，一次只亮一个；boss 也已统一到单个 isAttacking）

    // combatEnabled / Activate() 已上移到 EnemyBase（所有 boss 通用的登场沉睡/唤醒）

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        damageFeedback = GetComponent<DamageFeedback>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseSpriteColor = spriteRenderer.color;

        stateMachine  = new StateMachine();
        idleState     = new Boss_IdleState(this, stateMachine);
        battleState   = new Boss_BattleState(this, stateMachine);
        chaseState    = new Boss_ChaseState(this, stateMachine);
        waitState     = new Boss_WaitState(this, stateMachine);
        attackState   = new Boss_AttackState(this, stateMachine);
        enragedState  = new Boss_EnragedState(this, stateMachine);
        stunnedState  = new Boss_StunnedState(this, stateMachine);
        staggerState  = new Boss_StaggerState(this, stateMachine);
        deadState     = new Boss_DeadState(this, stateMachine);

        stateMachine.Initialize(idleState);
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
        if (pc  != null) pc.Stun(10f);   // 吼飞=进硬直态 10s(实际由 RestorePhase2State 提前解),击退由 RoarKnockbackPlayer 逐帧驱动
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
        ForceCombo("二阶段开场");
        stateMachine.ChangeState(battleState);
    }

    // 复原二阶段过渡的所有副作用（timeScale / 玩家物理与状态 / 血条 / 无敌 / 标志）。
    // 正常结束与 OnDisable 异常中断都调它，幂等。
    void RestorePhase2State()
    {
        Time.timeScale = 1f;
        if (phasePlayerRb != null) phasePlayerRb.simulated = true;
        if (phasePlayerPc != null && phasePlayerPc.stateMachine != null &&
            phasePlayerPc.stateMachine.currentState == phasePlayerPc.stunnedState)
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

    // 兜底：boss 被禁用（场景卸载 / 死亡收尾）时，若二阶段过渡正进行 → 复原全部副作用
    void OnDisable()
    {
        if (PhaseTransition) RestorePhase2State();
        else PhaseTransition = false;
        StopRageAura();
    }

    // 开战：第一套固定跳劈接大招
    public override void Activate()
    {
        base.Activate();
        ForceCombo("跳劈接大招");   // 开战首套
    }

    protected override void OnDeath()
    {
        StopRageAura();
        stateMachine.ChangeState(deadState);
    }
    protected override void OnRespawn()
    {
        StopAllCoroutines();   // 清掉读档/复活前残留的协程（转阶段染色/击退、攻击指示器跟随等）
        Attack.Cancel();       // 清掉进行中的连段/驱动(恢复无敌·物理·动画速度)
        IsPhase2 = false;
        PhaseTransition = false;
        StopRageAura();
        damageFeedback?.SetBaseColor(baseSpriteColor);   // 还原怒气染色回原始基色
        stateMachine.ChangeState(idleState);
        // 强制切回 idle clip：攻击 clip 都是 Anim.Play 强制进入的，仅靠 SetBool("isIdle") 切不回来
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

    // 处决对 boss 不即死:吃大额伤害,血归 0 才死;并清掉本次硬直窗口防连按 R
    [Tooltip("被处决后的硬直时长(秒),独立于普通破韧硬直(2/3秒)")]
    public float executeStunDuration = 1f;

    public override void OnExecuted(float damage)
    {
        if (CurrentHP <= 0f) return;
        TakeDamage(damage, 0f);
        if (CurrentHP > 0f)
        {
            stunnedState.nextDurationOverride = executeStunDuration;   // 处决后单独的硬直时长
            stateMachine.ChangeState(stunnedState);                    // Exit 顺带 ResetPoise
        }
    }

}
