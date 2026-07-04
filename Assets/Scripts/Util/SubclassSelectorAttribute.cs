using UnityEngine;

// 给 [SerializeReference] 多态字段加"类型下拉"选择器:
// 在 Inspector 用下拉选具体类型(逼近/瞬移/跳…),选中后该类型的参数自动展开可改。
// 配套 Editor/SubclassSelectorDrawer.cs。
public class SubclassSelectorAttribute : PropertyAttribute { }

// 给多态子类标下拉里显示的中文名(SubclassSelectorDrawer 读它;不标则回退显示类名)。
[System.AttributeUsage(System.AttributeTargets.Class)]
public class TypeLabelAttribute : System.Attribute
{
    public readonly string Label;
    public TypeLabelAttribute(string label) => Label = label;
}
