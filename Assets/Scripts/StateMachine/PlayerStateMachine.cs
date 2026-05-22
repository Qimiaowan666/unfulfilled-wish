public class PlayerStateMachine
{
    public PlayerBaseState currentState { get; private set; }
    public bool canChangeState = true;

    public void Initialize(PlayerBaseState startState)
    {
        canChangeState = true;
        currentState = startState;
        currentState.Enter();
    }

    public void ChangeState(PlayerBaseState newState)
    {
        if (!canChangeState) return;
        currentState.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void Update() => currentState.Update();
    public void Lock()   => canChangeState = false;
}
