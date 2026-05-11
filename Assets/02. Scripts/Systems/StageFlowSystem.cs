using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 이후 흐름을 담당하는 시스템.
///
/// [흐름]
/// - OnCleared (적 전멸) → 야영 버튼 활성화 + CampSystem에 현재 방 전달
/// - OnExited  (출구 통과) → 정산 여부 확인 창 표시
///
/// [등록 방식]
/// rooms 배열 대신 각 RoomStageController가 Start()에서
/// StageFlowSystem.Instance.Register(this)를 호출해 자동 등록.
/// 인스펙터에서 배열 연결 불필요.
///
/// [인스펙터 설정]
/// - settlementPrompt: 정산 여부 확인 UI
/// - settlementView  : 개별 정산 UI
/// - campButton      : 야영 버튼
/// - campSystem      : 야영 불침번 시스템
/// </summary>
public sealed class StageFlowSystem : MonoBehaviour
{
    public static StageFlowSystem Instance { get; private set; }

    [SerializeField] private SettlementPromptView  settlementPrompt;
    [SerializeField] private SettlementView        settlementView;
    [SerializeField] private CampButton            campButton;
    [SerializeField] private CampSystem            campSystem;

    private const float TrustPenaltyOnSkip = -5f;

    // ── Lifecycle ─────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Room 등록 API (RoomStageController에서 호출) ──────

    public void Register(RoomStageController room)
    {
        if (room == null) return;
        room.OnCleared += HandleStageClear;
        room.OnExited  += HandleStageExited;
        Debug.Log($"[StageFlowSystem] Registered room: {room.gameObject.name}");
    }

    public void Unregister(RoomStageController room)
    {
        if (room == null) return;
        room.OnCleared -= HandleStageClear;
        room.OnExited  -= HandleStageExited;
    }

    // ── 클리어 이벤트 (적 전멸) ──────────────────────────

    private void HandleStageClear(RoomStageController room)
    {
        if (campButton != null)
            campButton.Activate(room);
        else
            Debug.LogWarning("[StageFlowSystem] CampButton이 연결되지 않았습니다.");

        if (campSystem != null)
            campSystem.SetCurrentRoom(room);
        else
            Debug.LogWarning("[StageFlowSystem] CampSystem이 연결되지 않았습니다.");
    }

    // ── 출구 통과 이벤트 ─────────────────────────────────

    private void HandleStageExited(RoomStageController room)
    {
        Debug.Log("[StageFlowSystem] HandleStageExited called.");
        campButton?.Deactivate();
        StartCoroutine(RunSettlementPhase());
    }

    // ── 정산 페이즈 ───────────────────────────────────────

    private IEnumerator RunSettlementPhase()
    {
        Debug.Log("[StageFlowSystem] RunSettlementPhase started.");
        if (PartyRoster.Instance == null || PartyRoster.Instance.Members.Count == 0)
        {
            Debug.Log("[StageFlowSystem] No party members. Settlement skipped.");
            yield break;
        }

        var targets = new List<NPCCharacter>();
        foreach (var m in PartyRoster.Instance.Members)
            if (m != null && m.Relationship.UnpaidAmount > 0f)
                targets.Add(m);

        Debug.Log($"[StageFlowSystem] Settlement targets: {targets.Count}");
        if (targets.Count == 0) yield break;

        if (settlementPrompt == null)
        {
            Debug.LogWarning("[StageFlowSystem] SettlementPromptView 미연결. 정산 생략.");
            yield break;
        }
        Debug.Log("[StageFlowSystem] Showing SettlementPromptView.");

        bool? choice = null;
        settlementPrompt.Show(
            unpaidCount: targets.Count,
            onConfirm:   () => choice = true,
            onLater:     () => choice = false
        );
        yield return new WaitUntil(() => choice.HasValue);

        if (choice == false)
        {
            foreach (var companion in targets)
            {
                if (companion == null) continue;
                companion.Stats.ModifyTrust(TrustPenaltyOnSkip);
                BubbleManager.ShowBubble(companion.transform, "내 몫은 언제 주는 거야...");
            }
            LogManager.AddLog($"정산을 미뤘다. 동료 신뢰도 {TrustPenaltyOnSkip:F0} 감소.");
            yield break;
        }

        if (settlementView == null)
        {
            Debug.LogWarning("[StageFlowSystem] SettlementView 미연결.");
            yield break;
        }

        LogManager.AddLog("정산 시작.");
        foreach (var companion in targets)
        {
            if (companion == null) continue;
            bool done = false;
            settlementView.Open(companion, () => done = true);
            yield return new WaitUntil(() => done);
            yield return new WaitForSeconds(0.2f);
        }
        LogManager.AddLog("정산 완료.");
    }
}
