using UnityEngine;

/// <summary>
/// 영입 점수 계산 및 초기 Trust 산정. 순수 C# 정적 클래스.
/// </summary>
public static class RecruitmentSystem
{
    public static float CalculateScore(PlayerStats player, NPCStats npc)
    {
        return 50f
             + player.FinalThreat * 0.2f
             + player.FinalWealth * 0.1f
             - npc.Fear           * 0.3f
             + npc.Greed          * 0.1f
             + (npc.Morality - 50f) * 0.1f;
    }

    public static bool CanRecruit(float score) => score >= 50f;

    public static float CalculateInitialTrust(NPCStats npc, float score)
    {
        float calculated = 40f + (score - 50f) * 0.2f;
        return Mathf.Max(npc.Trust, calculated);
    }

    /// <summary>영입 거절 말풍선 텍스트 선택</summary>
    public static string GetRejectionLine(NPCStats npc)
    {
        if (npc.Fear >= 70f)
            return Random.value < 0.5f ? "난 너랑 못 가." : "위험해 보여.";
        if (npc.Greed >= 70f)
            return Random.value < 0.5f ? "조건이 별로인데?" : "내 몫은 확실한 거지?";
        return "당신을 아직 믿을 수 없군.";
    }
}
