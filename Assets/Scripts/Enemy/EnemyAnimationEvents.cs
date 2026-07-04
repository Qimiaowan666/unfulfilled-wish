using UnityEngine;

// 敌人动画事件接线员(对标 PlayerAnimationEvents):动画 clip 上的事件统一落在这,
// 转发给攻击运行器 / 受击反馈 / 音频 / 子类专属逻辑。挂在每个敌人 prefab 上(与 Animator 同物体)。
// ⚠ 方法名必须与 clip 中的 functionName 完全一致,改名会导致动画事件落空。
public class EnemyAnimationEvents : MonoBehaviour
{
    EnemyBase enemy;

    void Awake() => enemy = GetComponent<EnemyBase>();

    // 命中帧(可多次,i = hits 下标)→ 攻击运行器出伤(近战命中盒 / 远程发箭)
    public void Fire(int i) => enemy?.Attack.OnFire(i);

    // 攻击 clip 末帧 → 推进连段 / 退出(原 AnimationTrigger 别名已统一改成 AnimFinish)
    public void AnimFinish() => enemy?.Attack.OnAnimEnd();

    // 红闪预警:成对事件(时长由两事件在时间轴的间距决定)
    public void Warn()    => GetComponent<DamageFeedback>()?.HoldWarning();
    public void WarnEnd() => GetComponent<DamageFeedback>()?.ClearWarning();

    // 死亡动画"摔倒落地"帧 → 倒地音(小怪)
    public void AnimDeathFall() => AudioManager.Instance?.PlayEnemyDeath();

    // 死亡动画倒地帧 → 砸地声(boss)
    public void AnimBodyFall() => AudioManager.Instance?.PlayBossLand();

    // 射手大招起跳并悬空(special_attack 开头)
    public void SpecialLeap() => (enemy as ArcherEnemy)?.DoSpecialLeap();
}
