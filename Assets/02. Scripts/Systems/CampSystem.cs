using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 야영 불침번 턴제 시스템.
///
/// [턴 순서]
/// 1. 신뢰도 낮은 NPC 불만 대화
/// 2. 플레이어 불침번 선택 (CampActionView)
/// 3. NPC 불침번 (각 NPC가 수면 행동 자동 선택 후 행동 판정)
///    - NPC 수면 행동: Fear/Trust/Greed 기반으로 자동 결정
///    - 편하게 취침: 탐지 0%, 배신/절도 확률 높음
///    - 경계하며 취침: 탐지 50%, 배신/절도 확률 보통
///    - 취침하지 않기: 탐지 100%, 배신/절도 확률 낮음
/// 4. 회복 적용
///
/// [절도/기습 선택 시]
/// - 탐지 확률은 NPC 개별 CampSleepMode 기준으로 계산
/// </summary>
public sealed class CampSystem : MonoBehaviour
{
    // ── 상수 ──────────────────────────────────────────────

    private const float HpRecoverComfy = 0.5f;
    private const float HpRecoverAlert = 0.3f;
    private const float HpRecoverAwake = 0.1f;
    private const float StaminaRecover = 70f;

    // 플레이어 취침 모드별 NPC 절도/기습 차단 확률
    private const float StealBlockComfy   = 0f;
    private const float StealBlockAlert   = 0.5f;
    private const float StealBlockAwake   = 1f;
    private const float AssaultBlockComfy = 0f;
    private const float AssaultBlockAlert = 0.3f;
    private const float AssaultBlockAwake = 1f;

    // NPC 수면 모드별 절도 탐지 확률
    private const float DetectChanceComfy = 0f;
    private const float DetectChanceAlert = 0.5f;
    private const float DetectChanceAwake = 1f;

    // 경계하며 취침 시 기습 즉시 기상 확률 (절도 탐지보다 높아야 함)
    private const float AssaultWakeAlert = 0.65f;

    // ── 이벤트 ────────────────────────────────────────────

    public event Action OnCampStarted;
    public event Action OnCampFinished;

    // ── 상태 ──────────────────────────────────────────────

    public bool IsCamping { get; private set; }

    public enum WatchMode { Comfy, Alert, Awake, Assault, Steal }
    private WatchMode _playerMode;
    private bool      _playerChosen;

    private RoomStageController _currentRoom;

    // ── 인스펙터 ──────────────────────────────────────────

    [SerializeField] private CampActionView actionView;

    // ── 외부 API ──────────────────────────────────────────

    public void SetCurrentRoom(RoomStageController room) => _currentRoom = room;

    public void StartCamp()
    {
        if (IsCamping) return;
        StartCoroutine(CampCoroutine());
    }

    // ── 메인 코루틴 ───────────────────────────────────────

    private IEnumerator CampCoroutine()
    {
        IsCamping = true;
        OnCampStarted?.Invoke();
        SetPlayerMovement(false);
        LogManager.AddLog("야영을 시작했다.");

        // 1. 신뢰도 낮은 NPC 불만 대화
        yield return StartCoroutine(RunPreCampDialogue());

        // 2. 플레이어 불침번 선택
        yield return StartCoroutine(RunPlayerWatch());

        // 기습/절도는 각 핸들러에서 야영 종료까지 처리
        if (_playerMode == WatchMode.Assault || _playerMode == WatchMode.Steal)
            yield break;

        // 3. NPC 불침번 (취침 선택 시만)
        if (_playerMode != WatchMode.Awake)
            yield return StartCoroutine(RunNPCWatches());

        // 4. 회복 적용
        ApplyRecovery();

        FinishCamp();
    }

    // ── 사전 대화 ─────────────────────────────────────────

    private IEnumerator RunPreCampDialogue()
    {
        if (PartyRoster.Instance == null || PartyRoster.Instance.Members.Count == 0)
            yield break;

        NPCCharacter lowest = null;
        float lowestTrust = 50f;
        foreach (var m in PartyRoster.Instance.Members)
        {
            if (m == null) continue;
            if (m.Stats.Trust < lowestTrust) { lowestTrust = m.Stats.Trust; lowest = m; }
        }
        if (lowest == null) yield break;

        BubbleManager.ShowBubble(lowest.transform, "야영 전에 얘기 좀 하자.");
        LogManager.AddLog($"{lowest.Stats.NPCName}이(가) 불만을 제기했다. (Trust {lowest.Stats.Trust:F0})");
        yield return new WaitForSeconds(2f);
    }

    // ── 플레이어 불침번 선택 ──────────────────────────────

    private IEnumerator RunPlayerWatch()
    {
        if (actionView == null)
        {
            Debug.LogWarning("[CampSystem] CampActionView 미연결. 경계하며 취침 적용.");
            _playerMode = WatchMode.Alert;
            yield break;
        }

        _playerChosen = false;
        actionView.Show(
            onComfy:   () => { _playerMode = WatchMode.Comfy;  _playerChosen = true; },
            onAlert:   () => { _playerMode = WatchMode.Alert;  _playerChosen = true; },
            onAwake:   () => { _playerMode = WatchMode.Awake;  _playerChosen = true; },
            onAssault: () => StartCoroutine(HandlePlayerAssault()),
            onSteal:   () => StartCoroutine(HandlePlayerSteal())
        );

        yield return new WaitUntil(() => _playerChosen);
    }

    // ── NPC 수면 행동 자동 결정 ───────────────────────────

    /// <summary>
    /// NPC의 성향(Fear, Trust, Greed)에 따라 수면 행동을 자동으로 결정한다.
    /// Fear와 Trust를 복합 점수로 계산해 확률적으로 결정.
    ///
    /// 경계 점수 = Fear * 0.6 + (100 - Trust) * 0.4
    /// ≥ 70 → 취침하지 않기
    /// ≥ 40 → 경계하며 취침
    /// &lt; 40 → 편하게 취침 (다만 Greed가 높으면 확률 상승)
    /// </summary>
    private NpcSleepMode DecideNPCSleepMode(NPCStats stats)
    {
        // 경계 점수: Fear와 Trust 불신도를 복합
        float vigilance = stats.Fear * 0.6f + (100f - stats.Trust) * 0.4f;

        if (vigilance >= 70f)
        {
            // 경계 점수가 매우 높으면 취침하지 않기 (확률적)
            // vigilance 70~100 단계에서 점수에 비례해 안 잠 확률 증가
            float awakeChance = (vigilance - 70f) / 30f; // 0% ~ 100%
            if (UnityEngine.Random.value < awakeChance)
                return NpcSleepMode.Awake;
            return NpcSleepMode.Alert;
        }

        if (vigilance >= 40f)
        {
            // 경계 점수 중간대 → 경계하며 취침
            // 다만 Greed가 높으면 편하게 잠 확률 존재
            float comfyChance = Mathf.Max(0f, (stats.Greed - 50f) / 100f); // 0% ~ 50%
            if (UnityEngine.Random.value < comfyChance)
                return NpcSleepMode.Comfy;
            return NpcSleepMode.Alert;
        }

        // 경계 점수 낙음 → 편하게 취침 (기본)
        return NpcSleepMode.Comfy;
    }

    // ── 플레이어 기습 ─────────────────────────────────────

    private IEnumerator HandlePlayerAssault()
    {
        _playerMode = WatchMode.Assault;

        var snapshot = GetPartySnapshot();
        LogManager.AddLog("기습을 시작했다! 전부 처치해야 다음으로 넘어갈 수 있다.");

        foreach (var npc in snapshot)
        {
            if (npc == null) continue;

            // NPC 수면 행동 결정 및 저장
            var sleepMode = DecideNPCSleepMode(npc.Stats);
            npc.Stats.CampSleepMode = sleepMode;

            PartyRoster.Instance?.RemoveMember(npc);

            // 수면 모드에 따라 즉시 기상 여부 결정
            // 경계하며 취침의 기습 즉시 기상 확률(65%)은 절도 탐지 확률(50%)보다 높다
            bool wakesUp = sleepMode switch
            {
                NpcSleepMode.Awake  => true,
                NpcSleepMode.Alert  => UnityEngine.Random.value < AssaultWakeAlert,
                NpcSleepMode.Comfy  => false,
                _                   => false
            };

            npc.EnterSleepingHostile(wakesUp);

            string modeName = sleepMode switch
            {
                NpcSleepMode.Awake => "취침하지 않기",
                NpcSleepMode.Alert => "경계하며 취침",
                NpcSleepMode.Comfy => "편하게 취침",
                _                  => ""
            };

            if (wakesUp)
            {
                BubbleManager.ShowBubble(npc.transform, "뭐 하는 짓이야!");
                LogManager.AddLog($"{npc.Stats.NPCName}이(가) 기습을 눈치챘다! ({modeName})");
            }
            else
            {
                LogManager.AddLog($"{npc.Stats.NPCName}은(는) 아직 자고 있다. ({modeName})");
            }
        }

        FinishCamp();
        _playerChosen = true;
        yield return null;
    }

    // ── 플레이어 절도 ─────────────────────────────────────

    private IEnumerator HandlePlayerSteal()
    {
        _playerMode = WatchMode.Steal;

        var snapshot = GetPartySnapshot();

        // 미정산금 수집
        int totalUnpaid = 0;
        foreach (var m in snapshot)
            if (m != null) totalUnpaid += Mathf.RoundToInt(m.Relationship.UnpaidAmount);

        foreach (var m in snapshot)
            if (m != null) m.Relationship.ClearUnpaid();

        LogManager.AddLog(totalUnpaid > 0
            ? $"미정산금 {totalUnpaid}G를 챙겼다. 들키지 않고 달아날 수 있을까..."
            : "챙길 미정산금이 없다. 그냥 달아난다.");

        yield return new WaitForSeconds(1f);

        // NPC별 수면 행동 결정 및 탐지 판정
        bool detected = false;
        var detectedNPCs = new List<NPCCharacter>();
        var sleepingNPCs = new List<NPCCharacter>();

        foreach (var npc in snapshot)
        {
            if (npc == null) continue;

            // NPC 개별 수면 행동 결정
            var sleepMode = DecideNPCSleepMode(npc.Stats);
            npc.Stats.CampSleepMode = sleepMode;

            // 수면 모드에 따른 탐지 확률
            float detectChance = sleepMode switch
            {
                NpcSleepMode.Comfy => DetectChanceComfy,
                NpcSleepMode.Alert => DetectChanceAlert,
                NpcSleepMode.Awake => DetectChanceAwake,
                _                  => DetectChanceAlert
            };

            string modeName = sleepMode switch
            {
                NpcSleepMode.Awake => "취침하지 않기",
                NpcSleepMode.Alert => "경계하며 취침",
                NpcSleepMode.Comfy => "편하게 취침",
                _                  => ""
            };

            if (UnityEngine.Random.value < detectChance)
            {
                detected = true;
                detectedNPCs.Add(npc);
                LogManager.AddLog($"{npc.Stats.NPCName}이(가) 절도를 눈치챘다! ({modeName}, 탐지 확률 {detectChance * 100f:F0}%)");
            }
            else
            {
                sleepingNPCs.Add(npc);
                LogManager.AddLog($"{npc.Stats.NPCName}은(는) 눈치채지 못했다. ({modeName})");
            }
        }

        yield return new WaitForSeconds(0.5f);

        if (!detected)
        {
            // 도주 성공
            LogManager.AddLog("아무도 눈치채지 못했다. 조용히 달아났다.");

            foreach (var npc in snapshot)
            {
                PartyRoster.Instance?.RemoveMember(npc);
                // Companion 상태를 해제해야 더 이상 따라오지 않음
                if (npc != null) npc.SetAllegiance(NPCAllegiance.Neutral);
            }

            if (_currentRoom?.ExitPoint != null && PlayerCharacter.Instance != null)
                PlayerCharacter.Instance.transform.position = _currentRoom.ExitPoint.position;
            else
                Debug.LogWarning("[CampSystem] ExitPoint가 설정되지 않았습니다.");

            FinishCamp();
        }
        else
        {
            // 탐지됨 → 기습 루트 전환
            LogManager.AddLog("들켰다! 전투 돌입!");

            // 탐지한 NPC: Fear에 따라 도주 or 즉시 공격
            foreach (var npc in detectedNPCs)
            {
                if (npc == null) continue;
                PartyRoster.Instance?.RemoveMember(npc);
                npc.BecomeHostileFromCamp();
            }

            // 탐지 못한 NPC: 수면 모드에 따라 처리
            foreach (var npc in sleepingNPCs)
            {
                if (npc == null) continue;
                PartyRoster.Instance?.RemoveMember(npc);
                // 편하게 잔 NPC는 SleepingHostile, 경계 중이었으면 즉시 Engaging
                bool wakesUp = npc.Stats.CampSleepMode == NpcSleepMode.Alert
                    ? UnityEngine.Random.value < 0.3f
                    : false;
                npc.EnterSleepingHostile(wakesUp);
            }

            FinishCamp();
        }

        _playerChosen = true;
        yield return null;
    }

    // ── NPC 불침번 판정 (취침 선택 시) ───────────────────

    private IEnumerator RunNPCWatches()
    {
        if (PartyRoster.Instance == null) yield break;

        var snapshot = GetPartySnapshot();

        // 플레이어 취침 모드별 차단 확률
        float stealBlock   = _playerMode switch
        {
            WatchMode.Comfy => StealBlockComfy,
            WatchMode.Alert => StealBlockAlert,
            WatchMode.Awake => StealBlockAwake,
            _               => StealBlockAlert
        };
        float assaultBlock = _playerMode switch
        {
            WatchMode.Comfy => AssaultBlockComfy,
            WatchMode.Alert => AssaultBlockAlert,
            WatchMode.Awake => AssaultBlockAwake,
            _               => AssaultBlockAlert
        };

        foreach (var npc in snapshot)
        {
            if (npc == null) continue;
            yield return new WaitForSeconds(0.3f);

            // NPC 수면 행동 결정 및 저장
            var sleepMode = DecideNPCSleepMode(npc.Stats);
            npc.Stats.CampSleepMode = sleepMode;

            string modeName = sleepMode switch
            {
                NpcSleepMode.Awake => "취침하지 않기",
                NpcSleepMode.Alert => "경계하며 취침",
                NpcSleepMode.Comfy => "편하게 취침",
                _                  => ""
            };
            LogManager.AddLog($"{npc.Stats.NPCName}의 불침번: {modeName}");

            int theftScore  = npc.Relationship.CalculateTheftScore();
            int betrayScore = npc.Relationship.CalculateBetrayalScore();

            // NPC 수면 행동에 따른 행동 가중치 보정
            // 편하게 잔 NPC는 방심 상태 → 배신/절도 점수 상승
            float sleepBonus = sleepMode switch
            {
                NpcSleepMode.Comfy => 20f,   // 방심 → 기회주의 행동 증가
                NpcSleepMode.Alert => 0f,
                NpcSleepMode.Awake => -20f,  // 경계 → 배신 억제
                _                  => 0f
            };

            // 절도 시도
            if (theftScore + sleepBonus >= 130)
            {
                if (UnityEngine.Random.value < stealBlock)
                    LogManager.AddLog($"{npc.Stats.NPCName}이(가) 수상한 움직임을 보였지만 막았다.");
                else
                    npc.Relationship.CheckRestOpportunity();
                continue;
            }

            // 기습/배신 시도
            if (betrayScore + sleepBonus >= 120)
            {
                if (UnityEngine.Random.value < assaultBlock)
                    LogManager.AddLog($"{npc.Stats.NPCName}이(가) 위협적인 눈빛을 보였지만 넘어갔다.");
                else
                    npc.Relationship.CheckRestOpportunity();
                continue;
            }

            // 일반 판정
            npc.Relationship.CheckRestOpportunity();
        }
    }

    // ── 회복 적용 ─────────────────────────────────────────

    private void ApplyRecovery()
    {
        float hpRatio = _playerMode switch
        {
            WatchMode.Comfy => HpRecoverComfy,
            WatchMode.Alert => HpRecoverAlert,
            WatchMode.Awake => HpRecoverAwake,
            _               => HpRecoverAlert
        };

        var player = PlayerCharacter.Instance;
        if (player != null)
        {
            player.Health.HealPercent(hpRatio);
            player.Stats.RestoreStamina(StaminaRecover);
        }

        if (PartyRoster.Instance != null)
        {
            foreach (var m in PartyRoster.Instance.Members)
            {
                if (m == null) continue;
                m.Health.HealPercent(hpRatio);
                m.Stats.RestoreStamina(StaminaRecover);
                m.Relationship.ClearComplaintFlag();
            }
        }
    }

    // ── 야영 종료 ─────────────────────────────────────────

    private void FinishCamp()
    {
        SetPlayerMovement(true);
        IsCamping = false;
        OnCampFinished?.Invoke();
        LogManager.AddLog("야영을 마쳤다.");
    }

    // ── 헬퍼 ──────────────────────────────────────────────

    private static List<NPCCharacter> GetPartySnapshot()
    {
        if (PartyRoster.Instance == null) return new List<NPCCharacter>();
        return new List<NPCCharacter>(PartyRoster.Instance.Members);
    }

    private static void SetPlayerMovement(bool enabled)
    {
        var player = PlayerCharacter.Instance;
        if (player == null) return;
        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = enabled;
    }
}
