using UnityEngine;
using System;

public class PoiseMeter : MonoBehaviour
{
    public float maxPoise = 100f;
    public float poiseRegenRate = 10f;
    public float regenDelay = 3f;

    public float CurrentPoise { get; private set; }
    public bool IsBroken => CurrentPoise <= 0f;

    public event Action OnPoiseBroken;
    public event Action<float, float> OnPoiseChanged;

    float regenTimer;

    void Awake() => CurrentPoise = maxPoise;

    void Update()
    {
        if (IsBroken) return;

        if (regenTimer > 0f)
            regenTimer -= Time.deltaTime;
        else if (CurrentPoise < maxPoise)
        {
            CurrentPoise = Mathf.Min(CurrentPoise + poiseRegenRate * Time.deltaTime, maxPoise);
            OnPoiseChanged?.Invoke(CurrentPoise, maxPoise);
        }
    }

    public void TakePoiseDamage(float damage)
    {
        if (IsBroken) return;
        CurrentPoise = Mathf.Max(CurrentPoise - damage, 0f);
        regenTimer = regenDelay;
        Debug.Log($"{gameObject.name} 韧性 -{damage}，剩余：{CurrentPoise}/{maxPoise}");
        OnPoiseChanged?.Invoke(CurrentPoise, maxPoise);
        if (IsBroken) OnPoiseBroken?.Invoke();
    }

    public void ResetPoise()
    {
        CurrentPoise = maxPoise;
        OnPoiseChanged?.Invoke(CurrentPoise, maxPoise);
    }

}
