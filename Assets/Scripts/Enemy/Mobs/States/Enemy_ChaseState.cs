using UnityEngine;

// 小怪交战态(战斗大脑):锁定玩家后每帧决策——脱战没?→ 转身面向 → 冷却好且够得着就出手 → 否则按距离维持战距(太远走近/太近后撤/合适站定)。
// 动画:构造声明 isIdle 撑住 Locomotion(base.Enter 点亮 / base.Exit 清掉),idle↔walk 全由 Speed(水平速度,GroundEnemy.Update 每帧喂)自动切。
public class Enemy_ChaseState : EnemyBaseState
{
    float lastTimeDetected;

    public Enemy_ChaseState(GroundEnemy enemy, StateMachine sm)
        : base(enemy, sm, "isIdle") { }

    public override void Enter()
    {
        base.Enter();   // isIdle=true → 撑住 Locomotion
        lastTimeDetected = Time.time;
    }
    // Exit 用基类默认(把 isIdle 清成 false),无需覆盖

    public override void Update()
    {
        if (enemy.Passive) { stateMachine.ChangeState(enemy.patrolState); return; }   // 被动态绝不交战，被打进来也立刻退回待命

        base.Update();

        // 只有"看得到且够得到"才刷新交战计时；高差过大 → 计入脱战
        bool reachable = enemy.DetectPlayer() && enemy.VerticalDistToPlayer <= enemy.chaseVerticalLimit;
        if (reachable)
            lastTimeDetected = Time.time;
        else if (Time.time > lastTimeDetected + enemy.battleTimeDuration)
        {
            enemy.LoseTarget();
            stateMachine.ChangeState(enemy.patrolState);
            return;
        }

        if (enemy.playerTransform == null) return;

        float dist = enemy.GetHorizontalDistToPlayer();
        float dir  = enemy.DirToward(enemy.playerTransform.position);
        enemy.SetFacing(dir);

        // 同台阶 + 冷却好 → 抽连段打(小怪每招也是个单段 combo)
        if (enemy.VerticalDistToPlayer <= enemy.attackVerticalTolerance
            && enemy.attackCooldownTimer <= 0f
            && enemy.TryPickCombo(dist))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            stateMachine.ChangeState(enemy.attackState);
            return;
        }

        // 钉死态(训练射手)：站定面向玩家放箭,绝不移动 / 不跑步 → 跳过所有接近/后退逻辑
        if (enemy.Stationary)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (dist < enemy.retreatDistance)
        {
            float back  = -dir;   // 后退也别退下平台边
            bool  canGo = !enemy.LedgeAhead(back);
            rb.linearVelocity = new Vector2(canGo ? back * enemy.battleMoveSpeed * 0.5f : 0f, rb.linearVelocity.y);
            return;
        }
        if (dist <= enemy.preferredCombatDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);   // 站定(等 CD)→ Speed=0 → idle
            return;
        }
        // 追到平台边(前方是悬崖)就停住，不走下去
        bool fwd = !enemy.LedgeAhead(dir);
        rb.linearVelocity = new Vector2(fwd ? dir * enemy.battleMoveSpeed : 0f, rb.linearVelocity.y);
    }
}
