using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 말풍선 오브젝트 풀 싱글톤. NPC 1명당 활성 말풍선은 1개로 강제 보장.
/// 풀/매핑에 destroyed reference가 섞여 들어와도 자동으로 스킵.
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

        SpeechBubble bubble = null;

        // 1차: Dictionary 매핑 (destroyed reference 정리)
        if (_activeBubbles.TryGetValue(target, out var mapped))
        {
            if (mapped != null) bubble = mapped;
            else _activeBubbles.Remove(target);
        }

        // 2차: 자식 전체 검사로 같은 target 풍선 회수 (Dict 깨졌을 때 보정)
        if (bubble == null)
        {
            foreach (Transform child in transform)
            {
                if (child == null) continue;
                if (!child.gameObject.activeSelf) continue;

                var b = child.GetComponent<SpeechBubble>();
                if (b != null && b.Target == target) { bubble = b; break; }
            }
        }

        // 활성 풍선 재사용 (덮어쓰기)
        if (bubble != null)
        {
            _activeBubbles[target] = bubble;
            bubble.Show(target, text);
            return;
        }

        // 풀에서 안전하게 발급 (destroyed 항목 자동 스킵)
        bubble = DequeueAlive();
        if (bubble == null) bubble = Instantiate(bubblePrefab, transform);

        _activeBubbles[target] = bubble;
        bubble.Show(target, text);
    }

    private SpeechBubble DequeueAlive()
    {
        while (_pool.Count > 0)
        {
            var candidate = _pool.Dequeue();
            if (candidate != null) return candidate;
        }
        return null;
    }

    public void ReturnBubble(SpeechBubble bubble)
    {
        if (bubble == null) return;          // 이미 destroyed
        bubble.gameObject.SetActive(false);

        // 매핑에서 자기 자신을 가리키는 키 제거
        Transform keyToRemove = null;
        foreach (var kv in _activeBubbles)
        {
            if (kv.Value == bubble) { keyToRemove = kv.Key; break; }
        }
        if (keyToRemove != null) _activeBubbles.Remove(keyToRemove);

        _pool.Enqueue(bubble);
    }
}
