using UnityEngine;

public abstract class CharacterPanelPage
{
    public GameObject Root { get; protected set; }

    public abstract void Build(Transform parent, CharacterPanelUIFactory ui);
    public abstract void Refresh();

    public virtual void Show()
    {
        if (Root != null) Root.SetActive(true);
        Refresh();
    }

    public virtual void Hide()
    {
        if (Root != null) Root.SetActive(false);
    }
}
