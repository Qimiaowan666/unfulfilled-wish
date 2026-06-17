public class Enemy_GroundedState : EnemyBaseState
{
    public Enemy_GroundedState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Update()
    {
        base.Update();
        if (enemy.DetectPlayer())
            stateMachine.ChangeState(enemy.chaseState);
    }
}
