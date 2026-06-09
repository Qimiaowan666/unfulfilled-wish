using UnityEngine;
using UnityEngine.UI;

// 商店界面：常驻单例（跟 PauseUI 同类全局 UI），程序化构建。
// 由 ShopNPCTrigger 调 Open(shop) 打开。shopSlotPrefab 用现成的 ShopSlot.prefab（Bootstrap Inspector 拖入）。
public class ShopUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static ShopUI Instance { get; private set; }

    [Tooltip("商品行 prefab（拖 Assets/Prefabs/ShopSlot.prefab）")]
    public GameObject shopSlotPrefab;

    static readonly Color OverlayColor = new Color(0.03f, 0.02f, 0.02f, 0.72f);
    static readonly Color PanelColor   = new Color(0.12f, 0.10f, 0.09f, 0.98f);

    GameObject overlay;
    GameObject panel;
    Transform  itemListParent;
    Text       goldText;
    Font       uiFont;
    bool       uiBuilt;

    ShopSystem  shop;
    PlayerStats stats;
    bool  pausedByShop;
    float previousTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        IsOpen = false;
        pausedByShop = false;
    }

    public void Open(ShopSystem shopSystem)
    {
        EnsureUI();
        shop  = shopSystem;
        stats = FindAnyObjectByType<PlayerStats>();
        IsOpen = true;
        PlayerInputBuffer.ClearAll();
        if (overlay != null) overlay.SetActive(true);
        PauseGameTime();
        Refresh();
    }

    public void Close()
    {
        PlayerInputBuffer.ClearAll();
        IsOpen = false;
        if (overlay != null) overlay.SetActive(false);
        ResumeGameTime();
    }

    void OnDisable()
    {
        if (IsOpen) Close();
    }

    void Refresh()
    {
        if (shop == null || itemListParent == null || shopSlotPrefab == null) return;

        foreach (Transform child in itemListParent) Destroy(child.gameObject);
        if (goldText != null && stats != null) goldText.text = $"金币：{stats.gold}";

        foreach (var entry in shop.AvailableItems)
            AddSlot(entry.item.itemName, shop.FormatPrice(entry.item.price, entry.quantity), () => shop.BuyItem(entry, stats));

        foreach (var entry in shop.AvailableEquipment)
            AddSlot(entry.equipment.equipmentName, shop.FormatPrice(entry.equipment.price, entry.quantity), () => shop.BuyEquipment(entry, stats));

        foreach (var entry in shop.AvailableSkills)
            AddSlot(entry.skill.skillName, shop.FormatPrice(entry.skill.price, entry.quantity), () => shop.BuySkill(entry, stats));
    }

    void AddSlot(string label, string priceTextValue, System.Func<bool> buyAction, bool interactable = true)
    {
        var slot = Instantiate(shopSlotPrefab, itemListParent);
        Text nameText = null;
        Text priceText = null;
        foreach (var text in slot.GetComponentsInChildren<Text>())
        {
            if (text.name == "ItemName") nameText = text;
            else if (text.name == "PriceText") priceText = text;
        }

        if (nameText != null) nameText.text = label;
        if (priceText != null) priceText.text = priceTextValue;

        var btn = slot.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = interactable;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayUIClick();
                bool bought = buyAction();
                if (bought) AudioManager.Instance?.PlayShopBuy();
                else { AudioManager.Instance?.PlayShopFail(); Debug.LogWarning($"购买失败：{label}"); }
                Refresh();
            });
        }
    }

    // ── 程序化构建 UI ──────────────────────────────────────────────
    void EnsureUI()
    {
        if (uiBuilt) return;
        uiBuilt = true;

        EnsureHostCanvas();
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        overlay = CreateUIObject("ShopOverlay", transform);
        Stretch(overlay.GetComponent<RectTransform>());
        var bg = overlay.AddComponent<Image>();
        bg.color = OverlayColor;
        bg.raycastTarget = true;

        // 面板
        panel = CreateUIObject("ShopPanel", overlay.transform);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 720f));
        panel.AddComponent<Image>().color = PanelColor;

        var title = CreateText("Title", panel.transform, "商 店", 34, TextAnchor.UpperCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(560f, 50f));
        title.fontStyle = FontStyle.Bold;

        goldText = CreateText("Gold", panel.transform, "金币：0", 22, TextAnchor.UpperRight);
        SetRect(goldText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -72f), new Vector2(260f, 36f));
        goldText.color = new Color(1f, 0.86f, 0.4f, 1f);

        // 商品列表容器（竖排）
        var listGo = CreateUIObject("ItemList", panel.transform);
        SetRect(listGo.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(580f, 540f));
        var layout = listGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        itemListParent = listGo.transform;

        // 退出按钮
        BuildExitButton(panel.transform);

        overlay.SetActive(false);
    }

    void EnsureHostCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 950;   // 低于暂停(1000)/死亡(1100)，高于 HUD/角色面板(900)

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void BuildExitButton(Transform parent)
    {
        var go = CreateUIObject("ShopExitButton", parent);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(180f, 50f));
        go.AddComponent<Image>().color = new Color(0.30f, 0.20f, 0.18f, 0.95f);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Close);

        var text = CreateText("Text", go.transform, "退 出", 22, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.raycastTarget = false;
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

    // ── UI 小工具 ──────────────────────────────────────────────────
    GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor)
    {
        var go = CreateUIObject(name, parent);
        var text = go.AddComponent<Text>();
        text.text = value;
        text.font = uiFont;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
