public class EnemyStateMachine
{
    public EnemyBaseState currentState { get; private set; }

    public void Initialize(EnemyBaseState startState)
    {
        currentState = startState;
        currentState.Enter();
    }

    public void ChangeState(EnemyBaseState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void Update() => currentState?.Update();
}
