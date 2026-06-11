using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 属性行 prefab 的绑定脚本（详情面板 / 状态构成用）。
// 左 label、右 value（value 可含 +/- 富文本上色）。
public class StatRowView : MonoBehaviour
{
    [Tooltip("左侧属性名")]   public TMP_Text labelText;
    [Tooltip("右侧数值")]     public TMP_Text valueText;

    public void Setup(string label, string value)
    {
        if (labelText != null) labelText.text = label;
        if (valueText != null) valueText.text = value;
    }
}
