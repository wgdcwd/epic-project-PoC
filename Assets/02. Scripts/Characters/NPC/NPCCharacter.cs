using UnityEngine;

/// <summary>
/// 모든 NPC(동료/비동료)의 공통 베이스.
/// </summary>
[RequireComponent(typeof(NPCStats), typeof(StaminaSystem), typeof(NPCInventory))]
public abstract class NPCCharacter : CharacterBase
{
    public NPCStats      NPCStats  { get; private set; }
    public NPCInventory  Inventory { get; private set; }

    protected StaminaSystem StaminaSys { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        NPCStats   = GetComponent<NPCStats>();
        Inventory  = GetComponent<NPCInventory>();
        StaminaSys = GetComponent<StaminaSystem>();

        StaminaSys.Initialize(
            () => NPCStats.Stamina,
            delta => NPCStats.ModifyStamina(delta)
        );
    }
}
