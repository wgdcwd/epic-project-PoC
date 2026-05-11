using UnityEngine;

/// <summary>
/// NPC 사망 시 보유 골드를 월드에 드랍.
/// 장비 시스템 제거로 골드 드랍만 처리.
/// </summary>
public static class NPCDropHandler
{
    public static void HandleDrop(NPCStats stats, Vector3 position)
    {
        if (stats == null) return;

        int goldHeld = stats.TakeAllGold();
        if (goldHeld > 0)
        {
            LootSystem.SpawnGoldPickup(goldHeld, position);
            LogManager.AddLog($"{stats.NPCName}이(가) {goldHeld}G를 떨어뜨렸다.");
        }
    }
}
