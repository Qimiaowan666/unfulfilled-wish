using UnityEngine;

// boss 状态基类:公共部分(anim/rb/animBool/timer + Enter/Exit/Update)在 EntityState;这里只加 owner 引用。
public abstract class BossBaseState : EntityState
{
    protected MinotaurBoss boss;

    public BossBaseState(MinotaurBoss boss, StateMachine sm, string animBoolName)
        : base(sm, animBoolName)
    {
        this.boss = boss;
        rb   = boss.Rb;
        anim = boss.Anim;
    }

}
