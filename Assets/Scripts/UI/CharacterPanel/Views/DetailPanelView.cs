using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 右栏详情面板组件。布局在 prefab 里画好，各页 View 调这些方法填数据。
// 属性行用 statRowPrefab（StatRow.prefab）动态 Instantiate。
public class DetailPanelView : MonoBehaviour
{
    public Image icon;
    public TMP_Text title;
    public TMP_Text description;
    public Transform statRows;          // 属性行容器（VerticalLayoutGroup）
    public GameObject statRowPrefab;    // StatRow.prefab
    public Button actionButton;
    public TMP_Text actionLabel;

    public void ShowEmpty(string emptyTitle, string emptyDesc)
    {
        if (title != null) title.text = emptyTitle;
        if (description != null) description.text = emptyDesc;
        if (icon != null) icon.enabled = false;
        ClearStats();
        if (actionButton != null) actionButton.gameObject.SetActive(false);
    }

    public void SetHeader(string t, string desc, Sprite ic)
    {
        if (title != null) title.text = t;
        if (description != null) description.text = desc;
        if (icon != null) { icon.sprite = ic; icon.enabled = ic != null; }
    }

    public void ClearStats()
    {
        if (statRows == null) return;
        for (int i = statRows.childCount - 1; i >= 0; i--)
            Destroy(statRows.GetChild(i).gameObject);
    }

    public void AddStat(string label, string value)
    {
        if (statRows == null || statRowPrefab == null) return;
        var go = Instantiate(statRowPrefab, statRows);
        var row = go.GetComponent<StatRowView>();
        if (row != null) row.Setup(label, value);
    }

    public void SetAction(string label, bool interactable, Action onClick)
    {
        if (actionButton == null) return;
        actionButton.gameObject.SetActive(true);
        if (actionLabel != null) actionLabel.text = label;
        actionButton.interactable = interactable;
        actionButton.onClick.RemoveAllListeners();
        if (onClick != null) actionButton.onClick.AddListener(() => onClick());
    }

    public void HideAction()
    {
        if (actionButton != null) actionButton.gameObject.SetActive(false);
    }
}
