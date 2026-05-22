using UnityEngine;

public abstract class EnemyBaseState
{
    protected AoTenguEnemy    enemy;
    protected EnemyStateMachine stateMachine;
    protected Rigidbody2D     rb;
    protected Animator        anim;
    protected string          animBoolName;
    protected float           stateTimer;
    protected bool            triggerCalled;

    public EnemyBaseState(AoTenguEnemy enemy, EnemyStateMachine stateMachine, string animBoolName)
    {
        this.enemy        = enemy;
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
        rb   = enemy.Rb;
        anim = enemy.Anim;
    }

    public virtual void Enter()
    {
        if (anim != null && anim.runtimeAnimatorController != null)
            anim.SetBool(animBoolName, true);
        stateTimer    = 0f;
        triggerCalled = false;
    }

    public void AnimationTrigger() => triggerCalled = true;

    public virtual void Update()
    {
        stateTimer -= Time.deltaTime;
    }

    public virtual void Exit()
    {
        if (anim != null && anim.runtimeAnimatorController != null)
            anim.SetBool(animBoolName, false);
    }
}
