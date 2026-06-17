// 最基础的小怪：只有 1 招，行为全部继承 GroundEnemy。
// 它的 animator 用 idle/walk/attack/hit/death 命名(只覆盖与基类默认不同的两个)。
public class AoTenguEnemy : GroundEnemy
{
    public override string MoveClip => "walk";
    public override string HurtClip => "hit";
}
