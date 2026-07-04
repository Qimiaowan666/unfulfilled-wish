using UnityEngine;

// 营火存档点：玩家进范围按 F 存档 + 回血,并设为复活点。
public class CheckpointTrigger : InteractTrigger
{
    public string checkpointID;
    public string displayName;

    [Tooltip("复活落点（空子物体，摆在地面安全位置）。留空则用玩家站立位置 + 偏移")]
    public Transform respawnAnchor;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(checkpointID))
            checkpointID = gameObject.name;
    }

    protected override void Reset() { base.Reset(); prompt = "恢复"; }

    protected override void Interact()
    {
        Vector3? anchor = respawnAnchor != null ? respawnAnchor.position : (Vector3?)null;
        CheckpointManager.Instance?.ActivateCheckpoint(checkpointID, anchor);   // F 存档(处决已改 R,不冲突)
    }
}
