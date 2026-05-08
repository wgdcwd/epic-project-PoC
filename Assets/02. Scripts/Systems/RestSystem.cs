using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 휴식 시스템.
/// 플레이어 + Following/Waiting 동료 모두 정지. 적/배신/도주 NPC는 정상 동작.
/// 공격받거나 배신 발생 시 강제 종료(ForceWake).
/// 시작 시 확률적으로 적 습격 발생.
/// </summary>
public sealed class RestSystem : MonoBehaviour
{
    public static RestSystem Instance { get; private set; }

    [Header("Rest")]
    [SerializeField] private float restDuration     = 5f;
    [SerializeField] private float hpRecoverRatio   = 0.3f;
    [SerializeField] private float staminaRecover   = 70f;
    [SerializeField] private float enemyCheckRadius = 10f;

    [Header("Critical Damage Multiplier (휴식 중 피격 추가 데미지)")]
    [Tooltip("휴식 중 피격 시 현재 HP의 이 비율만큼 추가 데미지")]
    [SerializeField, Range(0f, 1f)] private float restCriticalRatio = 0.5f;
    public float RestCriticalRatio => restCriticalRatio;

    [Header("Ambush")]
    [SerializeField] private GameObject ambushEnemyPrefab;
    [SerializeField, Range(0f, 1f)] private float ambushChanceAlone           = 0.5f;
    [SerializeField, Range(0f, 1f)] private float ambushChanceWithCompanions  = 0.2f;
    [SerializeField] private float                ambushSpawnDistance         = 10f;
    [SerializeField] private Vector2Int           ambushSpawnCountRange       = new(1, 3);

    public bool IsResting { get; private set; }

    public event System.Action       OnRestStarted;
    public event System.Action<bool> OnRestFinished; // true: 정상완료, false: 강제종료

    private Coroutine _restCoroutine;

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
        _restCoroutine = StartCoroutine(RestCoroutine());
        return true;
    }

    /// <summary>휴식 강제 종료. 회복 없이 즉시 깨어남.</summary>
    public void ForceWake(string reason)
    {
        if (!IsResting) return;
        if (_restCoroutine != null) { StopCoroutine(_restCoroutine); _restCoroutine = null; }

        IsResting = false;
        LogManager.AddLog($"잠에서 깨어났다! ({reason})");
        OnRestFinished?.Invoke(false);
    }

    private IEnumerator RestCoroutine()
    {
        IsResting = true;
        OnRestStarted?.Invoke();
        LogManager.AddLog("휴식을 시작했다... (위험할 수 있다)");

        // 적 습격 시도
        TryAmbush();

        // 0.5초 후 동료 자유 시간 판정
        yield return new WaitForSeconds(0.5f);
        if (!IsResting) yield break; // 깨어났으면 종료
        DoCheckRestOpportunity();

        yield return new WaitForSeconds(Mathf.Max(0f, restDuration - 0.5f));
        if (!IsResting) yield break;

        // 회복
        var player = PlayerCharacter.Instance;
        if (player != null)
        {
            player.Health.HealPercent(hpRecoverRatio);
            player.Stats.RestoreStamina(staminaRecover);
        }

        if (PartyRoster.Instance != null)
        {
            var snapshot = new List<CompanionCharacter>(PartyRoster.Instance.Members);
            foreach (var c in snapshot)
            {
                if (c == null) continue;
                c.Health.HealPercent(hpRecoverRatio);
                c.NPCStats.RestoreStamina(staminaRecover);
                c.Relationship.CheckAfterRest();
                c.Relationship.ClearComplaintFlag();
            }
        }

        LogManager.AddLog("휴식을 마쳤다.");
        IsResting = false;
        _restCoroutine = null;
        OnRestFinished?.Invoke(true);
    }

    private void DoCheckRestOpportunity()
    {
        if (PartyRoster.Instance == null) return;
        var snapshot = new List<CompanionCharacter>(PartyRoster.Instance.Members);
        Debug.Log($"[RestSystem] 휴식 중 기회 판정 - {snapshot.Count}명");
        foreach (var c in snapshot)
        {
            if (c == null) continue;
            c.Relationship.CheckRestOpportunity();
        }
    }

    private void TryAmbush()
    {
        if (ambushEnemyPrefab == null) return;
        if (PlayerCharacter.Instance == null) return;

        int companionCount = PartyRoster.Instance != null ? PartyRoster.Instance.Members.Count : 0;
        float chance = companionCount == 0
            ? ambushChanceAlone
            : Mathf.Max(0f, ambushChanceWithCompanions - companionCount * 0.05f);

        if (UnityEngine.Random.value > chance) return;

        int count = UnityEngine.Random.Range(ambushSpawnCountRange.x, ambushSpawnCountRange.y + 1);
        Vector3 playerPos = PlayerCharacter.Instance.transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 pos = playerPos + new Vector3(dir.x, dir.y, 0f) * ambushSpawnDistance;
            var go = Instantiate(ambushEnemyPrefab, pos, Quaternion.identity);

            // 즉시 aggro 모드 - detection 무시하고 플레이어 직진
            var brain = go.GetComponent<EnemyBrain>();
            if (brain != null && PlayerCharacter.Instance != null)
                brain.EnterAggroMode(PlayerCharacter.Instance.transform);
        }

        LogManager.AddLog($"적의 습격! 적 {count}명이 나타났다!");
        BubbleManager.ShowBubble(PlayerCharacter.Instance.transform, "기습이다!");
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
