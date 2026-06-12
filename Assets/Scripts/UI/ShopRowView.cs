using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 商店一行：图标 + 名称 + 价格（右对齐金币色）+ 整行购买按钮。
// 布局在 ShopUI.prefab 的 ShopRow 模板里画好，这里只负责被填数据。
public class ShopRowView : MonoBehaviour
{
    [Tooltip("图标 Image")]            public Image icon;
    [Tooltip("名称（左，省略号截断）")] public TMP_Text nameText;
    [Tooltip("价格（右对齐）")]         public TMP_Text priceText;
    [Tooltip("整行购买 Button")]       public Button button;

    static readonly Color Affordable = new Color(0.86f, 0.66f, 0.27f, 1f);  // 买得起：金币色
    static readonly Color TooPoor    = new Color(0.70f, 0.32f, 0.28f, 1f);  // 买不起：暗红

    public void Setup(string label, string price, Sprite iconSprite, bool canAfford, Action onBuy)
    {
        if (nameText != null) nameText.text = label;
        if (priceText != null)
        {
            priceText.text = price;
            priceText.color = canAfford ? Affordable : TooPoor;
        }
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onBuy != null) button.onClick.AddListener(() => onBuy());
        }
    }
}
