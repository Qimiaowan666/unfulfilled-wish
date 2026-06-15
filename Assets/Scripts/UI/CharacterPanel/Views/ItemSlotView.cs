using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 背包格子 prefab 的绑定脚本。布局在 Editor 里画好，这里只负责被填数据。
// Editor 里把子节点拖到下面的字段。左键走 Button.onClick；右键走 IPointerClickHandler；悬停走 SetHover。
public class ItemSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("物品图标 Image")]            public Image icon;
    [Tooltip("数量文字（>1 才显示）")]      public TMP_Text quantityText;
    [Tooltip("选中高亮（默认隐藏）")]        public GameObject selectedFrame;
    [Tooltip("整格的 Button")]             public Button button;

    Action rightClick, hoverEnter, hoverExit;

    public void Setup(Sprite iconSprite, int quantity, bool selected, Action onClick, Action onRightClick = null)
    {
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (quantityText != null)
            quantityText.text = quantity > 1 ? quantity.ToString() : string.Empty;
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
        rightClick = onRightClick;
        hoverEnter = null; hoverExit = null;   // 每次刷新清掉悬停回调，避免残留
    }

    // 选槽预览用：悬停进入/离开（Setup 后再调；不需要时不调即可）
    public void SetHover(Action onEnter, Action onExit) { hoverEnter = onEnter; hoverExit = onExit; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) rightClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData) { hoverEnter?.Invoke(); }
    public void OnPointerExit(PointerEventData eventData) { hoverExit?.Invoke(); }
}
