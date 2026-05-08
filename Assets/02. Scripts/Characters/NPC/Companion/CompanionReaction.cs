using UnityEngine;

/// <summary>
/// NPC가 자신의 상태 변화에 따라 로그/말풍선을 발생시킨다.
/// 쿨타임과 확률 판정으로 스팸 방지.
/// </summary>
public sealed class CompanionReaction : MonoBehaviour
{
    private NPCStats _stats;

    private float _logCooldown    = 0f;
    private float _bubbleCooldown = 0f;

    private const float LogCooldownTime    = 15f;
    private const float BubbleCooldownTime = 7f;

    void Awake() => _stats = GetComponent<NPCStats>();

    void Update()
    {
        if (_logCooldown    > 0f) _logCooldown    -= Time.deltaTime;
        if (_bubbleCooldown > 0f) _bubbleCooldown -= Time.deltaTime;
    }

    // ── 외부에서 호출하는 트리거 ──────────────────────────

    public void TriggerGreedWealthReaction(float playerWealth)
    {
        if (_stats.Greed < 70f) return;
        float prob = 0.20f + (_stats.Greed - 70f) * 0.01f + playerWealth * 0.005f;
        if (!Roll(prob)) return;

        TryLog(PickRandom(
            $"{_stats.NPCName}이(가) 플레이어의 장비를 힐끗거렸다.",
            $"{_stats.NPCName}이(가) 자기 몫을 계산하는 듯하다."
        ));
        TryBubble(PickRandom("그거 꽤 좋아 보이는데.", "같이 다니면 내 몫도 있겠지?"));
    }

    public void TriggerFearThreatReaction(float playerThreat)
    {
        if (_stats.Fear < 70f) return;
        float prob = 0.20f + (_stats.Fear - 70f) * 0.01f + playerThreat * 0.005f;
        if (!Roll(prob)) return;

        TryLog(PickRandom(
            $"{_stats.NPCName}이(가) 플레이어와 거리를 벌렸다.",
            $"{_stats.NPCName}이(가) 무기를 쥔 손을 바라본다."
        ));
        TryBubble(PickRandom("거기서 멈춰.", "더 가까이 오지 마."));
    }

    public void TriggerLowTrust()
    {
        TryLog(PickRandom(
            $"{_stats.NPCName}이(가) 대답을 피한다.",
            $"{_stats.NPCName}의 말수가 줄었다."
        ));
        TryBubble(PickRandom("난 이런 식으로는 오래 못 가.", "요즘 좀 이상하다고 생각하지 않아?"));
    }

    public void TriggerLowStamina()
    {
        TryLog(PickRandom(
            $"{_stats.NPCName}이(가) 숨을 고르며 발걸음을 늦춘다.",
            $"{_stats.NPCName}이(가) 피곤한 듯 주저앉는다."
        ));
        TryBubble(PickRandom("좀 쉬어야 해.", "이 상태로는 못 싸워."));
    }

    public void TriggerEquipmentLent()
    {
        TryLog(PickRandom(
            $"{_stats.NPCName}이(가) 받은 장비를 살펴본다.",
            $"{_stats.NPCName}이(가) 장비를 챙겨 넣는다."
        ));
        TryBubble(PickRandom("일단 맡아두지.", "필요하면 쓰겠어."));
    }

    public void TriggerEquipmentRetrieved(bool wasEquipped, bool isDanger)
    {
        if (!wasEquipped && !isDanger) return;

        TryLog(PickRandom(
            $"{_stats.NPCName}이(가) 아쉬운 듯 장비를 바라본다.",
            $"{_stats.NPCName}이(가) 불안한 표정을 짓는다."
        ));
        TryBubble(PickRandom("지금 가져간다고?", "그거 없으면 좀 불안한데."));

        GetComponent<CompanionRelationship>()?.AddComplaintScore(5);
    }

    public void TriggerBetrayalWarning()
    {
        TryLog($"{_stats.NPCName}의 눈빛이 싸늘해졌다.");
    }

    public void TriggerRetirementWarning()
    {
        TryLog($"{_stats.NPCName}이(가) 말없이 거리를 둔다.");
        TryBubble("난 이런 식으로는 오래 못 가.");
    }

    // ── 내부 헬퍼 ────────────────────────────────────────

    private void TryLog(string message)
    {
        if (_logCooldown > 0f) return;
        _logCooldown = LogCooldownTime;
        LogManager.AddLog(message);
    }

    private void TryBubble(string text)
    {
        if (_bubbleCooldown > 0f) return;
        _bubbleCooldown = BubbleCooldownTime;
        BubbleManager.ShowBubble(transform, text);
    }

    private static bool Roll(float probability) => Random.value < probability;

    private static string PickRandom(string a, string b)
        => Random.value < 0.5f ? a : b;
}
