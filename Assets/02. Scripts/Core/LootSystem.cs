/// <summary>
/// 전리품 분배.
/// 골드는 플레이어가 전부 받고, 동료들의 미정산금에 1인당 몫이 누적된다.
/// 플레이어는 자신의 골드에서 결정한 만큼만 정산하면 된다.
/// </summary>
public static class LootSystem
{
    public static void HandleEnemyDeath(int goldDrop)
    {
        if (goldDrop <= 0) return;

        var player = PlayerCharacter.Instance;
        var roster = PartyRoster.Instance;

        // 플레이어가 골드를 모두 획득
        player?.Stats.AddGold(goldDrop);

        // 동료별 미정산금 누적 (전리품 총 가치를 분배 인원으로 나눈 몫)
        roster?.DistributeLoot(goldDrop);

        int heads = roster != null ? roster.HeadCount : 1;
        int share = goldDrop / heads;
        LogManager.AddLog(roster != null && roster.Members.Count > 0
            ? $"전리품 {goldDrop}G 획득. 동료 1인당 미정산 +{share}G."
            : $"전리품 {goldDrop}G 획득.");
    }
}
