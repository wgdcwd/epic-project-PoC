using UnityEngine;
using UnityEngine.UI;

public sealed class GameOverScreen : MonoBehaviour
{
    [SerializeField] private Button restartBtn;

    void Awake()
    {
        restartBtn.onClick.AddListener(() => GameManager.Instance?.RestartGame());
        gameObject.SetActive(false);
    }
}
