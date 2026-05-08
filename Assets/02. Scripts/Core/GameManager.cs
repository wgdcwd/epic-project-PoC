using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsPlaying { get; private set; } = true;

    [SerializeField] private GameObject gameOverScreenObj;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void TriggerGameOver()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        Time.timeScale = 0f;
        if (gameOverScreenObj != null)
            gameOverScreenObj.SetActive(true);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
