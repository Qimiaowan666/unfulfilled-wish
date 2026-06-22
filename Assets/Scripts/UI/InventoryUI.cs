using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("Legacy")]
    public bool allowLegacyToggle = false;

    public GameObject panel;
    public Transform slotParent;
    public GameObject slotPrefab;
    public Text hintText;

    bool isOpen;
    bool pausedByInventory;
    float previousTimeScale = 1f;

    void Start()
    {
        if (InventorySystem.Instance != null)
            InventorySystem.Instance.OnInventoryChanged += Refresh;
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (!allowLegacyToggle) return;

        var kb = Keyboard.current;
        if (ShopUI.IsOpen) return;
        if (kb != null && kb.iKey.wasPressedThisFrame)
            Toggle();
    }

    void Toggle()
    {
        isOpen = !isOpen;
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

    void OnDisable()
    {
        ResumeGameTime();
    }

    void Refresh()
    {
        if (slotParent == null || slotPrefab == null) return;

        foreach (Transform child in slotParent) Destroy(child.gameObject);

        var inventory = InventorySystem.Instance;
        if (inventory == null) return;

        if (hintText != null)
            hintText.text = "Left click: use consumable | Right click: discard | Drag: reorder";

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemData item = inventory.items[i];
            var slot = Instantiate(slotPrefab, slotParent);
            var img = slot.GetComponentInChildren<Image>();
            if (img != null && item.icon != null) img.sprite = item.icon;

            var slotUI = slot.GetComponent<InventorySlotUI>();
            if (slotUI == null) slotUI = slot.AddComponent<InventorySlotUI>();
            slotUI.Setup(this, item, i);
        }
    }

    public void UseItem(ItemData item)
    {
        var stats = FindAnyObjectByType<PlayerStats>();
        InventorySystem.Instance?.UseItem(item, stats);
    }

    public void DiscardItem(ItemData item)
    {
        InventorySystem.Instance?.DiscardItem(item);
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        InventorySystem.Instance?.MoveItem(fromIndex, toIndex);
    }

    void PauseGameTime()
    {
        if (pausedByInventory) return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedByInventory = true;
    }

    void ResumeGameTime()
    {
        if (!pausedByInventory) return;

        // 复原值钳到 >0,防止在别处已把 timeScale 置 0 时开背包、关背包又还原回 0 锁死游戏。
        Time.timeScale = GameManager.Instance != null && GameManager.Instance.IsPaused
            ? 0f
            : (previousTimeScale > 0f ? previousTimeScale : 1f);

        pausedByInventory = false;
    }
}
