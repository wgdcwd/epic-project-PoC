using System.Collections;
using UnityEngine;

/// <summary>
/// 휴식 시스템. 적이 감지 범위에 없을 때만 활성화.
/// 5초 후 전체 HP/Stamina 회복 후 동료 이벤트 판정.
/// </summary>
public sealed class RestSystem : MonoBehaviour
{
    public static RestSystem Instance { get; private set; }

    [SerializeField] private float restDuration     = 5f;
    [SerializeField] private float hpRecoverRatio   = 0.3f;  // +30%
    [SerializeField] private float staminaRecover   = 70f;
    [SerializeField] private float enemyCheckRadius = 10f;

    public bool IsResting { get; private set; }

    public event System.Action       OnRestStarted;
    public event System.Action<bool> OnRestFinished; // bool: 완료 여부

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TryRest()
    {
        if (IsResting) return false;
        if (IsEnemyNearby())
        {
            LogManager.AddLog("적이 근처에 있어 휴식할 수 없다.");
            return false;
        }
        StartCoroutine(RestCoroutine());
        return true;
    }

    private IEnumerator RestCoroutine()
    {
        IsResting = true;
        OnRestStarted?.Invoke();
        LogManager.AddLog("휴식을 시작했다...");

        yield return new WaitForSeconds(restDuration);

        // 플레이어 회복
        var player = PlayerCharacter.Instance;
        if (player != null)
        {
            player.Health.HealPercent(hpRecoverRatio);
            player.Stats.RestoreStamina(staminaRecover);
        }

        // 동료 회복 + 이벤트 판정
        if (PartyRoster.Instance != null)
        {
            foreach (var companion in PartyRoster.Instance.Members)
            {
                companion.Health.HealPercent(hpRecoverRatio);
                companion.NPCStats.RestoreStamina(staminaRecover);
            }

            // 이탈로 목록 변경 가능하므로 복사 후 순회
            var snapshot = new System.Collections.Generic.List<CompanionCharacter>(PartyRoster.Instance.Members);
            foreach (var companion in snapshot)
            {
                companion.Relationship.CheckAfterRest();
                companion.Relationship.ClearComplaintFlag();
            }
        }

        LogManager.AddLog("휴식을 마쳤다.");
        IsResting = false;
        OnRestFinished?.Invoke(true);
    }

    private bool IsEnemyNearby()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            PlayerCharacter.Instance != null ? PlayerCharacter.Instance.transform.position : transform.position,
            enemyCheckRadius,
            LayerMask.GetMask(Layers.Enemy));
        return hits.Length > 0;
    }
}
