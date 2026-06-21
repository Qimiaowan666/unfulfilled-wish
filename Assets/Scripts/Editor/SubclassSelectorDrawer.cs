using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

// [SerializeReference] + [SubclassSelector] 字段的绘制器:
// 第一行 = label + 类型下拉(无 / 各子类);下面缩进展开选中类型的参数。
// 不用 foldout —— 避免 foldout 的整行点击区把下拉按钮的点击吃掉(选了就改不了)。
[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    // 类型名 → 中文友好名(下拉项 + 当前显示)。没列的回退用类名。
    static readonly Dictionary<string, string> Nice = new Dictionary<string, string>
    {
        { "ApproachMover", "逼近 (走近)" },
        { "RetreatMover",  "后撤 (走开)" },
        { "TeleportMover", "瞬移 (闪身)" },
        { "JumpMover",     "跳接近 (先跳后砍)" },
        { "LaunchDriver",  "挑飞 (横劈→砸)" },
        { "JumpDriver",    "跳劈 (边跳边砍)" },
        { "LungeForward",  "前冲" },
        { "LungeBackward", "后撤" },
    };

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
        string full = property.managedReferenceFullTypename;
        if (string.IsNullOrEmpty(full)) return "无 (None)";
        int sp = full.LastIndexOf(' ');
        string fn = sp >= 0 ? full.Substring(sp + 1) : full;
        int dot = fn.LastIndexOf('.');
        string sn = dot >= 0 ? fn.Substring(dot + 1) : fn;
        return Nice.TryGetValue(sn, out var nice) ? nice : sn;
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
            string itemLabel = Nice.TryGetValue(t.Name, out var nice) ? nice : t.Name;
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
