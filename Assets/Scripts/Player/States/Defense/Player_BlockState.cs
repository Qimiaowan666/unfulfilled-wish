using UnityEngine;

public class Player_BlockState : PlayerBaseState
{
    float perfectBlockTimer;
    int   spamStacks;                  // 防刷：近期连续按下次数 → 缩小下次完美窗口
    float lastBlockPressTime = -999f;
    int   armedFrame = -1;             // 本帧已开窗 → 防止 Enter + BlockPressed 同帧重复开窗
    public bool InPerfectWindow => perfectBlockTimer > 0f;

    public Player_BlockState(PlayerController player, StateMachine sm)
        : base(player, sm, "isBlocking") { }

    public override void Enter()
    {
        base.Enter();
        ArmWindow();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    // 开/重开完美窗口(每次"按下边沿"调用)。重复按格挡惩罚：近期连按缩窗，
    // 静默 perfectBlockPenaltyResetTime 或成功精防(见 ReceiveAttack)会清零。
    void ArmWindow()
    {
        if (Time.time - lastBlockPressTime > player.perfectBlockPenaltyResetTime)
            spamStacks = 0;
        perfectBlockTimer = Mathf.Max(player.perfectBlockMinWindow,
                                      player.perfectBlockWindow - spamStacks * player.perfectBlockShrinkPerPress);
        bool shrunk = perfectBlockTimer < player.perfectBlockWindow - 0.0001f;
        Debug.Log($"[{System.DateTime.Now:HH:mm:ss.fff}] [格挡] 按下→开窗 {perfectBlockTimer:F2}s  {(shrunk ? $"·缩窗(连按×{spamStacks})" : "·满窗")}");
        spamStacks++;
        lastBlockPressTime = Time.time;
        armedFrame = Time.frameCount;
    }

    public override void Update()
    {
        base.Update();
        if (perfectBlockTimer > 0f) perfectBlockTimer -= Time.deltaTime;

        if (CheckGlobalTransitions()) return;   // 按住右键时再按左键 → 切识破

        // 已在格挡态里又按下一次(快速松→按不足一帧，没退出态) → 用按下边沿重开窗口
        if (input.BlockPressed && Time.frameCount != armedFrame) ArmWindow();

        if (!input.BlockHeld)
            stateMachine.ChangeState(player.GroundedOrFall);
    }

    public void ReceiveAttack(float damage, EnemyBase attacker = null)
    {
        if (InPerfectWindow)
        {
            spamStacks = 0;   // 成功精防 → 清零防刷惩罚，下次满窗口(仿只狼连段弹反)
            Debug.Log($"[{System.DateTime.Now:HH:mm:ss.fff}] [格挡] ✔完美！ 命中时剩{perfectBlockTimer:F2}s  → 惩罚清零·下次满窗");
            player.Stats.RedeemGhostHP(player.Stats.perfectBlockHealAmount);
            player.Stats.GainStamina(player.Stats.perfectBlockStaminaGain);
            attacker?.ApplyPoiseDamage(player.Stats.perfectBlockPoiseDamage);   // 完美格挡反震削韧（累计破韧 → 硬直）
            AudioManager.Instance?.PlayPerfectBlock();
            CameraShake.Shake(0.08f, 0.05f);
            // 精准格挡：冷白蓝火花（系统粒子预制，走 VfxManager 池）
            Vector3 bp = player.transform.position + new Vector3((player.FacingRight ? 1f : -1f) * 0.7f, 0.95f, 0f);
            VfxManager.Play("Vfx/GuardSpark", bp, Quaternion.identity, 0.95f,
                            new Color(0.8f, 0.95f, 1f), player.GetComponentInChildren<SpriteRenderer>());
            CombatSignals.RaisePerfectBlocked();   // 完美格挡 → 只发完美信号(教程里与普通格挡分开计数)
        }
        else
        {
            Debug.Log($"[{System.DateTime.Now:HH:mm:ss.fff}] [格挡] ○普通  伤害{damage:0.#}  转虚血{damage * player.Stats.ghostHPBlockRatio:0.#}  (未卡进完美窗)");
            player.Stats.OnNormalBlock(damage);
            AudioManager.Instance?.PlayBlock();
            CameraShake.Shake(0.06f, 0.03f);
            CombatSignals.RaiseBlocked();
        }
    }
}
