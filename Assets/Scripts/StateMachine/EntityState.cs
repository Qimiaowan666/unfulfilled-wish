using UnityEngine;

// 所有角色状态的公共基类(主角 / 小怪 / boss 共用)——对标参考项目的 EntityState。
// 公共部分:状态机引用 + anim/rb + 可选 animBool(留空=不设,走 Anim.Play)+ 计时/触发标志。
// 各角色的 owner 引用(PlayerController / GroundEnemy / MinotaurBoss)放各自子基类里。
public abstract class EntityState
{
    protected StateMachine stateMachine;
    protected string       animBoolName;

    protected Animator     anim;     // 子类构造时从各自 owner 赋值
    protected Rigidbody2D  rb;
    protected float        stateTimer;

    protected EntityState(StateMachine stateMachine, string animBoolName)
    {
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
    }

    public virtual void Enter()
    {
        SetAnimBool(true);
    }

    public virtual void Update() => stateTimer -= Time.deltaTime;

    public virtual void Exit() => SetAnimBool(false);

    // animBoolName 留空(如小怪非攻击态)→ 什么都不做,纯靠 Anim.Play
    void SetAnimBool(bool value)
    {
        if (!string.IsNullOrEmpty(animBoolName) && anim != null && anim.runtimeAnimatorController != null)
            anim.SetBool(animBoolName, value);
    }
}
