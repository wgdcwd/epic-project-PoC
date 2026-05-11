using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동료 관리창: 상태 확인, 내보내기.
/// - 정산: 스테이지 클리어 시 강제 페이즈로 처리 (이 창에서 제거)
/// - 정산 후 내보내기: 제거
/// - 대기 지시: 제거
/// </summary>
public sealed class CompanionManagementView : MonoBehaviour
{
    [SerializeField] private GameObject       panel;
    [SerializeField] private TextMeshProUGUI  nameText;
    [SerializeField] private TextMeshProUGUI  trustText;
    [SerializeField] private TextMeshProUGUI  unpaidText;

    [SerializeField] private Button statusBtn;
    [SerializeField] private Button dismissBtn;
    [SerializeField] private Button closeBtn;

    private NPCCharacter _target;

    void Awake()
    {
        statusBtn  .onClick.AddListener(ShowStatus);
        dismissBtn .onClick.AddListener(DismissDirectly);
        closeBtn   .onClick.AddListener(Close);
        panel.SetActive(false);
    }

    public void Open(NPCCharacter companion)
    {
        _target = companion;
        Refresh();
        panel.SetActive(true);
    }

    public void Close()
    {
        _target = null;
        panel.SetActive(false);
    }

    private void Refresh()
    {
        if (_target == null) return;
        if (nameText   != null) nameText.text   = _target.Stats.NPCName;
        if (trustText  != null) trustText.text  = $"Trust {_target.Stats.Trust:F0}";
        if (unpaidText != null) unpaidText.text = $"미정산 {_target.Relationship.UnpaidAmount:F0}G";
    }

    private void ShowStatus()
    {
        if (_target == null) return;
        var s = _target.Stats;
        LogManager.AddLog(
            $"[{s.NPCName}] HP:{_target.Health.CurrentHP:F0} Stamina:{s.Stamina:F0} " +
            $"Trust:{s.Trust:F0} Greed:{s.Greed:F0} Fear:{s.Fear:F0} Morality:{s.Morality:F0}"
        );
        Close();
    }

    private void DismissDirectly()
    {
        if (_target == null) return;
        var captured = _target;
        Close();
        LogManager.AddLog($"{captured.Stats.NPCName}을(를) 파티에서 내보냈다.");
        PartyRoster.Instance?.RemoveMember(captured);
        Destroy(captured.gameObject);
    }
}
