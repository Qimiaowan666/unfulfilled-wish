using UnityEngine;

public abstract class EnemyBaseState
{
    protected GroundEnemy       enemy;
    protected EnemyStateMachine stateMachine;
    protected Rigidbody2D       rb;
    protected Animator          anim;
    protected float             stateTimer;
    protected bool              triggerCalled;

    public EnemyBaseState(GroundEnemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy        = enemy;
        this.stateMachine = stateMachine;
        rb   = enemy.Rb;
        anim = enemy.Anim;
    }

    // 动画统一由各状态调 enemy.PlayXxx()(Anim.Play)，不再用 animator bool 过渡。
    public virtual void Enter()
    {
        stateTimer    = 0f;
        triggerCalled = false;
    }

    public void AnimationTrigger() => triggerCalled = true;

    public virtual void Update()
    {
        stateTimer -= Time.deltaTime;
    }

    public virtual void Exit() { }
}
