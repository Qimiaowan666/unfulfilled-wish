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
        AudioManager.Instance?.PlayCounterWhiff();   // 识破挥击声（挥空也有反馈；命中再叠 PlayCounter）
        Debug.Log("[Player] 进入识破状态，窗口由动画事件 AnimationCounterWindowClosed 关闭");
    }

    public override void Update()
    {
        base.Update();

        if (!isInCounterWindow && animFinished)
        {
            stateMachine.ChangeState(player.GroundedOrFall);
            return;
        }

        // Fallback: timer expired
        if (stateTimer < 0f)
        {
            stateMachine.ChangeState(player.GroundedOrFall);
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
        CameraShake.Shake(0.12f, 0.08f);   // 识破成功：一记爽脆震屏
        // 识破成功：暖金火花（更大更亮，系统粒子预制，走 VfxManager 池）
        Vector3 sp = player.transform.position + new Vector3((player.FacingRight ? 1f : -1f) * 0.7f, 0.95f, 0f);
        VfxManager.Play("Vfx/GuardSpark", sp, Quaternion.identity, 1.2f,
                        new Color(1f, 0.8f, 0.45f), player.GetComponentInChildren<SpriteRenderer>());
        enemy.OnCountered();   // 切到 boss 的 staggerState，结束当前攻击 + 短暂停顿
        enemy.ApplyPoiseDamage(player.Stats.counterPoiseDamage);   // 识破再削一截韧性（凑满 → 升级成破韧大硬直）
        CombatSignals.RaiseCountered();
        return true;
    }
}
