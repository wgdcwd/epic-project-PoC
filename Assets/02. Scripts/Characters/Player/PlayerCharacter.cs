using UnityEngine;

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
    }

    void OnDisable()
    {
        _input.OnFirePressed     -= HandleFire;
        _input.OnInteractPressed -= HandleInteract;
        _input.OnRestPressed     -= HandleRest;
        Health.OnDied            -= HandlePlayerDied;
    }

    private void HandlePlayerDied(GameObject _) => GameManager.Instance?.TriggerGameOver();

    private void HandleFire(Vector2 worldPos)
    {
        if (!GameManager.Instance.IsPlaying) return;
        bool fired = _shooter.TryFireAt(worldPos);
        if (fired) _stamina.OnRangedAttack();
    }

    private void HandleInteract(Vector2 worldPos)
    {
        if (!GameManager.Instance.IsPlaying) return;
        Detector?.TryInteract(worldPos);
    }

    private void HandleRest()
    {
        if (!GameManager.Instance.IsPlaying) return;
        RestSystem.Instance?.TryRest();
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
