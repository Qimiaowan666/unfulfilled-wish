public class BossStateMachine
{
    public BossBaseState currentState { get; private set; }

    public void Initialize(BossBaseState startState)
    {
        currentState = startState;
        currentState.Enter();
    }

    public void ChangeState(BossBaseState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void Update() => currentState?.Update();
}
