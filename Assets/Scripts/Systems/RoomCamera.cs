using System.Collections;
using UnityEngine;

public class RoomCamera : MonoBehaviour
{
    public static RoomCamera Instance { get; private set; }

    [SerializeField] float transitionDuration = 0.4f;

    bool transitioning;

    void Awake() => Instance = this;

    public void TransitionToRoom(Vector3 nextCenter, Vector3 playerSpawnPos)
    {
        if (transitioning) return;
        StartCoroutine(Transition(nextCenter, playerSpawnPos));
    }

    IEnumerator Transition(Vector3 nextCenter, Vector3 playerSpawnPos)
    {
        transitioning = true;

        // 冻结玩家
        var rb = FindAnyObjectByType<PlayerController>()?.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }

        // 相机滑动
        Vector3 from = transform.position;
        Vector3 to   = new Vector3(nextCenter.x, nextCenter.y, transform.position.z);
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, elapsed / transitionDuration);
            yield return null;
        }

        transform.position = to;

        // 传送玩家到新房间入口
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = playerSpawnPos;
        if (rb != null) rb.simulated = true;

        transitioning = false;
    }
}
