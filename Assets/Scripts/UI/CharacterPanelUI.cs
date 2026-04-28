using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CharacterPanelUI : MonoBehaviour
{
    enum CharacterTab
    {
        Status,
        Equipment,
        Inventory,
        Skills
    }

    public GameObject panel;
    public Text titleText;
    public Text contentText;
    public Transform equipmentActionParent;
    public Button statusButton;
    public Button equipmentButton;
    public Button inventoryButton;
    public Button skillsButton;

    [Header("Legacy References")]
    public Text hpText;
    public Text attackText;
    public Text defenseText;
    public Text goldText;
    public Image weaponIcon;
    public Image armorIcon;
    [FormerlySerializedAs("accessoryIcon")]
    public Image accessory1Icon;
    public Image accessory2Icon;

    bool isOpen;
    bool pausedByPanel;
    float previousTimeScale = 1f;
    CharacterTab currentTab = CharacterTab.Status;

    void Start()
    {
        EnsureUI();
        BindButtons();
        SetOpen(false);
    }

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        if (pausedByPanel)
            ResumeGameTime();

        Unsubscribe();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (ShopUI.IsOpen) return;

        if (keyboard.cKey.wasPressedThisFrame)
            Toggle();

        if (!isOpen) return;
        if (keyboard.digit1Key.wasPressedThisFrame) SetTab(CharacterTab.Status);
        if (keyboard.digit2Key.wasPressedThisFrame) SetTab(CharacterTab.Equipment);
        if (keyboard.digit3Key.wasPressedThisFrame) SetTab(CharacterTab.Inventory);
        if (keyboard.digit4Key.wasPressedThisFrame) SetTab(CharacterTab.Skills);
    }

    void Toggle()
    {
        SetOpen(!isOpen);
    }

    void SetOpen(bool value)
    {
        if (isOpen == value)
        {
            if (panel != null) panel.SetActive(isOpen);
            return;
        }

        isOpen = value;
        PlayerInputBuffer.ClearAll();
        if (panel != null) panel.SetActive(isOpen);

        if (isOpen)
        {
            PauseGameTime();
            Refresh();
        }
        else
        {
            ResumeGameTime();
        }
    }

    void PauseGameTime()
    {
        if (pausedByPanel) return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedByPanel = true;
    }

    void ResumeGameTime()
    {
        if (!pausedByPanel) return;

        Time.timeScale = GameManager.Instance != null && GameManager.Instance.IsPaused
            ? 0f
            : previousTimeScale;

        pausedByPanel = false;
    }

    void SetTab(CharacterTab tab)
    {
        currentTab = tab;
        Refresh();
    }

    void Refresh()
    {
        if (!isOpen) return;
        if (titleText != null) titleText.text = GetTabTitle();
        if (contentText != null) contentText.text = BuildTabText();
        RefreshLegacyFields();
        UpdateButtonStates();
        RefreshActionButtons();
    }

    string GetTabTitle()
    {
        switch (currentTab)
        {
            case CharacterTab.Status: return "状态";
            case CharacterTab.Equipment: return "装备";
            case CharacterTab.Inventory: return "背包";
            case CharacterTab.Skills: return "技能";
            default: return string.Empty;
        }
    }

    string BuildTabText()
    {
        switch (currentTab)
        {
            case CharacterTab.Status: return BuildStatusText();
            case CharacterTab.Equipment: return BuildEquipmentText();
            case CharacterTab.Inventory: return BuildInventoryText();
            case CharacterTab.Skills: return BuildSkillsText();
            default: return string.Empty;
        }
    }

    string BuildStatusText()
    {
        var stats = FindAnyObjectByType<PlayerStats>();
        var controller = FindAnyObjectByType<PlayerController>();
        if (stats == null) return "未找到玩家状态。";

        var sb = new StringBuilder();
        sb.AppendLine($"HP: {stats.CurrentHP:F0} / {stats.maxHP:F0}");
        sb.AppendLine($"虚血: {stats.CurrentGhostHP:F0}");
        sb.AppendLine($"攻击: {stats.attack:F0}");
        sb.AppendLine($"防御: {stats.defense:F0}");
        sb.AppendLine($"金币: {stats.gold}");
        if (controller != null)
        {
            sb.AppendLine();
            sb.AppendLine($"当前状态: {controller.State}");
            sb.AppendLine($"在地面: {(controller.IsGrounded ? "是" : "否")}");
            sb.AppendLine($"朝向: {(controller.FacingRight ? "右" : "左")}");
        }

        return sb.ToString();
    }

    string BuildEquipmentText()
    {
        var eq = EquipmentSystem.Instance;
        if (eq == null) return "未找到装备系统。";

        var sb = new StringBuilder();
        AppendEquipment(sb, "武器", eq.weapon);
        AppendEquipment(sb, "防具", eq.armor);
        AppendEquipment(sb, "饰品 1", eq.accessory1);
        AppendEquipment(sb, "饰品 2", eq.accessory2);
        sb.AppendLine();
        sb.AppendLine("已有装备：");

        if (eq.ownedEquipment.Count == 0)
        {
            sb.AppendLine("暂无。");
        }
        else
        {
            for (int i = 0; i < eq.ownedEquipment.Count; i++)
            {
                var equipment = eq.ownedEquipment[i];
                if (equipment == null) continue;
                string state = IsEquipped(eq, equipment) ? "已装备" : "可装备";
                sb.AppendLine($"{i + 1}. {equipment.equipmentName} [{GetSlotName(equipment.slot)}] - {state}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("点击下方按钮切换装备。");
        return sb.ToString();
    }

    void AppendEquipment(StringBuilder sb, string label, EquipmentData equipment)
    {
        if (equipment == null)
        {
            sb.AppendLine($"{label}: 未装备");
            return;
        }

        sb.AppendLine($"{label}: {equipment.equipmentName}");
        if (!string.IsNullOrWhiteSpace(equipment.description))
            sb.AppendLine($"  {equipment.description}");
        sb.AppendLine($"  攻击 +{equipment.attackBonus:F0}  防御 +{equipment.defenseBonus:F0}  HP +{equipment.maxHPBonus:F0}");
    }

    string BuildInventoryText()
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null) return "未找到背包系统。";
        if (inventory.items.Count == 0) return "背包为空。";

        var sb = new StringBuilder();
        sb.AppendLine($"容量: {inventory.items.Count} / {inventory.maxSlots}");
        sb.AppendLine("点击下方按钮使用道具。");
        sb.AppendLine();
        for (int i = 0; i < inventory.items.Count; i++)
        {
            var item = inventory.items[i];
            if (item == null)
            {
                sb.AppendLine($"{i + 1}. <空物品>");
                continue;
            }

            sb.AppendLine($"{i + 1}. {item.itemName} [{item.type}]");
            if (!string.IsNullOrWhiteSpace(item.description))
                sb.AppendLine($"   {item.description}");
            if (item.healAmount > 0f)
                sb.AppendLine($"   回复 HP: {item.healAmount:F0}");
        }

        return sb.ToString();
    }

    string BuildSkillsText()
    {
        var skills = SkillSystem.GetOrCreate();
        if (skills == null) return "未找到技能系统。";
        if (skills.learnedSkills.Count == 0) return "尚未学习技能。";

        var sb = new StringBuilder();
        for (int i = 0; i < skills.learnedSkills.Count; i++)
        {
            var skill = skills.learnedSkills[i];
            if (skill == null)
            {
                sb.AppendLine($"{i + 1}. <空技能>");
                continue;
            }

            sb.AppendLine($"{i + 1}. {skill.skillName} [{skill.type}]");
            if (!string.IsNullOrWhiteSpace(skill.description))
                sb.AppendLine($"   {skill.description}");
            if (skill.type == SkillType.Active)
                sb.AppendLine($"   伤害: {skill.damage:F0}  韧性伤害: {skill.poiseDamage:F0}  冷却: {skill.cooldown:F1}s");
            else
                sb.AppendLine($"   攻击 +{skill.attackPercent:F0}%  防御 +{skill.defensePercent:F0}%");
        }

        return sb.ToString();
    }

    void RefreshLegacyFields()
    {
        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            if (hpText != null) hpText.text = $"HP: {stats.CurrentHP:F0} / {stats.maxHP:F0}";
            if (attackText != null) attackText.text = $"ATK: {stats.attack:F0}";
            if (defenseText != null) defenseText.text = $"DEF: {stats.defense:F0}";
            if (goldText != null) goldText.text = $"Gold: {stats.gold}";
        }

        var eq = EquipmentSystem.Instance;
        if (eq == null) return;
        if (weaponIcon != null) weaponIcon.sprite = eq.weapon != null ? eq.weapon.icon : null;
        if (armorIcon != null) armorIcon.sprite = eq.armor != null ? eq.armor.icon : null;
        if (accessory1Icon != null) accessory1Icon.sprite = eq.accessory1 != null ? eq.accessory1.icon : null;
        if (accessory2Icon != null) accessory2Icon.sprite = eq.accessory2 != null ? eq.accessory2.icon : null;
    }

    void UpdateButtonStates()
    {
        SetButtonColor(statusButton, currentTab == CharacterTab.Status);
        SetButtonColor(equipmentButton, currentTab == CharacterTab.Equipment);
        SetButtonColor(inventoryButton, currentTab == CharacterTab.Inventory);
        SetButtonColor(skillsButton, currentTab == CharacterTab.Skills);
    }

    void RefreshActionButtons()
    {
        if (equipmentActionParent == null) return;

        foreach (Transform child in equipmentActionParent)
            Destroy(child.gameObject);

        bool show = isOpen && (currentTab == CharacterTab.Equipment || currentTab == CharacterTab.Inventory || currentTab == CharacterTab.Skills);
        equipmentActionParent.gameObject.SetActive(show);
        if (!show) return;

        if (currentTab == CharacterTab.Skills)
        {
            RefreshSkillButtons();
            return;
        }

        if (currentTab == CharacterTab.Inventory)
        {
            RefreshInventoryButtons();
            return;
        }

        var eq = EquipmentSystem.Instance;
        if (eq == null || eq.ownedEquipment.Count == 0) return;

        int buttonIndex = 0;
        foreach (var equipment in eq.ownedEquipment)
        {
            if (equipment == null) continue;
            CreateEquipmentActionButton(equipmentActionParent, equipment, buttonIndex++);
        }
    }

    void RefreshInventoryButtons()
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null || inventory.items.Count == 0) return;

        for (int i = 0; i < inventory.items.Count; i++)
        {
            var item = inventory.items[i];
            if (item == null) continue;
            CreateInventoryActionButton(equipmentActionParent, item, i);
        }
    }

    void CreateInventoryActionButton(Transform parent, ItemData item, int index)
    {
        var buttonObject = CreateUIObject(item.itemName + "UseButton", parent);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * 38f);
        rect.sizeDelta = new Vector2(0f, 32f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.22f, 0.34f, 0.26f, 0.95f);

        var button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            var stats = FindAnyObjectByType<PlayerStats>();
            InventorySystem.Instance?.UseItem(item, stats);
            Refresh();
        });

        var text = CreateText("Text", buttonObject.transform, $"使用 {item.itemName}", 16, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    void RefreshSkillButtons()
    {
        var skills = SkillSystem.GetOrCreate();
        if (skills == null || skills.learnedSkills.Count == 0) return;

        for (int i = 0; i < skills.learnedSkills.Count; i++)
        {
            var skill = skills.learnedSkills[i];
            if (skill == null) continue;
            CreateSkillStatusButton(equipmentActionParent, skill, i);
        }
    }

    void CreateSkillStatusButton(Transform parent, SkillData skill, int index)
    {
        var buttonObject = CreateUIObject(skill.skillName + "LearnedButton", parent);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * 38f);
        rect.sizeDelta = new Vector2(0f, 32f);

        var image = buttonObject.AddComponent<Image>();
        image.color = skill.type == SkillType.Active
            ? new Color(0.26f, 0.28f, 0.44f, 0.95f)
            : new Color(0.26f, 0.42f, 0.28f, 0.95f);

        var button = buttonObject.AddComponent<Button>();
        button.interactable = false;

        var text = CreateText("Text", buttonObject.transform, $"已学 {skill.skillName}", 16, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    void CreateEquipmentActionButton(Transform parent, EquipmentData equipment, int index)
    {
        var buttonObject = CreateUIObject(equipment.equipmentName + "Button", parent);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * 38f);
        rect.sizeDelta = new Vector2(0f, 32f);

        var image = buttonObject.AddComponent<Image>();
        var eq = EquipmentSystem.Instance;
        bool equipped = eq != null && IsEquipped(eq, equipment);
        image.color = equipped ? new Color(0.26f, 0.42f, 0.28f, 0.95f) : new Color(0.2f, 0.23f, 0.3f, 0.95f);

        var button = buttonObject.AddComponent<Button>();
        button.interactable = !equipped;
        button.onClick.AddListener(() =>
        {
            EquipmentSystem.Instance?.EquipOwned(equipment);
            Refresh();
        });

        string prefix = equipped ? "已装备" : "装备";
        var text = CreateText("Text", buttonObject.transform, $"{prefix} {equipment.equipmentName}", 16, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    bool IsEquipped(EquipmentSystem eq, EquipmentData equipment)
    {
        return eq.weapon == equipment || eq.armor == equipment || eq.accessory1 == equipment || eq.accessory2 == equipment;
    }

    string GetSlotName(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return "武器";
            case EquipmentSlot.Armor: return "防具";
            case EquipmentSlot.Accessory: return "饰品";
            default: return slot.ToString();
        }
    }

    void SetButtonColor(Button button, bool selected)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image == null) return;
        image.color = selected ? new Color(0.35f, 0.45f, 0.62f, 0.95f) : new Color(0.18f, 0.2f, 0.25f, 0.95f);
    }

    void Subscribe()
    {
        if (EquipmentSystem.Instance != null) EquipmentSystem.Instance.OnEquipmentChanged += Refresh;
        if (InventorySystem.Instance != null) InventorySystem.Instance.OnInventoryChanged += Refresh;
        SkillSystem.GetOrCreate().OnSkillsChanged += Refresh;
    }

    void Unsubscribe()
    {
        if (EquipmentSystem.Instance != null) EquipmentSystem.Instance.OnEquipmentChanged -= Refresh;
        if (InventorySystem.Instance != null) InventorySystem.Instance.OnInventoryChanged -= Refresh;
        if (SkillSystem.Instance != null) SkillSystem.Instance.OnSkillsChanged -= Refresh;
    }

    void BindButtons()
    {
        if (statusButton != null) statusButton.onClick.AddListener(() => SetTab(CharacterTab.Status));
        if (equipmentButton != null) equipmentButton.onClick.AddListener(() => SetTab(CharacterTab.Equipment));
        if (inventoryButton != null) inventoryButton.onClick.AddListener(() => SetTab(CharacterTab.Inventory));
        if (skillsButton != null) skillsButton.onClick.AddListener(() => SetTab(CharacterTab.Skills));
    }

    void EnsureUI()
    {
        if (panel == null)
            panel = CreatePanel(transform);
        else
            ConfigurePanel(panel);

        if (titleText == null || contentText == null || equipmentActionParent == null || statusButton == null || equipmentButton == null || inventoryButton == null || skillsButton == null)
            BuildGeneratedContent(panel.transform);
    }

    GameObject CreatePanel(Transform parent)
    {
        var createdPanel = CreateUIObject("CharacterPanel", parent);
        ConfigurePanel(createdPanel);
        return createdPanel;
    }

    void ConfigurePanel(GameObject targetPanel)
    {
        var rect = targetPanel.GetComponent<RectTransform>();
        if (rect == null) rect = targetPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = targetPanel.GetComponent<Image>();
        if (image == null) image = targetPanel.AddComponent<Image>();
        image.color = new Color(0.06f, 0.07f, 0.09f, 0.97f);
    }

    void BuildGeneratedContent(Transform parent)
    {
        var old = parent.Find("GeneratedCharacterMenu");
        if (old != null)
            Destroy(old.gameObject);

        var root = CreateUIObject("GeneratedCharacterMenu", parent);
        SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        titleText = CreateText("Title", root.transform, "人物信息", 34, TextAnchor.MiddleLeft);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(54f, -92f), new Vector2(-54f, -28f));

        var hintText = CreateText("Hint", root.transform, "C 关闭    1 状态    2 装备    3 背包    4 技能", 16, TextAnchor.MiddleLeft);
        hintText.color = new Color(0.66f, 0.7f, 0.78f, 1f);
        SetRect(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(54f, -126f), new Vector2(-54f, -96f));

        var tabRoot = CreateUIObject("Tabs", root.transform);
        SetRect(tabRoot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(54f, 56f), new Vector2(254f, -156f));
        statusButton = CreateTabButton(tabRoot.transform, "状态", 0);
        equipmentButton = CreateTabButton(tabRoot.transform, "装备", 1);
        inventoryButton = CreateTabButton(tabRoot.transform, "背包", 2);
        skillsButton = CreateTabButton(tabRoot.transform, "技能", 3);

        var contentBackground = CreateUIObject("ContentBackground", root.transform);
        var backgroundImage = contentBackground.AddComponent<Image>();
        backgroundImage.color = new Color(0.1f, 0.11f, 0.15f, 0.82f);
        SetRect(contentBackground.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(286f, 56f), new Vector2(-54f, -156f));

        contentText = CreateText("Content", contentBackground.transform, string.Empty, 20, TextAnchor.UpperLeft);
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        contentText.lineSpacing = 1.15f;
        SetRect(contentText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 190f), new Vector2(-28f, -24f));

        var equipmentActions = CreateUIObject("EquipmentActions", contentBackground.transform);
        equipmentActionParent = equipmentActions.transform;
        SetRect(equipmentActions.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 24f), new Vector2(-28f, 178f));
        equipmentActions.SetActive(false);
    }

    Button CreateTabButton(Transform parent, string label, int index)
    {
        var buttonObject = CreateUIObject(label + "Button", parent);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * 58f);
        rect.sizeDelta = new Vector2(0f, 46f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.2f, 0.25f, 0.95f);
        var button = buttonObject.AddComponent<Button>();

        var text = CreateText("Text", buttonObject.transform, label, 20, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    Text CreateText(string name, Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        var textObject = CreateUIObject(name, parent);
        var textComponent = textObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = new Color(0.92f, 0.93f, 0.95f, 1f);
        return textComponent;
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
