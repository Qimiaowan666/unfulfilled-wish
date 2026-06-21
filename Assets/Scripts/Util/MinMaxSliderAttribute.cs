using UnityEngine;

// 把一对 [min,max] 画成单条双手柄滑块(配合 Vector2:x=min, y=max)。
// 用法:[MinMaxSlider(0f, 30f)] public Vector2 range;
public class MinMaxSliderAttribute : PropertyAttribute
{
    public readonly float min;
    public readonly float max;
    public MinMaxSliderAttribute(float min, float max) { this.min = min; this.max = max; }
}
