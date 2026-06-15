using UnityEngine;

// 技能页 View：左栏主动槽 Q/E，中栏战技 + 已学列表，右栏详情。
public class SkillsPageView : CharacterPageView
{
    [Header("左栏主动槽 / 中栏列表 容器 + 行 prefab")]
    public Transform slotsContainer;
    public Transform listContainer;
    public GameObject listItemPrefab;    // ListItem.prefab
    [Header("右栏详情")]
    public DetailPanelView detail;

    object selected;   // Skill_Base 或 SkillData

    public override void Refresh()
    {
        ClearChildren(listContainer);

        var mgr = Object.FindAnyObjectByType<Player_SkillManager>();
        var skills = SkillSystem.GetOrCreate();
        int total = 0;

        // 分组一：主动（Q/E 战技 + 已学的主动技能）
        if ((mgr != null && mgr.allSkills != null && HasAny(mgr.allSkills)) || HasType(skills, SkillType.Active))
        {
            AddHeader("主动");
            if (mgr != null && mgr.allSkills != null)
                foreach (var s in mgr.allSkills)
                {
                    if (s == null) continue;
                    var captured = s; var t = s.GetSkillType();
                    AddItem(listContainer, $"[{SkillKey(t)}] {SkillDisplayName(t)}",
                        $"主动 · CD {s.GetCooldown():F1}s · 体力 {s.GetStaminaCost():F0}",
                        s.GetIcon(), ReferenceEquals(selected, s), () => { AudioManager.Instance?.PlayUIClick(); selected = captured; Refresh(); });
                    total++;
                }
            if (skills != null && skills.learnedSkills != null)
                foreach (var sk in skills.learnedSkills)
                {
                    if (sk == null || sk.type != SkillType.Active) continue;
                    var captured = sk;
                    AddItem(listContainer, sk.skillName, SkillSub(sk), sk.icon, ReferenceEquals(selected, sk),
                        () => { AudioManager.Instance?.PlayUIClick(); selected = captured; Refresh(); });
                    total++;
                }
        }

        // 分组二：被动（已学的被动技能）
        if (HasType(skills, SkillType.Passive))
        {
            AddHeader("被动");
            foreach (var sk in skills.learnedSkills)
            {
                if (sk == null || sk.type != SkillType.Passive) continue;
                var captured = sk;
                AddItem(listContainer, sk.skillName, SkillSub(sk), sk.icon, ReferenceEquals(selected, sk),
                    () => { AudioManager.Instance?.PlayUIClick(); selected = captured; Refresh(); });
                total++;
            }
        }

        RefreshDetail(total);
    }

    static bool HasType(SkillSystem skills, SkillType type)
    {
        if (skills == null || skills.learnedSkills == null) return false;
        foreach (var sk in skills.learnedSkills) if (sk != null && sk.type == type) return true;
        return false;
    }

    static bool HasAny(System.Collections.IEnumerable list)
    {
        foreach (var x in list) if (x != null) return true;
        return false;
    }

    // 分组标题行（不可选）
    void AddHeader(string text)
    {
        if (listContainer == null) return;
        var go = new GameObject("Header_" + text, typeof(RectTransform));
        go.transform.SetParent(listContainer, false);
        var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.minHeight = 28; le.preferredHeight = 28;
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 28);
        var t = go.AddComponent<TMPro.TextMeshProUGUI>();
        t.text = "— " + text + " —";
        t.fontSize = 18; t.fontStyle = TMPro.FontStyles.Bold;
        t.alignment = TMPro.TextAlignmentOptions.Center;
        t.color = new Color(0.55f, 0.45f, 0.30f);
    }

    void AddItem(Transform c, string title, string sub, Sprite icon, bool sel, System.Action onClick)
    {
        if (listItemPrefab == null || c == null) return;
        var go = Instantiate(listItemPrefab, c);
        var item = go.GetComponent<ListItemView>();
        if (item != null) item.Setup(title, sub, icon, sel, onClick);
    }

    void RefreshDetail(int total)
    {
        if (detail == null) return;

        if (selected is Skill_Base sb)
        {
            var t = sb.GetSkillType();
            detail.SetHeader($"[{SkillKey(t)}] {SkillDisplayName(t)}", ActiveDesc(t), sb.GetIcon());
            detail.ClearStats();
            detail.AddStat("冷却", $"{sb.GetCooldown():F1}s");
            detail.AddStat("体力消耗", $"{sb.GetStaminaCost():F0}");
            detail.HideAction();
        }
        else if (selected is SkillData sd)
        {
            detail.SetHeader(sd.skillName, string.IsNullOrWhiteSpace(sd.description) ? (sd.type == SkillType.Active ? "主动技能" : "被动技能") : sd.description, sd.icon);
            detail.ClearStats();
            if (sd.type == SkillType.Active)
            {
                detail.AddStat("伤害", $"{sd.damage:F0}");
                detail.AddStat("韧性", $"{sd.poiseDamage:F0}");
                detail.AddStat("冷却", $"{sd.cooldown:F1}s");
            }
            else
            {
                detail.AddStat("攻击加成", $"+{sd.attackPercent:F0}%");
                detail.AddStat("防御加成", $"+{sd.defensePercent:F0}%");
            }
            detail.HideAction();
        }
        else
        {
            detail.ShowEmpty("技能", total == 0 ? "尚未拥有技能。" : "选择一个技能查看详情。");
        }
    }

    static string SkillSub(SkillData sk)
    {
        if (sk.type == SkillType.Active) return $"主动 · 伤害 {sk.damage:F0} · CD {sk.cooldown:F1}s";
        return $"被动 · 攻 +{sk.attackPercent:F0}% 防 +{sk.defensePercent:F0}%";
    }

    static string SkillDisplayName(PlayerSkillType t)
    {
        switch (t) { case PlayerSkillType.DashStrike: return "突刺斩"; case PlayerSkillType.Heal: return "治疗"; default: return t.ToString(); }
    }

    static string SkillKey(PlayerSkillType t)
    {
        switch (t) { case PlayerSkillType.DashStrike: return "Q"; case PlayerSkillType.Heal: return "E"; default: return "?"; }
    }

    static string ActiveDesc(PlayerSkillType t)
    {
        switch (t)
        {
            case PlayerSkillType.DashStrike: return "向前突进并斩击，对命中的敌人造成伤害。";
            case PlayerSkillType.Heal:       return "消耗体力，恢复一部分生命。";
            default: return "战技。";
        }
    }
}
