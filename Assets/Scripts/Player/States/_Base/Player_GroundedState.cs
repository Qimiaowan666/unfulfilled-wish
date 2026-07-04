using UnityEngine;

public abstract class Player_GroundedState : PlayerBaseState
{
    protected Player_GroundedState(PlayerController player, StateMachine sm, string animBoolName)
        : base(player, sm, animBoolName) { }

    public override void Update()
    {
        base.Update();
        if (CheckGlobalTransitions()) return;
        if (CheckGroundedTransitions()) return;
    }

    protected bool CheckGroundedTransitions()
    {
        if (!player.IsGrounded && rb.linearVelocity.y < -0.1f)
        {
            stateMachine.ChangeState(player.fallState);
            return true;
        }

        if (CheckCombatTransitions()) return true;   // 攻击 / 格挡(地面空中共用)

        if (input.JumpPressed && player.IsGrounded)
        {
            stateMachine.ChangeState(player.jumpState);
            return true;
        }

        if (input.ExecutePressed)
        {
            if (TryStartExecute()) return true;
        }

        return false;
    }

    bool TryStartExecute()
    {
        var hits = Physics2D.OverlapCircleAll(player.transform.position, player.executeRange, player.enemyLayer);
        foreach (var col in hits)
        {
            var enemy = col.GetComponent<EnemyBase>();
            if (enemy != null && enemy.IsExecutable)
            {
                player.executeState.pendingEnemy = enemy;
                stateMachine.ChangeState(player.executeState);
                return true;
            }
        }
        return false;
    }
}
