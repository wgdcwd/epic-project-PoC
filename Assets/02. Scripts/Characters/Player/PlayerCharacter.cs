using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어 파사드. 입력 → 이동/발사/상호작용으로 연결.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerMovement), typeof(PlayerStats))]
[RequireComponent(typeof(Shooter), typeof(StaminaSystem), typeof(PlayerInventory))]
[RequireComponent(typeof(InteractionDetector))]
public sealed class PlayerCharacter : CharacterBase
{
    public static PlayerCharacter Instance { get; private set; }

    public PlayerStats        Stats     { get; private set; }
    public PlayerInventory    Inventory { get; private set; }
    public InteractionDetector Detector { get; private set; }

    private Shooter            _shooter;
    private PlayerInputHandler _input;
    private StaminaSystem      _stamina;

    protected override void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        base.Awake();
        Team      = CharacterTeam.Player;
        Stats     = GetComponent<PlayerStats>();
        Inventory = GetComponent<PlayerInventory>();
        _shooter  = GetComponent<Shooter>();
        _input    = GetComponent<PlayerInputHandler>();
        _stamina  = GetComponent<StaminaSystem>();
        Detector  = GetComponent<InteractionDetector>();

        _stamina.Initialize(() => Stats.Stamina, delta => Stats.ModifyStamina(delta));

        gameObject.layer = LayerMask.NameToLayer(Layers.Player);
    }

    void OnEnable()
    {
        _input.OnFirePressed     += HandleFire;
        _input.OnInteractPressed += HandleInteract;
        _input.OnRestPressed     += HandleRest;
        Health.OnDied            += HandlePlayerDied;
        Health.OnDamaged         += HandlePlayerDamaged;
    }

    void OnDisable()
    {
        _input.OnFirePressed     -= HandleFire;
        _input.OnInteractPressed -= HandleInteract;
        _input.OnRestPressed     -= HandleRest;
        Health.OnDied            -= HandlePlayerDied;
        Health.OnDamaged         -= HandlePlayerDamaged;
    }

    private void HandlePlayerDied(GameObject _) => GameManager.Instance?.TriggerGameOver();

    /// <summary>
    /// 플레이어가 피격될 때마다 모든 동료가 즉시 기회 판정.
    /// HP/Stamina 약화 보정이 배신 점수에 반영되어 자연스럽게 평소에도 배신 가능.
    /// </summary>
    private void HandlePlayerDamaged(float amount, GameObject attacker)
    {
        if (PartyRoster.Instance == null) return;
        var snapshot = new System.Collections.Generic.List<CompanionCharacter>(PartyRoster.Instance.Members);
        foreach (var c in snapshot)
        {
            if (c == null) continue;
            c.Relationship.CheckImmediateThresholds();
        }
    }

    private void HandleFire(Vector2 worldPos)
    {
        if (!CanAct()) return;
        if (IsPointerOverUI()) return;        // UI 위 클릭은 게임 액션 차단
        bool fired = _shooter.TryFireAt(worldPos);
        if (fired) _stamina.OnRangedAttack();
    }

    private void HandleInteract(Vector2 worldPos)
    {
        if (!CanAct()) return;
        if (IsPointerOverUI()) return;        // UI 위 우클릭도 차단
        Detector?.TryInteract(worldPos);
    }

    private void HandleRest()
    {
        if (!CanAct()) return;
        RestSystem.Instance?.TryRest();
    }

    /// <summary>게임 진행 중 + 휴식 중 아님</summary>
    private bool CanAct()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
        if (RestSystem.Instance != null && RestSystem.Instance.IsResting)    return false;
        return true;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    protected override void HandleDeath(GameObject attacker)
    {
        // 게임오버는 HealthComponent OnDied 구독으로 처리
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }
}
