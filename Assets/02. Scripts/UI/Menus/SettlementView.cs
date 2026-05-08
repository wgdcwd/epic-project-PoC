using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정산 UI: 슬라이더로 지급 금액 선택, 실시간으로 예상 반응 미리보기.
/// </summary>
public sealed class SettlementView : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI unpaidText;     // "미정산금: 400G"
    [SerializeField] private TextMeshProUGUI goldText;       // 슬라이더 값: "지급: 320G"
    [SerializeField] private TextMeshProUGUI reactionText;   // 예상 반응 미리보기
    [SerializeField] private TextMeshProUGUI resultText;     // 정산 후 결과 표시

    [Header("Input")]
    [SerializeField] private Slider goldSlider;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;

    private CompanionCharacter _target;
    private System.Action      _onClosed;

    void Awake()
    {
        confirmBtn.onClick.AddListener(OnConfirm);
        cancelBtn .onClick.AddListener(Close);
        if (goldSlider != null)
        {
            goldSlider.wholeNumbers = true;
            goldSlider.onValueChanged.AddListener(OnSliderChanged);
        }
        panel.SetActive(false);
    }

    // ── 진입점 ────────────────────────────────────────────

    public void Open(CompanionCharacter companion, System.Action onClosed = null)
    {
        _target   = companion;
        _onClosed = onClosed;
        if (resultText != null) resultText.text = "";
        SetupSlider();
        RefreshDisplay();
        panel.SetActive(true);
    }

    public void Close()
    {
        var cb = _onClosed;
        _target   = null;
        _onClosed = null;
        panel.SetActive(false);
        cb?.Invoke();
    }

    // ── 슬라이더 설정 ─────────────────────────────────────

    private void SetupSlider()
    {
        if (_target == null || goldSlider == null) return;

        int   playerGold = PlayerCharacter.Instance?.Stats.Gold ?? 0;
        float unpaid     = _target.Relationship.UnpaidAmount;

        // 후한 정산까지 허용 (미정산금의 1.5배), 단 보유 골드를 넘을 수는 없다.
        int upperByLoyalty = Mathf.Max(0, Mathf.RoundToInt(unpaid * 1.5f));
        int maxRange       = Mathf.Min(playerGold, upperByLoyalty);

        // 미정산금이 0이거나 보유 골드가 0이면 슬라이더 비활성화
        bool interactable  = unpaid > 0f && playerGold > 0;
        goldSlider.interactable = interactable;

        goldSlider.minValue = 0;
        goldSlider.maxValue = Mathf.Max(0, maxRange);

        // 기본값: 정확히 정산할 수 있는 만큼. 부족하면 보유 골드 한도까지.
        int defaultPay = Mathf.Min(playerGold, Mathf.RoundToInt(unpaid));
        goldSlider.SetValueWithoutNotify(Mathf.Clamp(defaultPay, 0, (int)goldSlider.maxValue));
    }

    private void RefreshDisplay()
    {
        if (_target == null) return;
        float unpaid = _target.Relationship.UnpaidAmount;
        int   gold   = PlayerCharacter.Instance?.Stats.Gold ?? 0;

        if (unpaidText != null)
            unpaidText.text = $"미정산금 {unpaid:F0} G   (보유 {gold:N0} G)";

        OnSliderChanged(goldSlider != null ? goldSlider.value : 0f);
    }

    // ── 슬라이더 변경 시 ─────────────────────────────────

    private void OnSliderChanged(float value)
    {
        int paid = Mathf.RoundToInt(value);
        if (goldText != null) goldText.text = $"지급 {paid:N0} G";
        UpdateReactionPreview(paid);
    }

    private void UpdateReactionPreview(int paid)
    {
        if (_target == null || reactionText == null) return;

        float unpaid = _target.Relationship.UnpaidAmount;
        if (unpaid <= 0f)
        {
            reactionText.text = "정산할 금액이 없습니다.";
            return;
        }

        float ratio = paid / unpaid;
        float delta = SettlementSystem.CalculateTrustDelta(ratio, _target.NPCStats.Greed);
        string name = _target.NPCStats.NPCName;

        string label = ClassifyReaction(delta, name);
        string sign  = delta >= 0f ? "+" : "";
        reactionText.text = $"예상 : {label}\nTrust {sign}{delta:F1}";
    }

    /// <summary>Trust 변화량을 자연어 반응으로 매핑.</summary>
    private static string ClassifyReaction(float delta, string name)
    {
        if (delta <= -12f) return $"{name}이(가) 크게 분노할 것입니다.";
        if (delta <=  -5f) return $"{name}이(가) 실망할 것입니다.";
        if (delta <    0f) return $"{name}이(가) 약간 실망할 수 있습니다.";
        if (delta <=  3f)  return $"{name}이(가) 만족할 것입니다.";
        if (delta <=  8f)  return $"{name}이(가) 기뻐할 것입니다.";
        return                    $"{name}이(가) 매우 기뻐할 것입니다.";
    }

    // ── 정산 확정 ────────────────────────────────────────

    private void OnConfirm()
    {
        if (_target == null || goldSlider == null) { Close(); return; }

        int amount = Mathf.RoundToInt(goldSlider.value);

        var player = PlayerCharacter.Instance?.Stats;
        if (player == null) { Close(); return; }

        if (!player.SpendGold(amount))
        {
            if (resultText != null) resultText.text = "골드가 부족합니다.";
            return;
        }

        float delta = _target.Relationship.Settle(amount);
        string sign = delta >= 0f ? "+" : "";

        if (resultText != null) resultText.text = $"정산 완료. Trust {sign}{delta:F1}";
        LogManager.AddLog($"{_target.NPCStats.NPCName}에게 {amount}G 정산. Trust {sign}{delta:F1}");

        // 슬라이더 범위/표시 재계산 (미정산금이 줄었거나 골드가 줄어서)
        SetupSlider();
        RefreshDisplay();
    }
}
