using UnityEngine;

public class Player_ExecuteState : PlayerBaseState
{
    public EnemyBase pendingEnemy; // set by GroundedState before ChangeState
    bool hitApplied;
    bool animFinished;
    float elapsed;

    public Player_ExecuteState(PlayerController player, StateMachine sm)
        : base(player, sm, "isExecuting") { }

    public override void Enter()
    {
        if (pendingEnemy == null || !pendingEnemy.IsExecutable)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        hitApplied   = false;
        animFinished = false;
        elapsed      = 0f;
        stateTimer   = player.executeDuration * 1.5f;   // 兜底时长(正常靠动画放完退出)
        base.Enter();
        player.Stats.SetInvulnerable(true);
        AudioManager.Instance?.PlayExecuteDraw();
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        // 兜底:动画 AnimHitFrame 事件没触发时,较晚再补一次(不抢在事件前)
        if (!hitApplied && elapsed >= 0.6f) ApplyExecuteHit();

        // 退出靠动画末尾的 AnimFinished 事件(→ OnAnimationFinished 置 animFinished);stateTimer 为绝对兜底
        if (animFinished || stateTimer < 0f)
            stateMachine.ChangeState(player.GroundedOrFall);   // 无敌解除放到 Exit(给后摇无敌帧)
    }

    // 由动画事件 AnimHitFrame 调(有的话,时机更准);没有就靠 Update 兜底
    public override void OnHitFrame() => ApplyExecuteHit();

    void ApplyExecuteHit()
    {
        if (hitApplied) return;
        hitApplied = true;

        if (pendingEnemy != null && pendingEnemy.IsExecutable)
        {
            pendingEnemy.OnExecuted(player.Stats.attack * player.executeDamageMultiplier);
            AudioManager.Instance?.PlayExecuteStrike();
            // boss 被处决致死 → 交给 BossFinishUI 击破演出独占时间/镜头,别再叠通用顿帧/震屏(否则抢 timeScale 把演出搞乱)
            if (!(pendingEnemy.isBoss && pendingEnemy.CurrentHP <= 0f))
            {
                CameraShake.Shake(0.3f, 0.9f);    // 处决强震(略长于顿帧,让镜头在时间恢复时"踢"一下)
                Hitstop.Do(0.25f, 0.04f);          // 处决顿帧:够重又不拖沓
            }
        }
    }

    public override void OnAnimationFinished() => animFinished = true;

    public override void Exit()
    {
        if (!hitApplied) ApplyExecuteHit();   // 绝对兜底:离开前一定结算
        base.Exit();
        player.Stats.SetInvulnerableFor(0.35f);   // 处决收尾的后摇无敌帧,避免刚结束就被反打
        pendingEnemy = null;
    }
}
