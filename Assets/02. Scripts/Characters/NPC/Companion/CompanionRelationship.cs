using System;
using UnityEngine;

/// <summary>
/// 동료 관계 관리: 미정산금, Trust 변화, 탈퇴/도주/배신 점수 계산 및 이벤트.
/// </summary>
public sealed class CompanionRelationship : MonoBehaviour
{
    private NPCStats           _stats;
    private CompanionCharacter _companion;

    public float UnpaidAmount  { get; private set; }

    // 임시 불만 플래그 (장비회수 등에서 부여)
    private int  _recentComplaintScore;
    private bool _hasComplaintFlag;

    public event Action OnBetrayalTriggered;
    public event Action OnFleeTriggered;
    public event Action OnRetirementTriggered;
    public event Action OnBetrayalWarning;
    public event Action OnRetirementWarning;
    public event Action<float> OnUnpaidChanged;

    void Awake()
    {
        _stats     = GetComponent<NPCStats>();
        _companion = GetComponent<CompanionCharacter>();
    }

    // ── 미정산금 ──────────────────────────────────────────

    public void AddUnpaidAmount(float amount)
    {
        UnpaidAmount += amount;
        OnUnpaidChanged?.Invoke(UnpaidAmount);
    }

    /// <summary>정산 실행. 지급 골드를 받아 Trust 변화 반환.</summary>
    public float Settle(int goldPaid)
    {
        if (UnpaidAmount <= 0f) return 0f;

        float ratio  = goldPaid / UnpaidAmount;
        float delta  = SettlementSystem.CalculateTrustDelta(ratio, _stats.Greed);
        _stats.ModifyTrust(delta);

        UnpaidAmount = Mathf.Max(0f, UnpaidAmount - goldPaid);
        OnUnpaidChanged?.Invoke(UnpaidAmount);

        return delta;
    }

    // ── 오인사격 피격 ─────────────────────────────────────

    public void OnHitByPlayer(float damage)
    {
        // Fear가 높을수록 더 예민하게 반응
        float delta = -5f * (1f + (_stats.Fear - 50f) / 100f);
        _stats.ModifyTrust(Mathf.Floor(delta));

        string name = _stats.NPCName;
        if (_stats.Trust >= 70f)
            BubbleManager.ShowBubble(transform, "조심해줘.");
        else if (_stats.Trust >= 40f)
            BubbleManager.ShowBubble(transform, "지금 뭐 하는 거야.");
        else
        {
            if (_stats.Fear >= 60f)
                BubbleManager.ShowBubble(transform, "날 죽이려는 거야?!");
            else
                BubbleManager.ShowBubble(transform, "한 번만 더 그러면 나도 가만 안 있어.");
        }

        LogManager.AddLog($"{name}이(가) 플레이어의 공격에 맞았다.");
        CheckImmediateThresholds();
    }

    // ── 불만 플래그 ───────────────────────────────────────

    public void AddComplaintScore(int score)
    {
        _recentComplaintScore += score;
        _hasComplaintFlag      = true;
    }

    public void ClearComplaintFlag()
    {
        _recentComplaintScore = 0;
        _hasComplaintFlag     = false;
    }

    // ── 점수 계산 ────────────────────────────────────────

    public int CalculateRetirementScore()
    {
        var ps = PlayerCharacter.Instance?.Stats;
        float unpaidComplaint = UnpaidAmount / 20f;
        float staminaPenalty  = _stats.Stamina <= 30f ? 15f : 0f;
        float complaintBonus  = _hasComplaintFlag ? _recentComplaintScore : 0f;

        return Mathf.RoundToInt(
            (100f - _stats.Trust)
            + unpaidComplaint
            + staminaPenalty
            + complaintBonus
        );
    }

    public int CalculateFleeScore()
    {
        var ps       = PlayerCharacter.Instance?.Stats;
        var roster   = PartyRoster.Instance;
        int allies   = roster != null ? roster.Members.Count : 1;

        // 전투 불리 보정 (간략 버전: 적 수는 EnemyBrain 조회 비용이 크므로 생략, Stamina/HP로 대신)
        float hpRatio   = GetComponent<HealthComponent>().HPRatio;
        float hpPenalty = hpRatio <= 0.3f ? 20f : 0f;
        float stPenalty = _stats.Stamina <= 20f ? 15f : 0f;
        float trustMod  = _stats.Trust * 0.3f;

        return Mathf.RoundToInt(
            _stats.Fear
            + hpPenalty
            + stPenalty
            - trustMod
        );
    }

    public int CalculateBetrayalScore()
    {
        var ps = PlayerCharacter.Instance?.Stats;
        float playerWealth   = ps?.FinalWealth ?? 0f;
        float playerThreat   = ps?.FinalThreat ?? 0f;
        float playerHPBonus  = (ps != null && PlayerCharacter.Instance.Health.HPRatio <= 0.3f) ? 20f : 0f;
        float playerSTBonus  = (ps != null && ps.Stamina <= 20f) ? 15f : 0f;
        float fearPenalty    = _stats.Fear * 0.3f;

        return Mathf.RoundToInt(
            (100f - _stats.Trust)
            + _stats.Greed
            + (100f - _stats.Morality)
            + playerWealth
            + playerHPBonus
            + playerSTBonus
            - playerThreat
            - fearPenalty
        );
    }

    // ── 임계값 즉시 체크 (피격/상태 변화 직후 호출) ────────

    public void CheckImmediateThresholds()
    {
        int betray  = CalculateBetrayalScore();
        int flee    = CalculateFleeScore();
        int retire  = CalculateRetirementScore();

        if      (betray >= 140) { OnBetrayalTriggered?.Invoke(); return; }
        else if (flee   >= 110) { OnFleeTriggered?.Invoke();     return; }
        else if (retire >= 100) { OnRetirementTriggered?.Invoke(); return; }
        else if (betray >= 120) OnBetrayalWarning?.Invoke();
        else if (retire >=  80) OnRetirementWarning?.Invoke();
    }

    // 휴식 후 전체 이벤트 판정 (우선순위: 배신 > 도주 > 탈퇴 > 요구 > 무사)
    public void CheckAfterRest()
    {
        int betray  = CalculateBetrayalScore();
        int flee    = CalculateFleeScore();
        int retire  = CalculateRetirementScore();

        if      (betray >= 140) OnBetrayalTriggered?.Invoke();
        else if (flee   >= 110) OnFleeTriggered?.Invoke();
        else if (retire >= 100) OnRetirementTriggered?.Invoke();
        else if (betray >= 120) OnBetrayalWarning?.Invoke();
        else if (retire >=  80) OnRetirementWarning?.Invoke();
        // 정산 요구 판정
        else CheckSettlementDemand();
    }

    private void CheckSettlementDemand()
    {
        var ps = PlayerCharacter.Instance?.Stats;
        float score =
            _stats.Greed
            + (UnpaidAmount / 10f)
            + (ps?.FinalWealth ?? 0f)
            - _stats.Trust;

        if (score >= 100f)
        {
            BubbleManager.ShowBubble(transform, "내 몫은 언제 줄 거야?");
            LogManager.AddLog($"{_stats.NPCName}이(가) 정산을 요구했다.");
        }
        else if (score >= 80f)
        {
            LogManager.AddLog($"{_stats.NPCName}이(가) 자신의 몫을 계산하는 듯하다.");
        }
    }
}
