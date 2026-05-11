using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어 후 정산 여부를 묻는 확인 UI.
/// "지금 정산하겠습니까?" → [정산하기] / [나중에]
/// StageFlowSystem이 Show()를 호출하고 콜백으로 결과를 받는다.
/// </summary>
public sealed class SettlementPromptView : MonoBehaviour
{
    [SerializeField] private GameObject       panel;
    [SerializeField] private TextMeshProUGUI  messageText;
    [SerializeField] private Button           confirmBtn;
    [SerializeField] private Button           laterBtn;

    private Action _onConfirm;
    private Action _onLater;

    void Awake()
    {
        confirmBtn?.onClick.AddListener(() => Choose(_onConfirm));
        laterBtn  ?.onClick.AddListener(() => Choose(_onLater));
        panel?.SetActive(false);
    }

    /// <summary>
    /// 정산 여부 확인 UI를 연다.
    /// </summary>
    /// <param name="unpaidCount">미정산금 있는 동료 수 (메시지 표시용)</param>
    /// <param name="onConfirm">정산하기 선택 시 콜백</param>
    /// <param name="onLater">나중에 선택 시 콜백</param>
    public void Show(int unpaidCount, Action onConfirm, Action onLater)
    {
        _onConfirm = onConfirm;
        _onLater   = onLater;

        if (messageText != null)
            messageText.text = unpaidCount > 0
                ? $"정산할 동료가 {unpaidCount}명 있습니다.\n지금 정산하겠습니까?"
                : "정산할 동료가 없습니다.";

        panel?.SetActive(true);
    }

    private void Choose(Action callback)
    {
        panel?.SetActive(false);
        _onConfirm = null;
        _onLater   = null;
        callback?.Invoke();
    }
}
