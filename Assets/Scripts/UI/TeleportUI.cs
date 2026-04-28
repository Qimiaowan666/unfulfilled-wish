using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public struct CheckpointEntry
{
    public string id;
    public string displayName;
    public Vector2 worldPosition;
}

public class TeleportUI : MonoBehaviour
{
    public GameObject panel;
    public Transform listParent;
    public GameObject entryPrefab;

    public List<CheckpointEntry> allCheckpoints = new List<CheckpointEntry>();

    public void Open()
    {
        panel.SetActive(true);
        Refresh();
    }

    public void Close() => panel.SetActive(false);

    void Refresh()
    {
        foreach (Transform child in listParent) Destroy(child.gameObject);

        foreach (var cp in allCheckpoints)
        {
            if (!CheckpointManager.Instance.IsTeleportable(cp.id)) continue;

            var entry = Instantiate(entryPrefab, listParent);
            var label = entry.GetComponentInChildren<Text>();
            if (label != null) label.text = cp.displayName;

            var btn = entry.GetComponent<Button>();
            var dest = cp.worldPosition;
            btn?.onClick.AddListener(() => { CheckpointManager.Instance.TeleportPlayer(dest); Close(); });
        }
    }
}
