using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class CharacterStatusPageUI : CharacterPanelPage
{
    CharacterPanelUIFactory ui;
    Image hpFill;
    RectTransform hpFillRect;
    Text hpValueText;
    Text attackText;
    Text defenseText;
    Text goldText;
    Text breakdownText;

    public override void Build(Transform parent, CharacterPanelUIFactory uiFactory)
    {
        ui = uiFactory;
        Root = ui.CreateRoot("StatusPage", parent);

        BuildHpPanel(Root.transform);
        BuildStatCards(Root.transform);
        BuildBreakdownPanel(Root.transform);
    }

    void BuildHpPanel(Transform parent)
    {
        var panel = ui.CreatePanel("HpPanel", parent, ui.PanelAltColor);
        ui.SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), new Vector2(0f, 0f));

        var label = ui.CreateText("Label", panel.transform, "生命", 20, TextAnchor.MiddleLeft);
        ui.SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(18f, 0f), new Vector2(92f, 0f));

        var barBg = ui.CreatePanel("HpBarBackground", panel.transform, new Color(0.05f, 0.06f, 0.08f, 0.95f));
        ui.SetRect(barBg.rectTransform, Vector2.zero, Vector2.one, new Vector2(98f, 34f), new Vector2(-136f, -34f));

        hpFill = ui.CreatePanel("HpFill", barBg.transform, new Color(0.22f, 0.78f, 0.42f, 1f));
        ui.SetRect(hpFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        hpFillRect = hpFill.rectTransform;

        hpValueText = ui.CreateText("HpValue", panel.transform, "", 18, TextAnchor.MiddleRight);
        hpValueText.color = ui.MutedTextColor;
        ui.SetRect(hpValueText.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-126f, 0f), new Vector2(-18f, 0f));
    }

    void BuildStatCards(Transform parent)
    {
        var cards = ui.CreateUIObject("StatCards", parent);
        ui.SetRect(cards.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -210f), new Vector2(0f, -112f));

        var grid = cards.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(210f, 92f);
        grid.spacing = new Vector2(12f, 0f);
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.MiddleLeft;

        attackText = BuildStatCard(cards.transform, "攻击");
        defenseText = BuildStatCard(cards.transform, "防御");
        goldText = BuildStatCard(cards.transform, "金币");
    }

    Text BuildStatCard(Transform parent, string label)
    {
        var card = ui.CreatePanel(label + "Card", parent, ui.PanelAltColor);

        var labelText = ui.CreateText("Label", card.transform, label, 16, TextAnchor.UpperLeft);
        labelText.color = ui.MutedTextColor;
        ui.SetRect(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 10f), new Vector2(-14f, -10f));

        var valueText = ui.CreateText("Value", card.transform, "", 22, TextAnchor.LowerLeft);
        valueText.fontStyle = FontStyle.Bold;
        ui.SetRect(valueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 10f), new Vector2(-14f, -12f));
        return valueText;
    }

    void BuildBreakdownPanel(Transform parent)
    {
        var panel = ui.CreatePanel("BreakdownPanel", parent, ui.PanelAltColor);
        ui.SetRect(panel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -232f));

        var title = ui.CreateText("Title", panel.transform, "属性构成", 20, TextAnchor.UpperLeft);
        ui.SetRect(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -48f), new Vector2(-18f, -14f));

        breakdownText = ui.CreateText("Breakdown", panel.transform, "", 18, TextAnchor.UpperLeft);
        breakdownText.lineSpacing = 1.3f;
        breakdownText.verticalOverflow = VerticalWrapMode.Overflow;
        ui.SetRect(breakdownText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -62f));
    }

    public override void Refresh()
    {
        var stats = Object.FindAnyObjectByType<PlayerStats>();
        if (stats == null)
        {
            if (breakdownText != null) breakdownText.text = "未找到玩家状态。";
            return;
        }

        float maxHP = Mathf.Max(stats.maxHP, 1f);
        if (hpFillRect != null)
            hpFillRect.anchorMax = new Vector2(Mathf.Clamp01(stats.CurrentHP / maxHP), 1f);
        if (hpValueText != null) hpValueText.text = $"{stats.CurrentHP:F0} / {stats.maxHP:F0}";

        stats.GetAttackBreakdown(out float baseAttack, out float equipmentAttack, out float skillAttack);
        stats.GetDefenseBreakdown(out float baseDefense, out float equipmentDefense, out float skillDefense);

        if (attackText != null)
            attackText.text = $"{stats.attack:F0}  ({baseAttack:F0} + {equipmentAttack:F0} + {skillAttack:F0})";
        if (defenseText != null)
            defenseText.text = $"{stats.defense:F0}  ({baseDefense:F0} + {equipmentDefense:F0} + {skillDefense:F0})";
        if (goldText != null)
            goldText.text = stats.gold.ToString();

        var sb = new StringBuilder();
        sb.AppendLine($"攻击  {stats.attack:F0} = 基础 {baseAttack:F0} + 装备 {equipmentAttack:F0} + 技能 {skillAttack:F0}");
        sb.AppendLine($"防御  {stats.defense:F0} = 基础 {baseDefense:F0} + 装备 {equipmentDefense:F0} + 技能 {skillDefense:F0}");
        sb.AppendLine($"技能加成基于基础值计算：攻击 +{stats.SkillAttackPercent:F0}%，防御 +{stats.SkillDefensePercent:F0}%");
        sb.AppendLine($"体力  {stats.CurrentStamina:F0} / {stats.maxStamina:F0}（完美格挡 / 识破回复，用于释放战技）");

        if (stats.CurrentGhostHP > 0f)
            sb.AppendLine($"虚血  {stats.CurrentGhostHP:F0}");

        if (breakdownText != null)
            breakdownText.text = sb.ToString();
    }
}
