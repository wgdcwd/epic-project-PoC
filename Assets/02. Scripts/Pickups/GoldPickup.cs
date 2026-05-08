using TMPro;
using UnityEngine;

/// <summary>
/// 골드 주머니 픽업. Initialize(amount)으로 금액 설정.
/// 픽업 시 플레이어 골드 추가 + 동료 미정산 분배.
/// </summary>
public sealed class GoldPickup : PickupBase
{
    [SerializeField] private int             amount;
    [SerializeField] private TextMeshPro     amountLabel;   // 옵션: 떠있는 금액 표시

    public void Initialize(int amount)
    {
        this.amount = amount;
        if (amountLabel != null) amountLabel.text = $"{amount}G";
    }

    protected override void OnPickup(PlayerCharacter player)
    {
        if (amount <= 0) return;

        player.Stats.AddGold(amount);

        // 픽업 시점에 동료 미정산 분배
        var roster = PartyRoster.Instance;
        if (roster != null && roster.Members.Count > 0)
        {
            roster.DistributeLoot(amount);
            int share = amount / roster.HeadCount;
            LogManager.AddLog($"{amount}G 획득. 동료 1인당 미정산 +{share}G.");
        }
        else
        {
            LogManager.AddLog($"{amount}G 획득.");
        }
    }

    /// <summary>NPC가 줍는 경로. 성공 시 NPC 지갑에 누적 후 자체 파괴. (로그 없음 — 사용자 무관)</summary>
    public bool TryPickupBy(NPCStats stats)
    {
        if (stats == null || amount <= 0) return false;
        if (!TryClaim()) return false;
        stats.AddGold(amount);
        Destroy(gameObject);
        return true;
    }
}
