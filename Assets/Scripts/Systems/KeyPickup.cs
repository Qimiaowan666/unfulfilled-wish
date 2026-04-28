using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    public int keyAmount = 1;

    bool collected;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player")) return;

        collected = true;
        LevelKeyManager.Instance?.CollectKey(keyAmount);
        gameObject.SetActive(false);
    }
}
