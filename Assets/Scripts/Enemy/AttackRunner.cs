using UnityEngine;

// 统一攻击运行器:执行一招 / 一套连段。小怪与 boss 共用(EnemyBase 持有一个)。
// 流程:段前位移(step.preMove:逼近/后撤/瞬移/跳)→ 出招(挥砍 + step.driver 编排)→ 段后停顿 → 推进连段 → 完。
// 命中:普通挥砍走 Fire(i)(动画事件,立即判);驱动招(挑飞/前冲等)把 Fire(i) 转给 driver 协调。
// 退出:动画事件 AnimFinish 或超时兜底。被打断调 Cancel()。
public class AttackRunner
{
    readonly EnemyBase e;
    public AttackRunner(EnemyBase e) { this.e = e; }

    enum Phase { Idle, PreMove, Swing, Gap } //swing 出招中位移
    Phase phase = Phase.Idle;
    float timer;
    bool  animEnded;

    AttackDriverBase driver;   // 出招时编排(前冲/挑飞,step.driver)
    StepMover        mover;    // 出招前位移(逼近/后撤/瞬移/跳,step.preMove)

    public bool Active => phase != Phase.Idle;

    // 开始执行当前连段 / 单招(e.CurrentStep / e.CurrentAttack 已由 TryPick* 选好)
    public void Begin() => StartStep();

    void StartStep()
    {
        var step = e.CurrentStep;
        mover = step != null ? step.preMove : null;   // 段前位移驱动(可插拔,留空=不动)
        if (mover != null) { mover.Begin(e); phase = Phase.PreMove; return; }
        StartSwing();
    }

    void StartSwing()
    {
        var atk = e.CurrentAttack;
        e.Rb.linearVelocity = new Vector2(0f, e.Rb.linearVelocity.y);
        animEnded = false;
        e.ResetAttackSwings();

        var step = e.CurrentStep;
        // animSpeed / driver 全在 step 上(招只管打击数据)
        if (e.Anim != null) e.Anim.speed = (step != null && step.animSpeed > 0f) ? step.animSpeed : 1f;
        // 攻击统一点亮 isAttacking(摁住攻击态);出招由下面 PlayCurrentAttack 强播
        e.SetOnlyAnimBool("isAttacking");
        e.PlayCurrentAttack();

        // 出招时驱动(前冲/挑飞,step)
        driver = step != null ? step.driver : null;
        if (driver != null) driver.Begin(e, atk);

        // 超时兜底:动画时长(+余量)或固定 1.5s(正常靠 AnimFinish/AnimationTrigger 退出)
        float clip = e.CurrentAttackClipLength();
        timer = clip > 0.01f ? clip + 0.3f : 1.5f;
        phase = Phase.Swing;
    }

    public void Tick()
    {
        switch (phase)
        {
            case Phase.PreMove: TickPreMove(); break;
            case Phase.Swing:   TickSwing();   break;
            case Phase.Gap:     TickGap();     break;
        }
    }

    // ── 段前位移:交给 step.preMove(逼近/后撤/瞬移/跳),到位即出招 ──
    void TickPreMove()
    {
        if (mover == null || mover.Tick(e)) { mover = null; FacePlayer(); StartSwing(); }
    }

    // ── 出招中 ──
    void TickSwing()
    {
        timer -= Time.deltaTime;
        if (driver != null) driver.Tick(e, e.CurrentAttack);   // 前冲/挑飞;普通挥砍无驱动=不位移
        if (animEnded || timer < 0f) EndSwing();
    }

    void EndSwing()
    {
        var atk   = e.CurrentAttack;
        var combo = e.CurrentCombo;
        var step  = e.CurrentStep;
        if (driver != null) { driver.End(e, atk); driver = null; }
        if (e.Anim != null) e.Anim.speed = 1f;
        e.DamageFeedback?.ClearWarning();

        if (e.AdvanceCombo())   // 还有下一段
        {
            float gap = (step != null && step.gap >= 0f) ? step.gap : (combo != null ? combo.stepGap : 0f);
            // gap 里要停住 idle:必须点亮 isIdle(Entry/Exit 控制器靠 bool 撑住状态);
            // 只 PlayIdle 不设 isIdle → idle 因 isIdle==false 立刻退出 → Entry 无 isAttacking 路由 → 回默认 idle 死循环第0帧(冻帧)。
            if (gap > 0f) { phase = Phase.Gap; timer = gap; e.SetOnlyAnimBool("isIdle"); e.PlayIdle(); return; }
            FacePlayer(); StartStep(); return;
        }
        Finish(atk, combo);   // 连段打完 / 单招
    }

    void TickGap()
    {
        timer -= Time.deltaTime;
        e.Rb.linearVelocity = new Vector2(0f, e.Rb.linearVelocity.y);
        if (timer <= 0f) { FacePlayer(); StartStep(); }
    }

    void Finish(EnemyBase.EnemyAttack atk, EnemyBase.EnemyCombo combo)
    {
        float cd = (combo != null && combo.cooldownAfter > 0f) ? combo.cooldownAfter : e.defaultAttackCooldown;
        e.StartAttackCooldown(cd);
        phase = Phase.Idle;
    }

    // ── 动画事件(由 EnemyBase 转来)──
    public void OnAnimEnd() { if (phase == Phase.Swing) animEnded = true; }

    // hits[i]
    public void OnFire(int i)
    {
        if (phase == Phase.Swing && driver != null) { driver.OnFire(e, e.CurrentAttack, i); return; }
        e.DoFireHit(i);
    }

    // 被打断(破韧 / 死亡 / 识破):恢复驱动/位移/动画/连段
    public void Cancel()
    {
        // 段前位移(跳/瞬移)中被打断:让 mover 恢复物理 + 无敌 + 可见,别卡住
        if (mover  != null) { mover.Cancel(e); mover = null; }
        if (driver != null) { driver.End(e, e.CurrentAttack); driver = null; }
        if (e.Anim != null) e.Anim.speed = 1f;
        e.DamageFeedback?.ClearWarning();
        e.ResetCombo();
        phase = Phase.Idle;
    }

    void FacePlayer() { if (e.playerTransform != null) e.SetFacing(e.DirToward(e.playerTransform.position)); }
}
