using UnityEngine;

// boss 状态基类:公共部分(anim/rb/animBool/timer + Enter/Exit/Update)在 EntityState;这里加 owner + 动画事件回调。
public abstract class BossBaseState : EntityState
{
    protected MinotaurBoss boss;

    public BossBaseState(MinotaurBoss boss, BossStateMachine sm, string animBoolName)
        : base(sm, animBoolName)
    {
        this.boss = boss;
        rb   = boss.Rb;
        anim = boss.Anim;
    }

    // ── Animation Event Callbacks（子类按需 override）─────────────────
    public virtual void OnHitWindowOpen()      { }
    public virtual void OnHitWindowClose()     { }
    public virtual void OnAnimationFinished()  { }
}
