using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public GameObject panel;
    public Transform slotParent;
    public GameObject slotPrefab;
    public Text hintText;

    bool isOpen;

    void Start()
    {
        if (InventorySystem.Instance != null)
            InventorySystem.Instance.OnInventoryChanged += Refresh;
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.iKey.wasPressedThisFrame)
            Toggle();
    }

    void Toggle()
    {
        isOpen = !isOpen;
        if (panel != null) panel.SetActive(isOpen);
        if (isOpen) Refresh();
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
}
