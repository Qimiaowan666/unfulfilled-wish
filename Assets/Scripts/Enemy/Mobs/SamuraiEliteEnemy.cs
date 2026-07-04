// 精英武士：近战三连(atk1→atk2→atk3) + 远程飞镖(throw + 手里剑)。
// 行为全靠统一攻击系统数据驱动(attacks / combos 在 Inspector 配),这里只把动作 clip 名对到 Samurai4 控制器。
public class SamuraiEliteEnemy : GroundEnemy
{
    public override string MoveClip => "run";    // Samurai4 控制器里移动叫 run(基类默认 walk);受击/死亡动画由 isHit/isDead bool 驱动,无需配 clip 名
}
