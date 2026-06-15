using UnityEngine;

// 单个 boss 的展示数据（名字 / 登场名牌素材 / 配色）。
// 在 Project 里右键 Create/Game/Boss Profile 新建，挂到 boss 的 EnemyBase.profile。
// 血条读 displayName(+可选 barColor)；登场名牌(BossNameCard)读 romanSprite + nameChars 动态排版（任意字数）。
[CreateAssetMenu(fileName = "BossProfile", menuName = "Game/Boss Profile")]
public class BossProfile : ScriptableObject
{
    [Header("通用")]
    [Tooltip("血条上显示的 boss 名")]
    public string displayName = "Boss";

    [Header("血条配色（可选）")]
    [Tooltip("勾上才会用 barColor 覆盖血条填充色；不勾保持预制体里的原色")]
    public bool  overrideBarColor = false;
    public Color barColor = new Color(0.85f, 0.15f, 0.2f, 1f);

    [Header("登场名牌 (BossNameCard)")]
    [Tooltip("罗马名整图（白底任意染色），如 MINOTAUR")]
    public Sprite romanSprite;
    [Tooltip("中文名【逐字】图，按顺序排（任意字数）。逐字弹入。")]
    public Sprite[] nameChars;
    [Tooltip("中文名染色")]
    public Color nameColor = new Color(214f / 255f, 38f / 255f, 50f / 255f, 1f);

    [Header("击破名牌 (BossFinishUI)")]
    [Tooltip("击破时的「xx 被打败了」烘焙整图（per-boss）。空则用预制体里的默认。")]
    public Sprite defeatBanner;
}
