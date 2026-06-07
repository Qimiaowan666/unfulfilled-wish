using UnityEngine;

public class Player_CounterState : PlayerBaseState
{
    bool isInCounterWindow;
    bool animFinished;

    public Player_CounterState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isCountering") { }

    public override void Enter()
    {
        base.Enter();
        isInCounterWindow = true;
        animFinished      = false;
        stateTimer        = player.counterWindow * 3f; // total fallback duration
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        Debug.Log("[Player] 进入识破状态，窗口由动画事件 AnimationCounterWindowClosed 关闭");
    }

    public override void Update()
    {
        base.Update();

        if (!isInCounterWindow && animFinished)
        {
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
            return;
        }

        // Fallback: timer expired
        if (stateTimer < 0f)
        {
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
        }
    }

    public override void OnCounterWindowClosed()
    {
        isInCounterWindow = false;
        Debug.Log("[Player] 识破窗口关闭（动画事件触发）");
    }

    public override void OnAnimationFinished()
    {
        animFinished = true;
    }

    public bool TryCounter(EnemyBase enemy)
    {
        if (!isInCounterWindow)
        {
            Debug.Log("[Player] 识破失败：窗口已关闭");
            return false;
        }

        Debug.Log("[Player] 识破成功！boss 当前攻击被打断（进入短暂停顿）");
        player.Stats.RedeemGhostHP(player.Stats.counterHealAmount);
        player.Stats.GainStamina(player.Stats.counterStaminaGain);
        AudioManager.Instance?.PlayCounter();
        enemy.OnCountered();   // 切到 boss 的 staggerState，结束当前攻击 + 短暂停顿
        return true;
    }
}
