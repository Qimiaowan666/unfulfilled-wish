using UnityEngine;

public abstract class BossBaseState
{
    protected MinotaurBoss      boss;
    protected BossStateMachine  stateMachine;
    protected Rigidbody2D       rb;
    protected Animator          anim;
    protected string            animBoolName;
    protected float             stateTimer;

    public BossBaseState(MinotaurBoss boss, BossStateMachine sm, string animBoolName)
    {
        this.boss         = boss;
        this.stateMachine = sm;
        this.animBoolName = animBoolName;
        rb   = boss.Rb;
        anim = boss.Anim;
    }

    public virtual void Enter()
    {
        if (!string.IsNullOrEmpty(animBoolName) && anim != null && anim.runtimeAnimatorController != null)
            anim.SetBool(animBoolName, true);
    }

    public virtual void Update()
    {
        stateTimer -= Time.deltaTime;
    }

    public virtual void Exit()
    {
        if (!string.IsNullOrEmpty(animBoolName) && anim != null && anim.runtimeAnimatorController != null)
            anim.SetBool(animBoolName, false);
    }

    // ── Animation Event Callbacks（子类按需 override）─────────────────
    public virtual void OnHitWindowOpen()      { }
    public virtual void OnHitWindowClose()     { }
    public virtual void OnAnimationFinished()  { }
}
