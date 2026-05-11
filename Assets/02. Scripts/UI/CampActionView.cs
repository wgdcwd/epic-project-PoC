using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 야영 불침번 플레이어 행동 선택 UI.
/// CampSystem이 Show()를 호출하면 패널이 열리고,
/// 플레이어가 하나를 선택하면 콜백 실행 후 자동으로 닫힌다.
///
/// [버튼 구성]
/// - 편하게 취침하기
/// - 경계하며 취침하기
/// - 취침하지 않기
/// - 동료를 기습한다
/// - 훔치고 달아난다
///
/// 기습/절도는 누르는 순간 UI가 닫히고 CampSystem이 처리한다.
/// 누굴 기습할지 선택하는 UI는 없다 — 전부 적대화.
/// </summary>
public sealed class CampActionView : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Buttons")]
    [SerializeField] private Button comfyBtn;    // 편하게 취침하기
    [SerializeField] private Button alertBtn;    // 경계하며 취침하기
    [SerializeField] private Button awakeBtn;    // 취침하지 않기
    [SerializeField] private Button assaultBtn;  // 동료를 기습한다
    [SerializeField] private Button stealBtn;    // 훔치고 달아난다

    [Header("Labels (선택사항)")]
    [SerializeField] private TextMeshProUGUI titleText;

    // ── 콜백 ──────────────────────────────────────────────

    private Action _onComfy;
    private Action _onAlert;
    private Action _onAwake;
    private Action _onAssault;
    private Action _onSteal;

    // ── Lifecycle ─────────────────────────────────────────

    void Awake()
    {
        comfyBtn  ?.onClick.AddListener(() => Choose(_onComfy));
        alertBtn  ?.onClick.AddListener(() => Choose(_onAlert));
        awakeBtn  ?.onClick.AddListener(() => Choose(_onAwake));
        assaultBtn?.onClick.AddListener(() => Choose(_onAssault));
        stealBtn  ?.onClick.AddListener(() => Choose(_onSteal));

        panel?.SetActive(false);
    }

    // ── 외부 API ──────────────────────────────────────────

    public void Show(
        Action onComfy,
        Action onAlert,
        Action onAwake,
        Action onAssault,
        Action onSteal)
    {
        _onComfy   = onComfy;
        _onAlert   = onAlert;
        _onAwake   = onAwake;
        _onAssault = onAssault;
        _onSteal   = onSteal;

        if (titleText != null) titleText.text = "불침번 후 행동 선택";
        panel?.SetActive(true);
    }

    public void Hide()
    {
        panel?.SetActive(false);
    }

    // ── 내부 ──────────────────────────────────────────────

    private void Choose(Action callback)
    {
        Hide();
        callback?.Invoke();
    }
}
