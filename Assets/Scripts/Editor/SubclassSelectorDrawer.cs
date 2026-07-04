using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

// [SerializeReference] + [SubclassSelector] 字段的绘制器:
// 第一行 = label + 类型下拉(无 / 各子类);下面缩进展开选中类型的参数。
// 不用 foldout —— 避免 foldout 的整行点击区把下拉按钮的点击吃掉(选了就改不了)。
[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    // 类型 → 中文友好名:读类上的 [TypeLabel];没标则回退类名。
    static string Label(Type t) => t.GetCustomAttribute<TypeLabelAttribute>()?.Label ?? t.Name;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
            return EditorGUI.GetPropertyHeight(property, label, true);

        float h = EditorGUIUtility.singleLineHeight;   // 类型下拉行
        if (!string.IsNullOrEmpty(property.managedReferenceFullTypename))
        {
            var it = property.Copy();
            var end = it.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                h += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // 第一行:label + 类型下拉
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var ctrl = EditorGUI.PrefixLabel(line, label);
        if (EditorGUI.DropdownButton(ctrl, new GUIContent(Display(property)), FocusType.Keyboard))
            ShowMenu(property, ctrl);

        // 选中类型的参数:手动遍历画在下面(缩进)
        if (!string.IsNullOrEmpty(property.managedReferenceFullTypename))
        {
            EditorGUI.indentLevel++;
            float y = line.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var it = property.Copy();
            var end = it.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                float hh = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, hh), it, true);
                y += hh + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    string Display(SerializedProperty property)
    {
        var obj = property.managedReferenceValue;
        return obj == null ? "无 (None)" : Label(obj.GetType());
    }

    void ShowMenu(SerializedProperty property, Rect rect)
    {
        Type baseType = fieldInfo.FieldType;
        var so   = property.serializedObject;
        var path = property.propertyPath;

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("无 (None)"), false, () => Set(so, path, null));
        foreach (var t in TypeCache.GetTypesDerivedFrom(baseType))
        {
            if (t.IsAbstract) continue;
            string itemLabel = Label(t);
            var tt = t;
            menu.AddItem(new GUIContent(itemLabel), false, () => Set(so, path, tt));
        }
        menu.DropDown(rect);
    }

    // 菜单回调延后执行,SerializedProperty 句柄可能失效 → 用 so + path 重取
    static void Set(SerializedObject so, string path, Type t)
    {
        so.Update();
        var p = so.FindProperty(path);
        if (p != null) p.managedReferenceValue = t == null ? null : Activator.CreateInstance(t);
        so.ApplyModifiedProperties();
    }
}
