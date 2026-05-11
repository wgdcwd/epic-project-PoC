using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어 파사드. 입력 → 이동/발사/상호작용으로 연결.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerMovement), typeof(PlayerStats))]
[RequireComponent(typeof(Shooter), typeof(StaminaSystem))]
[RequireComponent(typeof(InteractionDetector))]
public sealed class PlayerCharacter : CharacterBase
{
    public static PlayerCharacter Instance { get; private set; }

    public PlayerStats        Stats     { get; private set; }
    public InteractionDetector Detector { get; private set; }

    [Header("Wealth Visual")]
    [SerializeField] private float goldScalePerGold  = 0.0006f;
    [SerializeField] private float goldScaleMaxBonus = 0.6f;

    private Shooter            _shooter;
    private PlayerInputHandler _input;
    private StaminaSystem      _stamina;
    private SpriteRenderer     _sr;
    private Vector3            _baseScale;

    protected override void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        base.Awake();
        Team      = CharacterTeam.Player;
        Stats     = GetComponent<PlayerStats>();
        _shooter  = GetComponent<Shooter>();
        _input    = GetComponent<PlayerInputHandler>();
        _stamina  = GetComponent<StaminaSystem>();
        Detector  = GetComponent<InteractionDetector>();

        _stamina.Initialize(() => Stats.Stamina, delta => Stats.ModifyStamina(delta));

        gameObject.layer = LayerMask.NameToLayer(Layers.Player);

        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null) _baseScale = _sr.transform.localScale;
    }

    void OnEnable()
    {
        _input.OnFirePressed     += HandleFire;
        _input.OnInteractPressed += HandleInteract;
        Health.OnDied            += HandlePlayerDied;
        Health.OnDamaged         += HandlePlayerDamaged;
        Stats.OnGoldChanged      += OnGoldChanged;
        UpdateGoldScale();
    }

    void OnDisable()
    {
        _input.OnFirePressed     -= HandleFire;
        _input.OnInteractPressed -= HandleInteract;
        Health.OnDied            -= HandlePlayerDied;
        Health.OnDamaged         -= HandlePlayerDamaged;
        Stats.OnGoldChanged      -= OnGoldChanged;
    }

    private void OnGoldChanged(int _) => UpdateGoldScale();

    private void UpdateGoldScale()
    {
        if (_sr == null) return;
        float bonus = Mathf.Min(goldScaleMaxBonus, Stats.Gold * goldScalePerGold);
        _sr.transform.localScale = _baseScale * (1f + bonus);
    }

    private void HandlePlayerDied(GameObject _) => GameManager.Instance?.TriggerGameOver();

    private bool _handlingRestDamage;

    /// <summary>
    /// 플레이어 피격 시:
    /// 1) 휴식 중이면 치명타 추가 데미지 + 강제 종료
    /// 2) 모든 동료 점수 즉시 재계산 (HP 약화 보정 반영)
    /// </summary>
    private void HandlePlayerDamaged(float amount, GameObject attacker)
    {
        if (_handlingRestDamage) return; // 재진입 방지

        if (RestSystem.Instance != null && RestSystem.Instance.IsResting)
        {
            _handlingRestDamage = true;
            try
            {
                float extra = Health.CurrentHP * RestSystem.Instance.RestCriticalRatio;
                if (extra > 0f) Health.TakeDamage(extra, attacker);
                BubbleManager.ShowBubble(transform, "크윽, 기습이다!");
            }
            finally { _handlingRestDamage = false; }

            RestSystem.Instance.ForceWake("적의 기습!");
        }

        if (PartyRoster.Instance == null) return;
        var snapshot = new System.Collections.Generic.List<NPCCharacter>(PartyRoster.Instance.Members);
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
