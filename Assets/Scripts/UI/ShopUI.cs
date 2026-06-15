using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 商店界面（View 版）：布局在 Assets/Prefabs/UI/ShopUI.prefab 里摆好（木框风），常驻 Bootstrap。
// 这个脚本只引用节点 + 跑逻辑；由 ShopNPCTrigger 调 ShopUI.Instance.Open(shop) 打开。
public class ShopUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static ShopUI Instance { get; private set; }

    [Header("根 / 标题")]
    public GameObject panelRoot;     // overlay 开关根
    public TMP_Text titleText;
    public TMP_Text goldText;

    [Header("列表")]
    public Transform listContent;    // ScrollRect 的 Content（VerticalLayoutGroup）
    public ShopRowView rowTemplate;  // 模板行（默认隐藏，运行时克隆）

    [Header("按钮")]
    public Button exitButton;

    [Header("详情确认弹窗")]
    public GameObject detailPopup;     // 弹窗根（默认隐藏）
    public Image      detailIcon;
    public TMP_Text   detailName;
    public TMP_Text   detailDesc;
    public TMP_Text   detailPrice;
    public Button     buyButton;       // 确认购买
    public TMP_Text   buyButtonLabel;
    public Button     cancelButton;    // 取消
    public TMP_Text   hintText;        // 失败提示（默认隐藏）

    static readonly Color GoldColor = new Color(0.86f, 0.66f, 0.27f, 1f);
    static readonly Color PoorColor = new Color(0.70f, 0.32f, 0.28f, 1f);

    ShopSystem  shop;
    PlayerStats stats;
    bool  pausedByShop;
    float previousTimeScale = 1f;
    System.Action reselect;   // 重新展示当前选中项（购买后刷新金币/可购性）

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        IsOpen = false;
        pausedByShop = false;
    }

    void Start()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); Close(); });
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); ClearDetail(); });
        }
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (detailPopup != null) detailPopup.SetActive(true);   // 详情面板常驻显示
        ClearDetail();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open(ShopSystem shopSystem)
    {
        shop  = shopSystem;
        stats = FindAnyObjectByType<PlayerStats>();
        IsOpen = true;
        AudioManager.Instance?.PlayUIOpen();   // 开商店
        PlayerInputBuffer.ClearAll();
        if (panelRoot != null) panelRoot.SetActive(true);
        if (detailPopup != null) detailPopup.SetActive(true);
        ClearDetail();
        PauseGameTime();
        Refresh();
    }

    public void Close()
    {
        PlayerInputBuffer.ClearAll();
        IsOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        ResumeGameTime();
    }

    void OnDisable()
    {
        if (IsOpen) Close();
    }

    void Refresh()
    {
        if (shop == null || listContent == null || rowTemplate == null) return;

        // 清掉旧行（保留模板）
        foreach (Transform child in listContent)
            if (child != rowTemplate.transform) Destroy(child.gameObject);

        if (goldText != null) goldText.text = stats != null ? $"金币 {stats.gold}" : "金币 0";

        foreach (var entry in shop.AvailableItems)
        {
            var e = entry;
            AddRow(e.item.itemName, e.item.description, e.item.price, e.quantity, e.item.icon, () => shop.BuyItem(e, stats));
        }
        foreach (var entry in shop.AvailableEquipment)
        {
            var e = entry;
            AddRow(e.equipment.equipmentName, e.equipment.description, e.equipment.price, e.quantity, e.equipment.icon, () => shop.BuyEquipment(e, stats));
        }
        foreach (var entry in shop.AvailableSkills)
        {
            var e = entry;
            AddRow(e.skill.skillName, e.skill.description, e.skill.price, e.quantity, e.skill.icon, () => shop.BuySkill(e, stats));
        }
    }

    // 点一行 → 不直接买，弹出详情确认框
    void AddRow(string label, string desc, int price, int quantity, Sprite icon, System.Func<bool> buyAction)
    {
        var row = Instantiate(rowTemplate, listContent);
        row.gameObject.SetActive(true);
        bool canAfford = stats != null && stats.gold >= price;
        row.Setup(label, shop.FormatPrice(price, quantity), icon, canAfford,
            () => ShowDetail(label, desc, price, quantity, icon, buyAction));
    }

    // 选中一行 → 把详情填进常驻的右侧面板（不再开关弹窗）
    void ShowDetail(string label, string desc, int price, int quantity, Sprite icon, System.Func<bool> buyAction)
    {
        AudioManager.Instance?.PlayUIClick();
        reselect = () => ShowDetail(label, desc, price, quantity, icon, buyAction);

        bool canAfford = stats != null && stats.gold >= price;
        if (detailIcon != null) { detailIcon.sprite = icon; detailIcon.enabled = icon != null; }
        if (detailName != null) detailName.text = label;
        if (detailDesc != null) detailDesc.text = string.IsNullOrEmpty(desc) ? "（无描述）" : desc;
        if (detailPrice != null)
        {
            detailPrice.text  = shop.FormatPrice(price, quantity);
            detailPrice.color = canAfford ? GoldColor : PoorColor;
        }
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (buyButtonLabel != null) buyButtonLabel.text = "购买";

        if (buyButton != null)
        {
            buyButton.interactable = true;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayUIClick();
                bool bought = buyAction();
                if (bought)
                {
                    AudioManager.Instance?.PlayShopBuy();
                    Refresh();           // 刷新列表 + 金币
                    reselect?.Invoke();  // 重填详情，更新金币/价格颜色，可连续购买
                }
                else
                {
                    AudioManager.Instance?.PlayShopFail();
                    if (hintText != null)
                    {
                        hintText.text = (stats != null && stats.gold < price) ? "金币不足" : "无法购买";
                        hintText.gameObject.SetActive(true);
                    }
                }
            });
        }
    }

    // 未选中 / 取消 / 刚打开：详情面板显示占位，购买键禁用
    void ClearDetail()
    {
        reselect = null;
        if (detailIcon != null) { detailIcon.sprite = null; detailIcon.enabled = false; }
        if (detailName != null) detailName.text = "选择商品查看详情";
        if (detailDesc != null) detailDesc.text = "";
        if (detailPrice != null) detailPrice.text = "";
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (buyButtonLabel != null) buyButtonLabel.text = "购买";
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.interactable = false;
        }
    }

    void PauseGameTime()
    {
        if (pausedByShop) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedByShop = true;
    }

    void ResumeGameTime()
    {
        if (!pausedByShop) return;
        Time.timeScale = GameManager.Instance != null && GameManager.Instance.IsPaused ? 0f : previousTimeScale;
        pausedByShop = false;
    }
}
