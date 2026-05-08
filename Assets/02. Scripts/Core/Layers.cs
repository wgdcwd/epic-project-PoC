using UnityEngine;

public static class Layers
{
    public const string Player      = "Player";
    public const string Companion   = "Companion";
    public const string Enemy       = "Enemy";
    public const string WandererNPC = "WandererNPC";

    // 투사체 충돌 마스크: 씬 로드 후 첫 사용 시 초기화
    public static int PlayerProjectileMask   => LayerMask.GetMask(Companion, Enemy, WandererNPC);
    public static int CompanionProjectileMask => LayerMask.GetMask(Enemy);
    public static int CompanionBetrayMask    => LayerMask.GetMask(Player, Enemy);
    public static int EnemyProjectileMask    => LayerMask.GetMask(Player, Companion);

    // 상호작용 감지: 플레이어 주변 NPC 탐색용
    public static int InteractableMask => LayerMask.GetMask(Companion, WandererNPC);

    // 적이 공격 대상으로 삼을 레이어 (플레이어 + 동료)
    public static int EnemyTargetMask => LayerMask.GetMask(Player, Companion);

    // 동료 자동 공격 대상
    public static int CompanionTargetMask => LayerMask.GetMask(Enemy);
}
