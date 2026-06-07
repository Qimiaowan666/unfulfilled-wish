using UnityEngine;

// 闪身：原地淡出 → 瞬移到玩家身后 → 淡入。
// 位移期间无敌（Invincible），现身后短暂硬直（landingRecovery）再出招。
public class Boss_RepositionState : BossBaseState
{
    Vector2 targetPos;
    MinotaurBoss.AttackId pendingAttack;

    float elapsed;
    bool  landed;          // 现身完成，进入落地硬直
    RigidbodyType2D originalBodyType;

    public Boss_RepositionState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isIdle") {}

    public void Configure(MinotaurBoss.MovementType move, Vector2 target, MinotaurBoss.AttackId next)
    {
        targetPos     = target;
        pendingAttack = next;
    }

    public override void Enter()
    {
        base.Enter();

        elapsed = 0f;
        landed  = false;

        boss.SetInvincible(true);
        originalBodyType  = rb.bodyType;
        rb.bodyType       = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        elapsed += Time.deltaTime;
        float dur = boss.teleportDuration;

        if (!landed)
        {
            float t = dur > 0f ? Mathf.Clamp01(elapsed / dur) : 1f;

            // 前半段原地淡出，后半段在目标点淡入
            if (t < 0.5f)
            {
                boss.SetSpriteAlpha(1f - t / 0.5f);
            }
            else
            {
                boss.transform.position = targetPos;
                boss.SetSpriteAlpha((t - 0.5f) / 0.5f);
            }

            if (t >= 1f) OnArrived();
            return;
        }

        // 现身硬直：已解除无敌，到点出招
        if (elapsed >= dur + boss.landingRecovery)
        {
            boss.RefreshFacingToPlayer();
            boss.EnterAttack(pendingAttack);
        }
    }

    void OnArrived()
    {
        landed = true;
        boss.transform.position = targetPos;
        boss.SetSpriteVisible(true);
        boss.SetSpriteAlpha(1f);
        boss.SetInvincible(false);          // 现身即恢复可被伤 → 玩家反击窗口
        rb.bodyType = originalBodyType;
        rb.linearVelocity = Vector2.zero;
        boss.RefreshFacingToPlayer();
    }

    public override void Exit()
    {
        base.Exit();
        // 兜底：任何原因离开本 state（被识破/死亡等）都恢复可见、可伤、物理
        boss.SetSpriteVisible(true);
        boss.SetSpriteAlpha(1f);
        boss.SetInvincible(false);
        if (rb.bodyType == RigidbodyType2D.Kinematic)
            rb.bodyType = originalBodyType;
    }
}
