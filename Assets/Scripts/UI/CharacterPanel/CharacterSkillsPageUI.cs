using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSkillsPageUI : CharacterPanelPage
{
    CharacterPanelUIFactory ui;
    Transform listContent;
    Text headerText;

    public override void Build(Transform parent, CharacterPanelUIFactory uiFactory)
    {
        ui = uiFactory;
        Root = ui.CreateRoot("SkillsPage", parent);

        headerText = ui.CreateText("Header", Root.transform, "已学技能", 20, TextAnchor.MiddleLeft);
        ui.SetRect(headerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 0f));

        var scroll = ui.CreateScrollArea("SkillsScroll", Root.transform, out listContent);
        ui.SetRect(scroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -42f));
        ui.ConfigureVerticalContent(listContent, 10f, 0f);
    }

    public override void Refresh()
    {
        var skills = SkillSystem.GetOrCreate();
        ui.ClearChildren(listContent);

        if (skills == null || skills.learnedSkills.Count == 0)
        {
            if (headerText != null) headerText.text = "已学技能  0";
            CreateEmptyMessage("尚未学习技能。");
            return;
        }

        if (headerText != null) headerText.text = $"已学技能  {skills.learnedSkills.Count}";

        foreach (var skill in skills.learnedSkills)
        {
            if (skill == null) continue;
            BuildSkillCard(skill);
        }
    }

    void BuildSkillCard(SkillData skill)
    {
        var card = ui.CreatePanel(skill.skillName + "Card", listContent, ui.GetSkillColor(skill));
        ui.SetFixedHeight(card.rectTransform, 76f);

        var iconBlock = ui.CreateIconBlock("Icon", card.transform, new Vector2(50f, 50f));
        var iconRect = iconBlock.RootImage.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(12f, 0f);
        ui.SetIcon(iconBlock, skill.icon, ui.GetSkillColor(skill), ui.GetSkillPlaceholder(skill));

        var name = ui.CreateText("Name", card.transform, skill.skillName, 19, TextAnchor.UpperLeft);
        ui.SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(74f, -8f), new Vector2(-76f, -4f));

        var type = ui.CreateText("Type", card.transform, skill.type == SkillType.Active ? "主动" : "被动", 15, TextAnchor.UpperRight);
        type.color = ui.MutedTextColor;
        ui.SetRect(type.rectTransform, new Vector2(1f, 0.5f), Vector2.one, new Vector2(-72f, -10f), new Vector2(-12f, -4f));

        var stats = ui.CreateText("Stats", card.transform, BuildSkillStatText(skill), 15, TextAnchor.LowerLeft);
        stats.color = ui.MutedTextColor;
        ui.SetRect(stats.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(74f, 8f), new Vector2(-12f, 2f));
    }

    void CreateEmptyMessage(string message)
    {
        var text = ui.CreateText("EmptyMessage", listContent, message, 18, TextAnchor.MiddleCenter);
        ui.SetFixedHeight(text.rectTransform, 52f);
        text.color = ui.MutedTextColor;
    }

    static string BuildSkillStatText(SkillData skill)
    {
        if (skill.type == SkillType.Active)
            return $"伤害 {skill.damage:F0}  韧性 {skill.poiseDamage:F0}  CD {skill.cooldown:F1}s";

        var sb = new StringBuilder();
        sb.Append($"攻击 +{skill.attackPercent:F0}%");
        sb.Append($"  防御 +{skill.defensePercent:F0}%");
        return sb.ToString();
    }
}
