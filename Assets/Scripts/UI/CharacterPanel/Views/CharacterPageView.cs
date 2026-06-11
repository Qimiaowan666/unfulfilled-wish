using UnityEngine;

// 角色面板各页 View 的基类（挂在 prefab 的页节点上）。
// 布局在 prefab 里画好，子类只实现 Refresh() 填数据。
public abstract class CharacterPageView : MonoBehaviour
{
    public abstract void Refresh();

    public void Show() { gameObject.SetActive(true); Refresh(); }
    public void Hide() { gameObject.SetActive(false); }

    protected static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.Destroy(t.GetChild(i).gameObject);
    }
}
