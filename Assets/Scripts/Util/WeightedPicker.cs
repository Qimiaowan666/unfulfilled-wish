using UnityEngine;

public static class WeightedPicker
{
    // 输入权重数组，按权重比例随机抽中一个，返回索引；全 0 或负则返回 -1
    public static int Pick(float[] weights)
    {
        if (weights == null || weights.Length == 0) return -1;

        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
            if (weights[i] > 0f) total += weights[i];

        if (total <= 0f) return -1;

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0f) continue;
            acc += weights[i];
            if (roll <= acc) return i;
        }
        return -1;
    }
}
