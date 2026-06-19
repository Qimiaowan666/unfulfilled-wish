using UnityEngine;

public class Player_AttackState : PlayerBaseState
{
    int   comboStep;
    float lastAttackTime;
    bool  hitApplied;
    bool  animFinished;
    bool  comboQueued;
    bool  whooshPending;   // whoosh 延到首个 Update（CheckGlobalTransitions 之后）播，好让识破/冲刺能在出声前打断

    public Player_AttackState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isAttacking") { }

    public override void Enter()
    {
        if (Time.time > lastAttackTime + player.comboResetTime)
            comboStep = 0;
        if (comboStep >= player.maxComboStep)
            comboStep = 0;

        hitApplied   = false;
        animFinished = false;
        comboQueued  = false;
        whooshPending = true;   // 不在 Enter 立刻播 whoosh，等首个 Update（避免被识破打断的攻击还出声）

        base.Enter();
        anim.SetInteger("AttackStep", comboStep + 1);

        int idx = Mathf.Min(comboStep, player.attackDurations.Length - 1);
        stateTimer = player.attackDurations[idx] * 2f; // fallback if animation event missed

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Exit()
    {
        base.Exit();
        // 只有真正出手过的攻击(whoosh 已播)才推进连段；被识破/冲刺秒断的"幽灵攻击"不计
        if (!whooshPending)
        {
            comboStep++;
            if (comboStep >= player.maxComboStep) comboStep = 0;
            lastAttackTime = Time.time;
        }
    }

    public override void Update()
    {
        base.Update();

        // 识破/冲刺可打断攻击（否则识破的"左+右"会被当成左键排队下一段连招）
        if (CheckGlobalTransitions()) return;

        // whoosh 延到这里播：若上面已被识破/冲刺打断并 return，这一段攻击就不会出声
        if (whooshPending) { whooshPending = false; AudioManager.Instance?.PlayAttackWhoosh(comboStep); }

        // Queue next combo on attack press
        if (!IsInputBlocked && input.AttackPressed && comboStep < player.maxComboStep - 1)
            comboQueued = true;

        // Exit when animation event fires or timer expires
        if (animFinished || stateTimer < 0f)
            HandleStateExit();
    }

    public override void OnHitFrame()
    {
        if (hitApplied) return;
        hitApplied = true;
        DoHit();
    }

    public override void OnAnimationFinished()
    {
        animFinished = true;
    }

    void HandleStateExit()
    {
        if (comboQueued)
        {
            anim.SetBool(animBoolName, false);
            player.EnterAttackStateWithDelay();
        }
        else
        {
            stateMachine.ChangeState(player.GroundedOrFall);
        }
    }

    void DoHit()
    {
        float dir    = player.FacingRight ? 1f : -1f;
        Vector2 origin = (Vector2)player.transform.position +
                         new Vector2(player.hitboxOffset.x * dir, player.hitboxOffset.y);

        var hits = Physics2D.OverlapBoxAll(origin, player.hitboxSize, 0f, player.enemyLayer);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            int  idx    = Mathf.Min(comboStep, player.attackDamageMultipliers.Length - 1);
            float dmg   = player.Stats.attack * player.attackDamageMultipliers[idx];
            float poise = player.attackPoiseDamage[Mathf.Min(comboStep, player.attackPoiseDamage.Length - 1)];

            enemy.TakeDamage(dmg, poise);
            var feedback = hit.GetComponent<DamageFeedback>();
            feedback?.ApplyKnockback(player.transform.position, 4f);
            player.Stats.OnAttackHit();

            if (comboStep == 2) AudioManager.Instance?.PlayAttack3Impact();
        }
    }
}
