using UnityEngine;

public class Player_AttackState : PlayerBaseState
{
    int   comboStep;
    float lastAttackTime;
    bool  hitApplied;
    bool  animFinished;
    bool  comboQueued;

    public Player_AttackState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isAttacking") { }

    public override void Enter()
    {
        if (Time.time > lastAttackTime + player.comboResetTime)
            comboStep = 0;

        hitApplied   = false;
        animFinished = false;
        comboQueued  = false;

        base.Enter();
        anim.SetInteger("AttackStep", comboStep + 1);

        int idx = Mathf.Min(comboStep, player.attackDurations.Length - 1);
        stateTimer = player.attackDurations[idx] * 2f; // fallback if animation event missed

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        AudioManager.Instance?.PlayAttackWhoosh(comboStep);
    }

    public override void Update()
    {
        base.Update();

        // Queue next combo on attack press
        if (!IsInputBlocked && input.AttackPressed && comboStep < player.maxComboStep - 1)
            comboQueued = true;

        // Exit when animation event fires or timer expires
        if (animFinished || stateTimer < 0f)
            ExitAttack();
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

    void ExitAttack()
    {
        lastAttackTime = Time.time;

        if (comboQueued)
        {
            comboStep = (comboStep + 1) % player.maxComboStep;
            stateMachine.ChangeState(player.attackState); // re-enter same state
        }
        else
        {
            comboStep = 0;
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
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
            var enemy = hit.GetComponent<EnemyBase>() ?? hit.GetComponentInParent<EnemyBase>();
            if (enemy == null) continue;

            int  idx    = Mathf.Min(comboStep, player.attackDamageMultipliers.Length - 1);
            float dmg   = player.Stats.attack * player.attackDamageMultipliers[idx];
            float poise = player.attackPoiseDamage[Mathf.Min(comboStep, player.attackPoiseDamage.Length - 1)];

            enemy.TakeDamage(dmg, poise);
            var feedback = hit.GetComponent<DamageFeedback>() ?? hit.GetComponentInParent<DamageFeedback>();
            feedback?.ApplyKnockback(player.transform.position, 4f);
            player.Stats.OnAttackHit();

            if (comboStep == 2) AudioManager.Instance?.PlayAttack3Impact();
        }
    }
}
