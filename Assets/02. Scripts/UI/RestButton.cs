using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 휴식 버튼 UI. RestSystem.Instance에 위임.
/// 휴식 중에는 버튼 비활성화 + 라벨 변경.
/// </summary>
public sealed class RestButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string idleText  = "휴식 (R)";
    [SerializeField] private string restingText = "휴식 중...";

    void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClick);
        if (label  != null) label.text = idleText;
    }

    void Start()
    {
        if (RestSystem.Instance == null) return;
        RestSystem.Instance.OnRestStarted  += OnRestStarted;
        RestSystem.Instance.OnRestFinished += OnRestFinished;
    }

    void OnDestroy()
    {
        if (RestSystem.Instance == null) return;
        RestSystem.Instance.OnRestStarted  -= OnRestStarted;
        RestSystem.Instance.OnRestFinished -= OnRestFinished;
    }

    private void OnClick()
    {
        if (RestSystem.Instance == null) return;
        RestSystem.Instance.TryRest();
    }

    private void OnRestStarted()
    {
        if (button != null) button.interactable = false;
        if (label  != null) label.text = restingText;
    }

    private void OnRestFinished(bool _)
    {
        if (button != null) button.interactable = true;
        if (label  != null) label.text = idleText;
    }
}
