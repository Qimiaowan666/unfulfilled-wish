using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadTrigger : MonoBehaviour
{
    public string sceneName = "ForsakenShrine";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance != null)
            GameManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
