using UnityEngine;

public class Enemy_AttackState : EnemyBaseState
{
    const float MaxMoveTime = 0.8f;   // 段前位移最长时间(防卡)

    bool  inGap;
    float gapTimer;
    bool  moving;
    float moveTimer;

    public Enemy_AttackState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        inGap  = false;
        moving = false;
        StartStep();
    }

    // 起一段:本段有 move 先位移,否则直接挥刀
    void StartStep()
    {
        var step = enemy.CurrentStep;
        if (step != null && step.move != StepMove.None
            && !enemy.Stationary && !enemy.Passive && enemy.player != null)
        {
            moving    = true;
            moveTimer = MaxMoveTime;
            enemy.PlayMove();
            return;
        }
        BeginSwing();
    }

    // 起一刀(招已由 TryPick* / AdvanceCombo 选好)
    void BeginSwing()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        // 兜底超时：跟随当前招动画时长(+余量)，避免长招被固定 1.5s 提前切断。正常仍由 AnimationTrigger 退出。
        float clipLen = enemy.CurrentAttackClipLength();
        stateTimer    = clipLen > 0.01f ? clipLen + 0.3f : 1.5f;
        triggerCalled = false;
        enemy.ResetAttackSwings();
        enemy.PlayCurrentAttack();   // 播当前选中的招(含变身后缀)
    }

    // 招式无论怎么结束(正常 / 被韧性破·死亡打断)都清红闪 + 结束连段，避免卡红或下次接着上次连段
    public override void Exit()
    {
        base.Exit();
        enemy.ResetCombo();
        enemy.GetComponent<DamageFeedback>()?.ClearWarning();
    }

    public override void Update()
    {
        base.Update();

        // ① 段前位移:朝玩家逼近 / 后撤,到位或超时就挥刀
        if (moving)
        {
            if (enemy.player == null || enemy.CurrentStep == null) { moving = false; BeginSwing(); return; }
            moveTimer -= Time.deltaTime;
            float dir  = enemy.DirToward(enemy.player.position);
            float dist = enemy.GetHorizontalDistToPlayer();
            bool  done = moveTimer <= 0f;
            float vx   = 0f;

            if (enemy.CurrentStep.move == StepMove.Approach)
            {
                float reach = enemy.CurrentAttack != null ? enemy.CurrentAttack.maxRange : enemy.preferredCombatDistance;
                if (dist <= reach || enemy.LedgeAhead(dir)) done = true;
                else vx = dir * enemy.battleMoveSpeed;
            }
            else // Retreat：背对玩家后撤,边缘检测防掉平台
            {
                float back = -dir;
                if (enemy.LedgeAhead(back)) done = true;
                else vx = back * enemy.battleMoveSpeed;
            }

            rb.linearVelocity = new Vector2(done ? 0f : vx, rb.linearVelocity.y);
            if (done) { moving = false; FacePlayer(); BeginSwing(); }
            return;
        }

        // ② 段间停顿:站定等 gapTimer,到点起下一段
        if (inGap)
        {
            gapTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (gapTimer <= 0f) { inGap = false; FacePlayer(); StartStep(); }
            return;
        }

        // ③ 出招位移(招自带的 lunge,挥刀“中”;和 step.move 的挥刀“前”互补)
        var atk = enemy.CurrentAttack;
        if (atk != null && atk.lungeDir != LungeDir.None && atk.lungeSpeed > 0f)
        {
            float vx = 0f;
            if (atk.lungeDir == LungeDir.Forward)
                vx = enemy.GetHorizontalDistToPlayer() > 1.5f ? enemy.FacingDir * atk.lungeSpeed : 0f;
            else
            {
                float d = -enemy.FacingDir;
                vx = enemy.LedgeAhead(d) ? 0f : d * atk.lungeSpeed;
            }
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        }

        // ④ 本刀结束 → 推进连段 / 收尾
        if (triggerCalled || stateTimer < 0f)
        {
            var step  = enemy.CurrentStep;     // 本段(读 gap),Advance 前先抓住
            var combo = enemy.CurrentCombo;    // 本套(读 cooldownAfter)

            if (enemy.AdvanceCombo())
            {
                enemy.GetComponent<DamageFeedback>()?.ClearWarning();   // 清上一段红闪
                float gap = step != null && step.gap >= 0f ? step.gap
                          : (combo != null ? combo.stepGap : 0f);
                if (gap > 0f) { inGap = true; gapTimer = gap; enemy.PlayIdle(); return; }
                FacePlayer();
                StartStep();
                return;
            }

            // 连段打完 / 单招 → 刷新冷却(整套 cooldownAfter 优先,其次招的 cooldownOverride,再默认),回 chase/idle
            float cd = combo != null && combo.cooldownAfter > 0f ? combo.cooldownAfter
                     : (atk != null && atk.cooldownOverride > 0f ? atk.cooldownOverride : enemy.attackCooldown);
            enemy.StartAttackCooldown(cd);
            stateMachine.ChangeState(enemy.player != null
                ? (EnemyBaseState)enemy.chaseState
                : enemy.idleState);
        }
    }

    void FacePlayer()
    {
        if (enemy.player != null) enemy.SetFacing(enemy.DirToward(enemy.player.position));
    }
}
