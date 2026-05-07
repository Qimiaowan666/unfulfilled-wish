using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterPanelUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    enum CharacterTab
    {
        Status = 0,
        Equipment = 1,
        Inventory = 2,
        Skills = 3
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

    CharacterPanelUIFactory ui;
    CharacterPanelPage[] pages;
    PlayerStats subscribedStats;
    EquipmentSystem subscribedEquipment;
    InventorySystem subscribedInventory;
    SkillSystem subscribedSkills;
    bool isOpen;
    bool pausedByPanel;
    bool hasSubscribed;
    float previousTimeScale = 1f;
    CharacterTab currentTab = CharacterTab.Status;
    readonly List<GameObject> hiddenHudRoots = new List<GameObject>();
    readonly List<bool> hiddenHudPreviousStates = new List<bool>();

    void Awake()
    {
        isOpen = false;
        IsOpen = false;
        pausedByPanel = false;
    }

    void Start()
    {
        SetOpen(false);
    }

    void OnEnable()
    {
        if (isOpen)
            Subscribe();
    }

    void OnDisable()
    {
        if (pausedByPanel)
            ResumeGameTime();

        RestoreGameplayHud();
        if (isOpen)
        {
            isOpen = false;
            IsOpen = false;
        }

        Unsubscribe();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (!isOpen && ShopUI.IsOpen) return;

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
        if (!value && !isOpen)
        {
            IsOpen = false;
            RestoreGameplayHud();
            if (panel != null) panel.SetActive(false);
            return;
        }

        if (value && ShopUI.IsOpen) return;

        if (value)
        {
            BuildPages();
            BindButtons();
        }

        isOpen = value;
        IsOpen = value;
        PlayerInputBuffer.ClearAll();

        if (panel != null) panel.SetActive(isOpen);

        if (isOpen)
        {
            Resubscribe();
            HideGameplayHud();
            PauseGameTime();
            Refresh();
        }
        else
        {
            HideAllPages();
            ResumeGameTime();
            RestoreGameplayHud();
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
        if (pages == null) BuildPages();
        if (pages == null) return;

        if (titleText != null) titleText.text = GetTabTitle(currentTab);
        if (contentText != null) contentText.gameObject.SetActive(false);
        if (equipmentActionParent != null) equipmentActionParent.gameObject.SetActive(false);

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == null) continue;

            if (i == (int)currentTab)
                pages[i].Show();
            else
                pages[i].Hide();
        }

        RefreshLegacyFields();
        UpdateButtonStates();
    }

    void HideAllPages()
    {
        if (pages == null) return;

        foreach (var page in pages)
            page?.Hide();
    }

    string GetTabTitle(CharacterTab tab)
    {
        switch (tab)
        {
            case CharacterTab.Status: return "状态";
            case CharacterTab.Equipment: return "装备";
            case CharacterTab.Inventory: return "背包";
            case CharacterTab.Skills: return "技能";
            default: return string.Empty;
        }
    }

    void RefreshLegacyFields()
    {
        SetLegacyObjectActive(hpText, false);
        SetLegacyObjectActive(attackText, false);
        SetLegacyObjectActive(defenseText, false);
        SetLegacyObjectActive(goldText, false);
        SetLegacyObjectActive(weaponIcon, false);
        SetLegacyObjectActive(armorIcon, false);
        SetLegacyObjectActive(accessory1Icon, false);
        SetLegacyObjectActive(accessory2Icon, false);
    }

    void SetLegacyObjectActive(Graphic graphic, bool value)
    {
        if (graphic != null)
            graphic.gameObject.SetActive(value);
    }

    void UpdateButtonStates()
    {
        ui?.SetButtonState(statusButton, currentTab == CharacterTab.Status);
        ui?.SetButtonState(equipmentButton, currentTab == CharacterTab.Equipment);
        ui?.SetButtonState(inventoryButton, currentTab == CharacterTab.Inventory);
        ui?.SetButtonState(skillsButton, currentTab == CharacterTab.Skills);
    }

    void Subscribe()
    {
        if (!isActiveAndEnabled || hasSubscribed) return;

        subscribedStats = FindAnyObjectByType<PlayerStats>();
        if (subscribedStats != null) subscribedStats.OnStatsChanged += Refresh;

        subscribedEquipment = EquipmentSystem.Instance != null
            ? EquipmentSystem.Instance
            : FindAnyObjectByType<EquipmentSystem>();
        if (subscribedEquipment != null) subscribedEquipment.OnEquipmentChanged += Refresh;

        subscribedInventory = InventorySystem.Instance != null
            ? InventorySystem.Instance
            : FindAnyObjectByType<InventorySystem>();
        if (subscribedInventory != null) subscribedInventory.OnInventoryChanged += Refresh;

        subscribedSkills = SkillSystem.Instance != null
            ? SkillSystem.Instance
            : FindAnyObjectByType<SkillSystem>();
        if (subscribedSkills != null) subscribedSkills.OnSkillsChanged += Refresh;

        hasSubscribed = true;
    }

    void Resubscribe()
    {
        Unsubscribe();
        Subscribe();
    }

    void Unsubscribe()
    {
        if (!hasSubscribed) return;

        if (subscribedStats != null) subscribedStats.OnStatsChanged -= Refresh;
        subscribedStats = null;

        if (subscribedEquipment != null) subscribedEquipment.OnEquipmentChanged -= Refresh;
        subscribedEquipment = null;

        if (subscribedInventory != null) subscribedInventory.OnInventoryChanged -= Refresh;
        subscribedInventory = null;

        if (subscribedSkills != null) subscribedSkills.OnSkillsChanged -= Refresh;
        subscribedSkills = null;

        hasSubscribed = false;
    }

    void BindButtons()
    {
        BindTabButton(statusButton, CharacterTab.Status);
        BindTabButton(equipmentButton, CharacterTab.Equipment);
        BindTabButton(inventoryButton, CharacterTab.Inventory);
        BindTabButton(skillsButton, CharacterTab.Skills);
    }

    void BindTabButton(Button button, CharacterTab tab)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SetTab(tab));
    }

    void EnsureUI()
    {
        ui = new CharacterPanelUIFactory();

        if (panel == null)
            panel = CreatePanel(transform);
        else
            ConfigurePanel(panel);

        BuildGeneratedContent(panel.transform);
    }

    GameObject CreatePanel(Transform parent)
    {
        var createdPanel = new GameObject("CharacterPanel", typeof(RectTransform));
        createdPanel.transform.SetParent(parent, false);
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
        image.color = new Color(0.05f, 0.06f, 0.08f, 0.99f);
    }

    void HideGameplayHud()
    {
        RestoreGameplayHud();

        string[] names =
        {
            "HUD_Canvas",
            "HUD",
            "CombatHUDCanvas",
            "ExecutePromptCanvas",
            "TeleportCanvas"
        };

        foreach (string hudName in names)
        {
            GameObject hud = GameObject.Find(hudName);
            if (hud == null || hud == panel) continue;

            hiddenHudRoots.Add(hud);
            hiddenHudPreviousStates.Add(hud.activeSelf);
            hud.SetActive(false);
        }
    }

    void RestoreGameplayHud()
    {
        for (int i = 0; i < hiddenHudRoots.Count; i++)
        {
            if (hiddenHudRoots[i] != null)
                hiddenHudRoots[i].SetActive(hiddenHudPreviousStates[i]);
        }

        hiddenHudRoots.Clear();
        hiddenHudPreviousStates.Clear();
    }

    void BuildGeneratedContent(Transform parent)
    {
        var old = parent.Find("GeneratedCharacterMenu");
        if (old != null)
            Destroy(old.gameObject);

        var root = ui.CreateUIObject("GeneratedCharacterMenu", parent);
        ui.SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        titleText = ui.CreateText("Title", root.transform, "人物信息", 34, TextAnchor.MiddleLeft);
        ui.SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(54f, -92f), new Vector2(-54f, -28f));

        var hintText = ui.CreateText("Hint", root.transform, "C 关闭    1 状态    2 装备    3 背包    4 技能", 16, TextAnchor.MiddleLeft);
        hintText.color = ui.MutedTextColor;
        ui.SetRect(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(54f, -126f), new Vector2(-54f, -96f));

        var tabRoot = ui.CreateUIObject("Tabs", root.transform);
        ui.SetRect(tabRoot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(54f, 56f), new Vector2(254f, -156f));
        statusButton = CreateTabButton(tabRoot.transform, "状态", 0);
        equipmentButton = CreateTabButton(tabRoot.transform, "装备", 1);
        inventoryButton = CreateTabButton(tabRoot.transform, "背包", 2);
        skillsButton = CreateTabButton(tabRoot.transform, "技能", 3);

        var contentBackground = ui.CreatePanel("ContentBackground", root.transform, ui.PanelColor);
        ui.SetRect(contentBackground.rectTransform, new Vector2(0f, 0f), Vector2.one, new Vector2(286f, 56f), new Vector2(-54f, -156f));

        contentText = ui.CreateText("Content", contentBackground.transform, string.Empty, 20, TextAnchor.UpperLeft);
        ui.SetRect(contentText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 24f), new Vector2(-28f, -24f));
        contentText.gameObject.SetActive(false);

        var equipmentActions = ui.CreateUIObject("EquipmentActions", contentBackground.transform);
        equipmentActionParent = equipmentActions.transform;
        ui.SetRect(equipmentActions.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 24f), new Vector2(-28f, 178f));
        equipmentActions.SetActive(false);

        pages = new CharacterPanelPage[]
        {
            new CharacterStatusPageUI(),
            new CharacterEquipmentPageUI(),
            new CharacterInventoryPageUI(),
            new CharacterSkillsPageUI()
        };

        foreach (var page in pages)
            page.Build(contentBackground.transform, ui);
    }

    void BuildPages()
    {
        if (pages != null) return;
        EnsureUI();
    }

    Button CreateTabButton(Transform parent, string label, int index)
    {
        var button = ui.CreateButton(label + "Button", parent, label, 20);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * 58f);
        rect.sizeDelta = new Vector2(0f, 46f);
        return button;
    }
}
