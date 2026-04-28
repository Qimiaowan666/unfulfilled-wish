using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    public bool enableShake = false;
    public float defaultDuration = 0.1f;
    public float defaultMagnitude = 0.06f;

    Vector3 originalLocalPosition;
    Coroutine activeShake;

    void Awake()
    {
        Instance = this;
        originalLocalPosition = transform.localPosition;
    }

    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }

    public void Shake(float duration, float magnitude)
    {
        if (!enableShake) return;
        if (!isActiveAndEnabled) return;
        if (activeShake != null) StopCoroutine(activeShake);
        activeShake = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            Vector2 offset = Random.insideUnitCircle * magnitude;
            transform.localPosition = originalLocalPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        activeShake = null;
    }
}
