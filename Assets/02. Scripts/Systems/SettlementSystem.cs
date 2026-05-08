using UnityEngine;

/// <summary>
/// 정산 Trust 변화 계산. 순수 C# 정적 클래스 (MonoBehaviour 없음).
/// </summary>
public static class SettlementSystem
{
    /// <summary>
    /// 정산 결과 Trust 변화량 반환.
    /// </summary>
    /// <param name="ratio">지급 비율 (지급 골드 / 미정산금)</param>
    /// <param name="greed">NPC Greed (0~100)</param>
    public static float CalculateTrustDelta(float ratio, float greed)
    {
        float baseDelta;

        if (ratio <= 0f)
        {
            baseDelta = -15f;
        }
        else if (ratio < 0.8f)
        {
            float shortage = 0.8f - ratio;
            baseDelta = -5f - (shortage * 25f);
        }
        else if (ratio <= 1.0f)
        {
            baseDelta = 3f;
        }
        else
        {
            float excess  = ratio - 1.0f;
            baseDelta = 3f + Mathf.Min(excess * 20f, 10f);
        }

        // Greed 보정 (0.7 ~ 1.5)
        float greedMod = Mathf.Clamp(1f + (greed - 50f) / 100f, 0.7f, 1.5f);
        return baseDelta * greedMod;
    }
}
