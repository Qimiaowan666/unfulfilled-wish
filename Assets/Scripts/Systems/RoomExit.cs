using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RoomExit : MonoBehaviour
{
    [SerializeField] Vector3 nextRoomCenter;
    [SerializeField] Vector3 playerSpawnPosition;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        RoomCamera.Instance?.TransitionToRoom(nextRoomCenter, playerSpawnPosition);
    }

    void OnDrawGizmos()
    {
        // 绿框：下一个房间范围
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireCube(nextRoomCenter, new Vector3(17.78f, 10f, 0f));
        // 黄点：玩家出生点
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(playerSpawnPosition, 0.3f);
    }
}
