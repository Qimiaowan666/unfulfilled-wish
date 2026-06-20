using UnityEngine;

// 统一攻击运行器:执行一招 / 一套连段。小怪与 boss 共用(EnemyBase 持有一个)。
// 流程：(段前位移 Approach/Retreat/Teleport) → 出招(挥砍 or 驱动) → 段后停顿 → 推进连段 → 完。
// 命中：普通挥砍走 Fire(i)(立即) 或 AnimHitOpen/Close 窗口(每帧判);驱动招把命中事件转给 driver。
// 退出：动画事件 AnimationTrigger / AnimFinish 或超时。被打断调 Cancel()。
public class AttackRunner
{
    readonly EnemyBase e;
    public AttackRunner(EnemyBase e) { this.e = e; }

    enum Phase { Idle, Move, Teleport, Jump, Swing, Gap }
    Phase phase = Phase.Idle;
    float timer;
    bool  animEnded;

    // 挥砍命中窗口(boss 风格:无 driver 时用 hits[windowIndex])
    bool  inSwingWindow;
    bool  hitThisWindow;
    int   windowIndex;

    AttackDriverBase driver;

    // 瞬移子状态
    float tpElapsed; bool tpLanded; Vector2 tpTarget; RigidbodyType2D tpBody;
    // 跳跃子状态
    float jElapsed; Vector2 jStart, jApex, jGround; RigidbodyType2D jBody;

    const float MaxMoveTime = 0.8f;

    public bool Active => phase != Phase.Idle;

    // 开始执行当前连段 / 单招(e.CurrentStep / e.CurrentAttack 已由 TryPick* 选好)
    public void Begin() => StartStep();

    void StartStep()
    {
        var step = e.CurrentStep;
        if (step != null && IsTeleport(step.move)) { StartTeleport(step.move); return; }
        if (step != null && step.move == StepMove.Jump) { StartJump(); return; }
        if (step != null && (step.move == StepMove.Approach || step.move == StepMove.Retreat) && e.player != null)
        {
            phase = Phase.Move; timer = MaxMoveTime; e.PlayMove(); return;
        }
        StartSwing();
    }

    static bool IsTeleport(StepMove m) =>
        m == StepMove.TeleportBehind || m == StepMove.TeleportFront || m == StepMove.TeleportOtherSide;

    void StartSwing()
    {
        var atk = e.CurrentAttack;
        e.Rb.linearVelocity = new Vector2(0f, e.Rb.linearVelocity.y);
        animEnded = false; inSwingWindow = false; hitThisWindow = false; windowIndex = 0;
        e.ResetAttackSwings();

        if (e.Anim != null) e.Anim.speed = (atk != null && atk.animSpeed > 0f) ? atk.animSpeed : 1f;
        // boss:每招设自己的 animator bool(isAttacking/2/3/isSpecialAttacking),保持对应攻击态;小怪 animBool 空 → 跳过,走扁平 Anim.Play
        if (atk != null && !string.IsNullOrEmpty(atk.animBool)) e.SetAnimBool(atk.animBool);
        e.PlayCurrentAttack();

        // 预警
        if (atk != null && atk.telegraph != AttackTelegraph.None)
        {
            e.GetComponent<DamageFeedback>()?.FlashWarning(atk.telegraphDuration);
            if (atk.telegraph == AttackTelegraph.Indicator) e.ShowAttackIndicator(atk.telegraphDuration);
        }

        // 驱动(编排招):atk3 等招自带的 driver
        driver = atk != null ? atk.driver : null;
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
            case Phase.Move:     TickMove();     break;
            case Phase.Teleport: TickTeleport(); break;
            case Phase.Jump:     TickJump();     break;
            case Phase.Swing:    TickSwing();    break;
            case Phase.Gap:      TickGap();      break;
        }
    }

    // ── 段前逼近 / 后撤 ──
    void TickMove()
    {
        timer -= Time.deltaTime;
        if (e.player == null || e.CurrentStep == null) { FacePlayer(); StartSwing(); return; }
        float dir  = e.DirToward(e.player.position);
        float dist = e.GetHorizontalDistToPlayer();
        bool  done = timer <= 0f; float vx = 0f;

        if (e.CurrentStep.move == StepMove.Approach)
        {
            float reach = e.CurrentAttack != null ? e.CurrentAttack.maxRange : 1f;
            if (dist <= reach || e.LedgeAhead(dir)) done = true; else vx = dir * e.AttackMoveSpeed;
        }
        else // Retreat
        {
            float back = -dir;
            if (e.LedgeAhead(back)) done = true; else vx = back * e.AttackMoveSpeed;
        }
        e.Rb.linearVelocity = new Vector2(done ? 0f : vx, e.Rb.linearVelocity.y);
        if (done) { FacePlayer(); StartSwing(); }
    }

    // ── 瞬移(由 Boss_RepositionState 移植)──
    void StartTeleport(StepMove move)
    {
        tpTarget  = e.GetTeleportTarget(move);
        tpElapsed = 0f; tpLanded = false;
        AudioManager.Instance?.PlayBossTeleport();
        e.Invincible = true;
        tpBody = e.Rb.bodyType;
        e.Rb.bodyType = RigidbodyType2D.Kinematic;
        e.Rb.linearVelocity = Vector2.zero;
        phase = Phase.Teleport;
    }
    void TickTeleport()
    {
        tpElapsed += Time.deltaTime;
        float dur = e.teleportDuration;
        if (!tpLanded)
        {
            float t = dur > 0f ? Mathf.Clamp01(tpElapsed / dur) : 1f;
            if (t < 0.5f) e.SetSpriteAlpha(1f - t / 0.5f);
            else { e.transform.position = tpTarget; e.SetSpriteAlpha((t - 0.5f) / 0.5f); }
            if (t >= 1f)
            {
                tpLanded = true;
                e.transform.position = tpTarget; e.SetSpriteVisible(true); e.SetSpriteAlpha(1f);
                e.Invincible = false;
                if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = tpBody;
                FacePlayer();
            }
            return;
        }
        if (tpElapsed >= dur + e.teleportLanding) { FacePlayer(); StartSwing(); }
    }

    // ── 跳劈接近(StepMove.Jump):抛物线跳到玩家身位,落地接出招;飞行中无敌。──
    void StartJump()
    {
        jStart = e.transform.position;
        float groundY = jStart.y;
        float pX  = e.player != null ? e.player.position.x : jStart.x;
        float dir = Mathf.Sign(pX - jStart.x); if (dir == 0f) dir = e.FacingDir;
        float targetX = pX - dir * e.jumpMoveStandoff;   // 落点比玩家靠后一个身位
        jGround = new Vector2(targetX, groundY);
        jApex   = new Vector2(targetX, groundY + e.jumpMoveHeight);
        jElapsed = 0f;
        e.Invincible = true;
        jBody = e.Rb.bodyType;
        e.Rb.bodyType = RigidbodyType2D.Kinematic;
        e.Rb.linearVelocity = Vector2.zero;
        phase = Phase.Jump;
    }
    void TickJump()
    {
        jElapsed += Time.deltaTime;
        float air  = e.jumpMoveTime;
        float rise = air * 0.55f;   // 上升前 55%,下落后 45%
        if (jElapsed < air)
        {
            if (jElapsed <= rise)
            {
                float a = rise > 0f ? jElapsed / rise : 1f;
                a = 1f - (1f - a) * (1f - a);                 // ease-out 上升
                e.transform.position = Vector2.Lerp(jStart, jApex, a);
            }
            else
            {
                float d = (jElapsed - rise) / (air - rise);
                d *= d;                                       // ease-in 下落
                e.transform.position = Vector2.Lerp(jApex, jGround, d);
            }
            return;
        }
        // 落地 → 解无敌 / 恢复物理 → 接出招
        e.transform.position = jGround;
        e.Invincible = false;
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = jBody;
        FacePlayer();
        StartSwing();
    }

    // ── 出招中 ──
    void TickSwing()
    {
        timer -= Time.deltaTime;
        var atk = e.CurrentAttack;
        if (driver != null) driver.Tick(e, atk);
        else
        {
            if (inSwingWindow) SwingWindowHit();   // boss 风格窗口:每帧判一次
            ApplyLunge(atk);
        }
        if (animEnded || timer < 0f) EndSwing();
    }

    void ApplyLunge(EnemyBase.EnemyAttack atk)
    {
        if (atk == null || atk.lungeDir == LungeDir.None || atk.lungeSpeed <= 0f) return;
        float vx = 0f;
        if (atk.lungeDir == LungeDir.Forward)
            vx = e.GetHorizontalDistToPlayer() > 1.5f ? e.FacingDir * atk.lungeSpeed : 0f;
        else { float d = -e.FacingDir; vx = e.LedgeAhead(d) ? 0f : d * atk.lungeSpeed; }
        e.Rb.linearVelocity = new Vector2(vx, e.Rb.linearVelocity.y);
    }

    void EndSwing()
    {
        var atk   = e.CurrentAttack;
        var combo = e.CurrentCombo;
        var step  = e.CurrentStep;
        if (driver != null) { driver.End(e, atk); driver = null; }
        if (e.Anim != null) e.Anim.speed = 1f;
        e.GetComponent<DamageFeedback>()?.ClearWarning();

        if (e.AdvanceCombo())   // 还有下一段
        {
            float gap = (step != null && step.gap >= 0f) ? step.gap : (combo != null ? combo.stepGap : 0f);
            if (gap > 0f) { phase = Phase.Gap; timer = gap; e.PlayIdle(); return; }
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
        float cd = (combo != null && combo.cooldownAfter > 0f) ? combo.cooldownAfter
                 : (atk != null && atk.cooldownOverride > 0f ? atk.cooldownOverride : e.attackCooldown);
        e.StartAttackCooldown(cd);
        phase = Phase.Idle;
    }

    // ── 动画事件(由 EnemyBase 转来)──
    public void OnAnimEnd() { if (phase == Phase.Swing) animEnded = true; }

    public void OnHitOpen()
    {
        if (phase != Phase.Swing) return;
        if (driver != null) { driver.OnHitOpen(e, e.CurrentAttack); return; }
        inSwingWindow = true; hitThisWindow = false; SwingWindowHit();
    }
    public void OnHitClose()
    {
        if (phase != Phase.Swing) return;
        if (driver != null) { driver.OnHitClose(e, e.CurrentAttack); return; }
        SwingWindowHit(); inSwingWindow = false; windowIndex++;
    }

    // boss 风格挥砍命中:用 hits[windowIndex]
    void SwingWindowHit()
    {
        if (hitThisWindow) return;
        var atk = e.CurrentAttack;
        if (atk == null || atk.hits == null || windowIndex >= atk.hits.Length || atk.hits[windowIndex] == null) return;
        var h = atk.hits[windowIndex];
        float dmg = e.attack * e.DamageMultiplier * h.damageMultiplier;
        if (e.PerformAttack(dmg, h.hitboxOffset, h.hitboxSize, h.red, h.knockback, h.hitStun)) hitThisWindow = true;
    }

    // 被打断(破韧 / 死亡 / 识破):恢复驱动/动画/连段
    public void Cancel()
    {
        // 在跳 / 瞬移飞行中被打断:恢复物理 + 无敌 + 可见,别卡住
        if (phase == Phase.Jump)     { e.Invincible = false; if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = jBody; }
        if (phase == Phase.Teleport) { e.Invincible = false; if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = tpBody; e.SetSpriteVisible(true); e.SetSpriteAlpha(1f); }
        if (driver != null) { driver.End(e, e.CurrentAttack); driver = null; }
        if (e.Anim != null) e.Anim.speed = 1f;
        e.GetComponent<DamageFeedback>()?.ClearWarning();
        e.ResetCombo();
        phase = Phase.Idle;
    }

    void FacePlayer() { if (e.player != null) e.SetFacing(e.DirToward(e.player.position)); }
}
