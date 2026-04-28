using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    public GameObject panel;
    public Transform itemListParent;
    public GameObject shopSlotPrefab;
    public Text goldText;
    public Button exitButton;

    ShopSystem shop;
    PlayerStats stats;
    bool pausedByShop;
    float previousTimeScale = 1f;

    void Start()
    {
        EnsureExitButton();
    }

    public void Open(ShopSystem shopSystem)
    {
        shop = shopSystem;
        stats = FindAnyObjectByType<PlayerStats>();
        IsOpen = true;
        PlayerInputBuffer.ClearAll();
        EnsureExitButton();
        if (panel != null) panel.SetActive(true);
        PauseGameTime();
        Refresh();
    }

    public void Close()
    {
        PlayerInputBuffer.ClearAll();
        IsOpen = false;
        if (panel != null) panel.SetActive(false);
        ResumeGameTime();
    }

    void OnDisable()
    {
        if (IsOpen) Close();
    }

    void Refresh()
    {
        if (shop == null) return;
        if (itemListParent == null || shopSlotPrefab == null) return;

        foreach (Transform child in itemListParent) Destroy(child.gameObject);
        if (goldText && stats != null) goldText.text = $"Gold: {stats.gold}";

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
                bool bought = buyAction();
                if (!bought)
                    Debug.LogWarning($"购买失败：{label}");
                Refresh();
            });
        }
    }

    void EnsureExitButton()
    {
        if (panel == null) return;

        if (exitButton == null)
            exitButton = FindExistingExitButton();

        if (exitButton == null)
            exitButton = CreateExitButton();

        exitButton.onClick.RemoveListener(Close);
        exitButton.onClick.AddListener(Close);
    }

    Button FindExistingExitButton()
    {
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            string lowerName = button.name.ToLowerInvariant();
            if (lowerName.Contains("exit") || lowerName.Contains("close") || button.name.Contains("退出"))
                return button;
        }

        return null;
    }

    Button CreateExitButton()
    {
        var buttonObject = new GameObject("ShopExitButton", typeof(RectTransform));
        buttonObject.transform.SetParent(panel.transform, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(92f, 36f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.18f, 0.18f, 0.95f);

        var button = buttonObject.AddComponent<Button>();

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObject.AddComponent<Text>();
        text.text = "退出";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return button;
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

        Time.timeScale = GameManager.Instance != null && GameManager.Instance.IsPaused
            ? 0f
            : previousTimeScale;

        pausedByShop = false;
    }
}
