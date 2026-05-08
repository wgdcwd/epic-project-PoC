using UnityEngine;

/// <summary>
/// 전리품 분배.
/// - Player/Companion이 죽인 경우 → 플레이어 + 동료 미정산 분배
/// - Wanderer NPC가 죽인 경우 → 그 NPC 자기 지갑에 누적 (사망 시 회수 가능)
/// - 그 외 (Enemy → Enemy 등) → 무시
/// </summary>
public static class LootSystem
{
    public static void HandleEnemyDeath(int goldDrop, GameObject attacker)
    {
        if (goldDrop <= 0) return;
        if (attacker == null) return;

        int playerLayer    = LayerMask.NameToLayer(Layers.Player);
        int companionLayer = LayerMask.NameToLayer(Layers.Companion);
        int wandererLayer  = LayerMask.NameToLayer(Layers.WandererNPC);

        if (attacker.layer == playerLayer || attacker.layer == companionLayer)
        {
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
        else if (attacker.layer == wandererLayer)
        {
            // Wanderer가 죽인 경우 자기 지갑에 누적 (사용자에게는 안 가지만 사망 시 회수 가능)
            var stats = attacker.GetComponent<NPCStats>();
            if (stats != null) stats.AddGold(goldDrop);
        }
    }
}
