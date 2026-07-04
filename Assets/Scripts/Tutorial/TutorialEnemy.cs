using UnityEngine;

// 教程靶子：把"被动 / 打不死 / 被打不位移 / 门开后消失"这些教程专属行为收在这一个组件,
// 不污染核心 EnemyBase / GroundEnemy。挂在练习敌人上,勾需要的开关即可。
// 全部复用引擎现成能力:passive→GroundEnemy.Passive、immortal→EnemyBase.Invincible、immovable→冻结刚体水平位置。
[RequireComponent(typeof(EnemyBase))]
public class TutorialEnemy : MonoBehaviour
{
    [Tooltip("被动:不主动移动 / 不感知 / 不攻击(木偶靶子)")]
    public bool passive;
    [Tooltip("钉死射手:照常瞄准放箭,但绝不移动 / 不跑步(训练弓箭手)")]
    public bool stationary;
    [Tooltip("被打不位移:冻结水平位置,挨打不会被击退")]
    public bool immovable;
    [Tooltip("打不死:玩家伤害无效,且固定位置不被击退(格挡 / 识破练习靶)")]
    public bool immortal;
    [Tooltip("这道门打开后,本敌人消失;留空=不消失")]
    public TutorialGate despawnWhenGateOpens;

    void Awake()
    {
        var enemy  = GetComponent<EnemyBase>();
        var ground = enemy as GroundEnemy;
        if (passive    && ground != null) ground.Passive    = true;     // 木桩:不感知/不攻击/不动
        if (stationary && ground != null) ground.Stationary = true;     // 钉死射手:照常放箭但不移动/不跑步
        if (immortal) enemy.Invincible = true;                          // 打不死(免伤)
        // 推不动:immortal 顺带固定(免伤不挡击退,玩家攻击仍会调 ApplyKnockback) / 或单独勾 immovable
        if ((immortal || immovable) && enemy.Rb != null)
            enemy.Rb.constraints |= RigidbodyConstraints2D.FreezePositionX;   // 冻结水平位移
    }

    void OnEnable()  { if (despawnWhenGateOpens != null) despawnWhenGateOpens.Opened += Despawn; }
    void OnDisable() { if (despawnWhenGateOpens != null) despawnWhenGateOpens.Opened -= Despawn; }

    void Despawn() => gameObject.SetActive(false);
}
