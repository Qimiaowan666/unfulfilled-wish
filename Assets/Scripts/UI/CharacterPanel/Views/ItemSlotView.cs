using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 背包格子 prefab 的绑定脚本。布局在 Editor 里画好，这里只负责被填数据。
// Editor 里把子节点拖到下面的字段。
public class ItemSlotView : MonoBehaviour
{
    [Tooltip("物品图标 Image")]            public Image icon;
    [Tooltip("数量文字（>1 才显示）")]      public TMP_Text quantityText;
    [Tooltip("选中高亮（默认隐藏）")]        public GameObject selectedFrame;
    [Tooltip("整格的 Button")]             public Button button;

    public void Setup(Sprite iconSprite, int quantity, bool selected, Action onClick)
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
    }
}
