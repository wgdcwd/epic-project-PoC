using UnityEngine;

/// <summary>
/// 그리드 기반 절차적 맵 생성기.
/// 동일 크기의 방 프리팹을 격자로 배치하고, 각 방에 Wanderer/Enemy를 랜덤 스폰.
///
/// 방 프리팹은 동서남북으로 길이 뚫려 있는 형태로 사용자가 직접 제작.
/// 사용자는 RoomPrefabs / WandererPrefabs / EnemyPrefabs를 인스펙터에 등록.
/// </summary>
public sealed class MapGenerator : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int   gridX    = 4;
    [SerializeField] private int   gridY    = 4;
    [Tooltip("방 한 칸의 한 변 길이 (월드 단위)")]
    [SerializeField] private float roomSize = 12f;

    [Header("Prefabs")]
    [SerializeField] private GameObject[] roomPrefabs;
    [SerializeField] private GameObject[] wandererPrefabs;
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Spawn Counts (per room)")]
    [SerializeField] private Vector2Int wandererCountRange = new(0, 2);
    [SerializeField] private Vector2Int enemyCountRange    = new(2, 6);

    [Tooltip("방 중심에서 이 비율 안쪽에만 스폰 (0.4 = 방 면적의 80%)")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float spawnAreaRatio = 0.35f;

    [Header("Player Start")]
    [Tooltip("이 좌표 방은 적/NPC 스폰 안 함 (안전구역)")]
    [SerializeField] private Vector2Int safeRoomCoord = new(0, 0);
    [Tooltip("씬의 Player를 안전 방 중심으로 이동시킬지")]
    [SerializeField] private bool       movePlayerToSafeRoom = true;

    [Header("Random Seed")]
    [Tooltip("0이면 매번 다른 맵, 0 외 값이면 고정 시드")]
    [SerializeField] private int seed = 0;

    [Header("Auto Generate")]
    [SerializeField] private bool generateOnStart = true;

    void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.LogError("[MapGenerator] roomPrefabs가 비어있습니다.");
            return;
        }

        if (seed != 0) Random.InitState(seed);

        // 기존 자식 정리 (재생성 대비)
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                Vector3 center = new(x * roomSize, y * roomSize, 0f);

                // 방 인스턴스화
                var roomPrefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
                Instantiate(roomPrefab, center, Quaternion.identity, transform);

                bool isSafe = (x == safeRoomCoord.x && y == safeRoomCoord.y);
                if (isSafe) continue;

                SpawnInRoom(center, wandererPrefabs, wandererCountRange);
                SpawnInRoom(center, enemyPrefabs,    enemyCountRange);
            }
        }

        // 플레이어 위치 조정
        if (movePlayerToSafeRoom && PlayerCharacter.Instance != null)
        {
            Vector3 safeCenter = new(safeRoomCoord.x * roomSize, safeRoomCoord.y * roomSize, 0f);
            PlayerCharacter.Instance.transform.position = safeCenter;
        }
    }

    private void SpawnInRoom(Vector3 center, GameObject[] prefabs, Vector2Int countRange)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        int count = Random.Range(countRange.x, countRange.y + 1);
        float halfArea = roomSize * spawnAreaRatio;

        for (int i = 0; i < count; i++)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            Vector3 offset = new(
                Random.Range(-halfArea, halfArea),
                Random.Range(-halfArea, halfArea),
                0f
            );
            Instantiate(prefab, center + offset, Quaternion.identity, transform);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                Vector3 center = new(x * roomSize, y * roomSize, 0f);
                Gizmos.DrawWireCube(center, new Vector3(roomSize, roomSize, 0f));
            }
        }

        Gizmos.color = Color.green;
        Vector3 safe = new(safeRoomCoord.x * roomSize, safeRoomCoord.y * roomSize, 0f);
        Gizmos.DrawWireCube(safe, new Vector3(roomSize * 0.95f, roomSize * 0.95f, 0f));
    }
}
