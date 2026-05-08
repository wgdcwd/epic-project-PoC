using UnityEngine;

/// <summary>
/// 전리품 분배.
/// 플레이어 또는 동료가 죽인 경우에만 골드가 들어온다.
/// 비동료 NPC가 죽인 적의 전리품은 무시 (사용자 소속이 아니므로).
/// </summary>
public static class LootSystem
{
    public static void HandleEnemyDeath(int goldDrop, GameObject attacker)
    {
        if (goldDrop <= 0) return;
        if (attacker == null) return;

        int playerLayer    = LayerMask.NameToLayer(Layers.Player);
        int companionLayer = LayerMask.NameToLayer(Layers.Companion);

        // 플레이어/동료가 죽인 경우에만 전리품 인정
        if (attacker.layer != playerLayer && attacker.layer != companionLayer)
            return;

        var player = PlayerCharacter.Instance;
        var roster = PartyRoster.Instance;

        player?.Stats.AddGold(goldDrop);
        roster?.DistributeLoot(goldDrop);

        int heads = roster != null ? roster.HeadCount : 1;
        int share = goldDrop / heads;
        LogManager.AddLog(roster != null && roster.Members.Count > 0
            ? $"전리품 {goldDrop}G 획득. 동료 1인당 미정산 +{share}G."
            : $"전리품 {goldDrop}G 획득.");
    }
}
