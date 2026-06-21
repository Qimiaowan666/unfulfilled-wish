// 通用状态机:主角 / 小怪 / boss 共用这一份(对标参考项目)。持有公共基类 EntityState。
// 三个命名子类(PlayerStateMachine/EnemyStateMachine/BossStateMachine)只是起个名,逻辑全在这。
public class StateMachine
{
    public EntityState currentState { get; private set; }
    public bool canChangeState = true;   // 主角某些招式播放中会 Lock();小怪/boss 用不到(恒 true)

    public void Initialize(EntityState startState)
    {
        canChangeState = true;
        currentState = startState;
        currentState.Enter();
    }

    public void ChangeState(EntityState newState)
    {
        if (!canChangeState) return;
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void Update() => currentState?.Update();
    public void Lock()   => canChangeState = false;
    public void Unlock() => canChangeState = true;
}
