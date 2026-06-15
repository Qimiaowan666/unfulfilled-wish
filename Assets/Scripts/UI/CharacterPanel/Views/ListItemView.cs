using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 列表行 prefab 的绑定脚本（装备列表 / 技能列表 / 左侧槽位共用）。
// 布局在 Editor 里画好，这里只负责被填数据。左键走 Button.onClick；右键走 IPointerClickHandler。
public class ListItemView : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("图标 Image")]              public Image icon;
    [Tooltip("主标题")]                  public TMP_Text titleText;
    [Tooltip("副标题（属性/冷却等）")]    public TMP_Text subtitleText;
    [Tooltip("选中高亮（默认隐藏）")]      public GameObject selectedFrame;
    [Tooltip("整行的 Button")]           public Button button;

    Action rightClick;

    public void Setup(string title, string subtitle, Sprite iconSprite, bool selected, Action onClick, Action onRightClick = null)
    {
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (selectedFrame != null) selectedFrame.SetActive(selected);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
        rightClick = onRightClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) rightClick?.Invoke();
    }
}
