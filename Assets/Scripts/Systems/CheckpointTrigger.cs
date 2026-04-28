using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    public string checkpointID;
    public string displayName;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        CheckpointManager.Instance?.ActivateCheckpoint(checkpointID, transform.position);
    }
}
