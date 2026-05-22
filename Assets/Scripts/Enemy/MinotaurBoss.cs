using UnityEngine;

public class MinotaurBoss : EnemyBase
{
    static readonly string[] boolNames =
    {
        "isIdle", "isMoving", "isAttacking", "isAttacking2",
        "isSpecialAttacking", "isDefending", "isHit", "isDead"
    };

    protected override void OnDeath()   => SetAnimBool("isDead");
    protected override void OnRespawn() => SetAnimBool("isIdle");

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        GetComponent<BossAI>()?.OnPoiseBreak();
    }

    public override void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var name in boolNames)
            Anim.SetBool(name, name == boolName);
    }
}
