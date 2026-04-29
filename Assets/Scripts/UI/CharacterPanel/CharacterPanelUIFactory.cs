using UnityEngine;
using UnityEngine.UI;

public class CharacterPanelUIFactory
{
    public readonly Color PanelColor = new Color(0.10f, 0.11f, 0.15f, 0.98f);
    public readonly Color PanelAltColor = new Color(0.14f, 0.16f, 0.21f, 0.98f);
    public readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f, 1f);
    public readonly Color MutedTextColor = new Color(0.68f, 0.72f, 0.80f, 1f);
    public readonly Color ButtonColor = new Color(0.20f, 0.24f, 0.32f, 0.95f);
    public readonly Color ButtonSelectedColor = new Color(0.35f, 0.45f, 0.62f, 0.95f);
    public readonly Color ButtonDisabledColor = new Color(0.16f, 0.17f, 0.20f, 0.75f);

    readonly Font font;

    public CharacterPanelUIFactory()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public GameObject CreateRoot(string name, Transform parent)
    {
        var root = CreateUIObject(name, parent);
        SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.SetActive(false);
        return root;
    }

    public Image CreatePanel(string name, Transform parent, Color color)
    {
        var go = CreateUIObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public Text CreateText(string name, Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        var go = CreateUIObject(name, parent);
        var textComponent = go.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = TextColor;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        return textComponent;
    }

    public Button CreateButton(string name, Transform parent, string label, int fontSize = 16)
    {
        var go = CreateUIObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = ButtonColor;

        var button = go.AddComponent<Button>();
        var text = CreateText("Text", go.transform, label, fontSize, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        return button;
    }

    public Image CreateDivider(string name, Transform parent)
    {
        var image = CreatePanel(name, parent, new Color(0.55f, 0.58f, 0.66f, 0.32f));
        image.rectTransform.sizeDelta = new Vector2(0f, 1f);
        return image;
    }

    public CharacterIconBlock CreateIconBlock(string name, Transform parent, Vector2 size)
    {
        var root = CreatePanel(name, parent, new Color(0.18f, 0.20f, 0.26f, 0.95f));
        root.rectTransform.sizeDelta = size;

        var icon = CreatePanel("Icon", root.transform, Color.white);
        SetRect(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        icon.preserveAspect = true;

        var placeholder = CreateText("Placeholder", root.transform, "", 22, TextAnchor.MiddleCenter);
        placeholder.fontStyle = FontStyle.Bold;
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return new CharacterIconBlock(root, icon, placeholder);
    }

    public ScrollRect CreateScrollArea(string name, Transform parent, out Transform content)
    {
        var viewportImage = CreatePanel(name, parent, new Color(0.07f, 0.08f, 0.11f, 0.45f));
        var viewport = viewportImage.gameObject;
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObject = CreateUIObject("Content", viewport.transform);
        var contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var scrollRect = viewport.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 18f;

        content = contentObject.transform;
        return scrollRect;
    }

    public void ConfigureVerticalContent(Transform content, float spacing = 8f, float padding = 0f)
    {
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = spacing;
        layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public GridLayoutGroup ConfigureGridContent(Transform content, Vector2 cellSize, Vector2 spacing, int columns)
    {
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return grid;
    }

    public void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    public void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public void SetFixedHeight(RectTransform rect, float height)
    {
        var layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = rect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.minHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    public void SetButtonState(Button button, bool selected)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null) image.color = selected ? ButtonSelectedColor : ButtonColor;
    }

    public void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null) return;
        button.interactable = interactable;
        var image = button.GetComponent<Image>();
        if (image != null) image.color = interactable ? ButtonColor : ButtonDisabledColor;
    }

    public Color GetEquipmentColor(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return new Color(0.62f, 0.25f, 0.18f, 1f);
            case EquipmentSlot.Armor: return new Color(0.22f, 0.36f, 0.56f, 1f);
            case EquipmentSlot.Accessory: return new Color(0.50f, 0.32f, 0.65f, 1f);
            default: return PanelAltColor;
        }
    }

    public string GetEquipmentPlaceholder(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return "武";
            case EquipmentSlot.Armor: return "甲";
            case EquipmentSlot.Accessory: return "饰";
            default: return "装";
        }
    }

    public Color GetItemColor(ItemData item)
    {
        if (item != null && item.type == ItemType.Passive)
            return new Color(0.24f, 0.50f, 0.52f, 1f);

        return new Color(0.22f, 0.50f, 0.28f, 1f);
    }

    public string GetItemPlaceholder(ItemData item)
    {
        if (item != null && item.type == ItemType.Passive) return "物";
        return "药";
    }

    public Color GetSkillColor(SkillData skill)
    {
        if (skill != null && skill.type == SkillType.Passive)
            return new Color(0.22f, 0.40f, 0.26f, 0.96f);

        return new Color(0.24f, 0.30f, 0.52f, 0.96f);
    }

    public string GetSkillPlaceholder(SkillData skill)
    {
        if (skill != null && skill.type == SkillType.Passive) return "被";
        return "技";
    }

    public void SetIcon(CharacterIconBlock block, Sprite icon, Color placeholderColor, string placeholderText)
    {
        if (block.RootImage != null) block.RootImage.color = placeholderColor;

        bool hasIcon = icon != null;
        if (block.Icon != null)
        {
            block.Icon.sprite = icon;
            block.Icon.enabled = hasIcon;
        }

        if (block.PlaceholderText != null)
        {
            block.PlaceholderText.text = hasIcon ? string.Empty : placeholderText;
            block.PlaceholderText.gameObject.SetActive(!hasIcon);
        }
    }
}

public struct CharacterIconBlock
{
    public Image RootImage { get; }
    public Image Icon { get; }
    public Text PlaceholderText { get; }

    public CharacterIconBlock(Image rootImage, Image icon, Text placeholderText)
    {
        RootImage = rootImage;
        Icon = icon;
        PlaceholderText = placeholderText;
    }
}
