using UnityEngine;

// 给 [SerializeReference] 多态字段加"类型下拉"选择器:
// 在 Inspector 用下拉选具体类型(逼近/瞬移/跳…),选中后该类型的参数自动展开可改。
// 配套 Editor/SubclassSelectorDrawer.cs。
public class SubclassSelectorAttribute : PropertyAttribute { }
