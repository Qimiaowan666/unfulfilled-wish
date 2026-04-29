using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    public string keyID;
    public int keyAmount = 1;

    bool collected;
    public bool IsCollected => collected;
    public string SaveID => SaveIdUtility.GetSceneObjectID(this, keyID);

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player")) return;

        collected = true;
        LevelKeyManager.Instance?.CollectKey(keyAmount);
        AudioManager.Instance?.PlayKeyPickup();
        gameObject.SetActive(false);
    }

    public void LoadCollected(bool value)
    {
        collected = value;
        gameObject.SetActive(!value);
    }
}
