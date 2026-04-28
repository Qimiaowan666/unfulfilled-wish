using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    static InventorySlotUI draggedSlot;

    InventoryUI inventoryUI;
    ItemData item;
    int index;
    CanvasGroup canvasGroup;

    public void Setup(InventoryUI owner, ItemData slotItem, int slotIndex)
    {
        inventoryUI = owner;
        item = slotItem;
        index = slotIndex;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            inventoryUI.UseItem(item);
        else if (eventData.button == PointerEventData.InputButton.Right)
            inventoryUI.DiscardItem(item);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggedSlot = this;
        if (canvasGroup != null) canvasGroup.alpha = 0.55f;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (draggedSlot == this) draggedSlot = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggedSlot == null || draggedSlot == this) return;
        inventoryUI.MoveItem(draggedSlot.index, index);
    }
}
