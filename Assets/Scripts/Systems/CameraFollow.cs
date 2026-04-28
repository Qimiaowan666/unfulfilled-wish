using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 8f;
    public Vector2 offset = new Vector2(0f, 1f);
    public Vector2 deadZone = new Vector2(0.5f, 0.3f);

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);

        Vector3 delta = desired - transform.position;
        if (Mathf.Abs(delta.x) < deadZone.x) desired.x = transform.position.x;
        if (Mathf.Abs(delta.y) < deadZone.y) desired.y = transform.position.y;

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
