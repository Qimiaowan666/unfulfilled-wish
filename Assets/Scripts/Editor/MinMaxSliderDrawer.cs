using UnityEngine;
using UnityEditor;

// 双手柄区间滑块:左数字框 | 滑块 | 右数字框。作用于带 [MinMaxSlider] 的 Vector2(x=min, y=max)。
[CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
public class MinMaxSliderDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        if (prop.propertyType != SerializedPropertyType.Vector2)
        {
            EditorGUI.PropertyField(pos, prop, label);   // 用错类型时退回默认绘制
            return;
        }

        var a = (MinMaxSliderAttribute)attribute;
        EditorGUI.BeginProperty(pos, label, prop);
        Rect r = EditorGUI.PrefixLabel(pos, label);

        const float fieldW = 46f, pad = 4f;
        var minRect    = new Rect(r.x, r.y, fieldW, r.height);
        var maxRect    = new Rect(r.xMax - fieldW, r.y, fieldW, r.height);
        var sliderRect = new Rect(minRect.xMax + pad, r.y, maxRect.x - minRect.xMax - 2f * pad, r.height);

        Vector2 v = prop.vector2Value;
        EditorGUI.BeginChangeCheck();
        float lo = EditorGUI.FloatField(minRect, v.x);
        float hi = EditorGUI.FloatField(maxRect, v.y);
        EditorGUI.MinMaxSlider(sliderRect, ref lo, ref hi, a.min, a.max);
        if (EditorGUI.EndChangeCheck())
        {
            v.x = Mathf.Clamp(lo, a.min, a.max);
            v.y = Mathf.Clamp(hi, v.x, a.max);
            prop.vector2Value = v;
        }
        EditorGUI.EndProperty();
    }
}
