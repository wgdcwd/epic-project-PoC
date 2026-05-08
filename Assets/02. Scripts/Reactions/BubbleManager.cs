using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 말풍선 오브젝트 풀 싱글톤. NPC 1명당 활성 말풍선은 1개만 유지.
/// </summary>
public sealed class BubbleManager : MonoBehaviour
{
    public static BubbleManager Instance { get; private set; }

    [SerializeField] private SpeechBubble bubblePrefab;
    [SerializeField] private int          poolSize = 8;

    private readonly Queue<SpeechBubble>                _pool          = new();
    private readonly Dictionary<Transform, SpeechBubble> _activeBubbles = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < poolSize; i++)
        {
            var b = Instantiate(bubblePrefab, transform);
            b.gameObject.SetActive(false);
            _pool.Enqueue(b);
        }
    }

    public static void ShowBubble(Transform target, string text)
    {
        Instance?.Show(target, text);
    }

    private void Show(Transform target, string text)
    {
        if (target == null) return;

        // 같은 NPC의 활성 풍선이 있으면 재사용 (덮어쓰기)
        if (_activeBubbles.TryGetValue(target, out var existing))
        {
            existing.Show(target, text);
            return;
        }

        SpeechBubble bubble = _pool.Count > 0
            ? _pool.Dequeue()
            : Instantiate(bubblePrefab, transform);

        _activeBubbles[target] = bubble;
        bubble.Show(target, text);
    }

    public void ReturnBubble(SpeechBubble bubble)
    {
        if (bubble == null) return;
        bubble.gameObject.SetActive(false);

        // 활성 매핑에서 제거 (자신을 가리키는 키 찾기)
        Transform keyToRemove = null;
        foreach (var kv in _activeBubbles)
        {
            if (kv.Value == bubble) { keyToRemove = kv.Key; break; }
        }
        if (keyToRemove != null) _activeBubbles.Remove(keyToRemove);

        _pool.Enqueue(bubble);
    }
}
