public class Enemy_GroundedState : EnemyBaseState
{
    public Enemy_GroundedState(GroundEnemy enemy, StateMachine sm, string animBoolName = "")
        : base(enemy, sm, animBoolName) { }

    public override void Update()
    {
        base.Update();
        if (enemy.DetectPlayer())
            stateMachine.ChangeState(enemy.chaseState);
    }
}
