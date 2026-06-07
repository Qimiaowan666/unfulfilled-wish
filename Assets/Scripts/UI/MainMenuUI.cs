using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public string firstGameplayScene = "ForsakenShrine";
    public Button startButton;
    public Button quitButton;

    void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstGameplayScene);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
