using UnityEngine;

public class Enemy_ChaseState : EnemyBaseState
{
    float lastTimeDetected;
    bool  moveAnim;   // 当前是否在播 move 动画(只在切换时调 Play，避免每帧重播)

    public Enemy_ChaseState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        moveAnim = false;
        enemy.PlayIdle();   // 先 idle(覆盖刚结束的攻击 clip)，Update 立刻按移动情况切 move
        lastTimeDetected = Time.time;
    }

    // 站定时播 idle、移动时播 move（等 CD 站桩不再原地跑）
    void SetMoveAnim(bool moving)
    {
        if (moving == moveAnim) return;
        moveAnim = moving;
        if (moving) enemy.PlayMove(); else enemy.PlayIdle();
    }

    public override void Update()
    {
        if (enemy.Passive) { stateMachine.ChangeState(enemy.idleState); return; }   // 被动态绝不交战，被打进来也立刻退回待命

        base.Update();

        // 只有"看得到且够得到"才刷新交战计时；高差过大 → 计入脱战
        bool reachable = enemy.DetectPlayer() && enemy.VerticalDistToPlayer <= enemy.chaseVerticalLimit;
        if (reachable)
            lastTimeDetected = Time.time;
        else if (Time.time > lastTimeDetected + enemy.battleTimeDuration)
        {
            enemy.LoseTarget();
            stateMachine.ChangeState(enemy.idleState);
            return;
        }

        if (enemy.player == null) return;

        float dist = enemy.GetHorizontalDistToPlayer();
        float dir  = enemy.DirToward(enemy.player.position);
        enemy.SetFacing(dir);

        // 同台阶 + 冷却好 → 抽招打
        if (enemy.VerticalDistToPlayer <= enemy.attackVerticalTolerance
            && enemy.attackCooldownTimer <= 0f && enemy.TryPickAttack(dist))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            stateMachine.ChangeState(enemy.attackState);
            return;
        }

        // 钉死态(训练射手)：站定面向玩家放箭,绝不移动 / 不跑步 → 跳过所有接近/后退逻辑
        if (enemy.Stationary)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetMoveAnim(false);
            return;
        }

        if (dist < enemy.retreatDistance)
        {
            float back  = -dir;   // 后退也别退下平台边
            bool  canGo = !enemy.LedgeAhead(back);
            rb.linearVelocity = new Vector2(canGo ? back * enemy.battleMoveSpeed * 0.5f : 0f, rb.linearVelocity.y);
            SetMoveAnim(canGo);
            return;
        }
        if (dist <= enemy.preferredCombatDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetMoveAnim(false);   // 站定(等 CD)→ idle
            return;
        }
        // 追到平台边(前方是悬崖)就停住，不走下去
        bool fwd = !enemy.LedgeAhead(dir);
        rb.linearVelocity = new Vector2(fwd ? dir * enemy.battleMoveSpeed : 0f, rb.linearVelocity.y);
        SetMoveAnim(fwd);
    }
}
