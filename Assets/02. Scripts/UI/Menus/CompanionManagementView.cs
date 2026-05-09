using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동료 관리창: 정산, 장비 대여/회수, 상태 확인, 대기 지시, 내보내기.
/// </summary>
public sealed class CompanionManagementView : MonoBehaviour
{
    [SerializeField] private GameObject       panel;
    [SerializeField] private TextMeshProUGUI  nameText;
    [SerializeField] private TextMeshProUGUI  trustText;
    [SerializeField] private TextMeshProUGUI  unpaidText;

    [SerializeField] private Button settleBtn;
    [SerializeField] private Button lendBtn;
    [SerializeField] private Button retrieveBtn;
    [SerializeField] private Button statusBtn;
    [SerializeField] private Button waitBtn;
    [SerializeField] private Button dismissWithSettleBtn;
    [SerializeField] private Button dismissBtn;
    [SerializeField] private Button closeBtn;

    [SerializeField] private SettlementView settlementView;
    [SerializeField] private InventoryView  inventoryView;

    private NPCCharacter _target;

    void Awake()
    {
        settleBtn            .onClick.AddListener(OpenSettle);
        lendBtn              .onClick.AddListener(OpenLend);
        retrieveBtn          .onClick.AddListener(OpenRetrieve);
        statusBtn            .onClick.AddListener(ShowStatus);
        waitBtn              .onClick.AddListener(ToggleWait);
        dismissWithSettleBtn .onClick.AddListener(DismissWithSettle);
        dismissBtn           .onClick.AddListener(DismissDirectly);
        closeBtn             .onClick.AddListener(Close);
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

    private void OpenSettle()
    {
        if (_target == null) return;
        settlementView?.Open(_target);
        Close();
    }

    private void OpenLend()
    {
        if (_target == null) return;
        inventoryView?.OpenLend(_target, null);
        Close();
    }

    private void OpenRetrieve()
    {
        if (_target == null) return;
        inventoryView?.OpenRetrieve(_target, null);
        Close();
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

    private void ToggleWait()
    {
        if (_target == null) { Debug.LogWarning("[ToggleWait] _target is null"); return; }
        bool isWaiting = _target.Behavior == NPCBehavior.Waiting;
        _target.ToggleWaiting();
        LogManager.AddLog($"{_target.Stats.NPCName}이(가) {(isWaiting ? "다시 따라온다" : "이 자리에서 대기")}.");
        Close();
    }

    private void DismissWithSettle()
    {
        if (_target == null) return;
        var captured = _target;     // 정산창 닫힐 때 사용할 참조 캡처
        Close();                    // 관리창은 먼저 닫고
        settlementView?.Open(captured, () => DismissCompanion(captured));
    }

    private void DismissDirectly()
    {
        if (_target == null) return;
        float unpaid = _target.Relationship.UnpaidAmount;
        if (unpaid > 0f)
        {
            float trustDelta = -5f - (unpaid / 100f);
            _target.Stats.ModifyTrust(trustDelta);
            LogManager.AddLog($"{_target.Stats.NPCName}이(가) 미정산 상태로 떠났다. Trust {trustDelta:F1}");
        }
        var captured = _target;
        Close();
        DismissCompanion(captured);
    }

    private static void DismissCompanion(NPCCharacter companion)
    {
        if (companion == null) return;
        float trust = companion.Stats.Trust;
        if (trust >= 50f) companion.Inventory.ReturnAllToPlayer();
        else              LogManager.AddLog($"{companion.Stats.NPCName}이(가) 장비 일부를 가지고 떠났다.");

        LogManager.AddLog($"{companion.Stats.NPCName}을(를) 파티에서 내보냈다.");
        PartyRoster.Instance?.RemoveMember(companion);
        Destroy(companion.gameObject);
    }
}
