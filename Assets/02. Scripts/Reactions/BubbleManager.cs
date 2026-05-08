using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 말풍선 오브젝트 풀 싱글톤.
/// </summary>
public sealed class BubbleManager : MonoBehaviour
{
    public static BubbleManager Instance { get; private set; }

    [SerializeField] private SpeechBubble bubblePrefab;
    [SerializeField] private int          poolSize = 8;

    private readonly Queue<SpeechBubble> _pool = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // 풀 초기화
        for (int i = 0; i < poolSize; i++)
        {
            var b = Instantiate(bubblePrefab, transform);
            b.gameObject.SetActive(false);
            _pool.Enqueue(b);
        }
    }

    /// <summary>정적 진입점. NPC 컴포넌트에서 호출.</summary>
    public static void ShowBubble(Transform target, string text)
    {
        Instance?.Show(target, text);
    }

    private void Show(Transform target, string text)
    {
        SpeechBubble bubble;
        if (_pool.Count > 0)
            bubble = _pool.Dequeue();
        else
            bubble = Instantiate(bubblePrefab, transform); // 풀 부족 시 동적 생성

        bubble.Show(target, text);
    }

    public void ReturnBubble(SpeechBubble bubble)
    {
        bubble.gameObject.SetActive(false);
        _pool.Enqueue(bubble);
    }
}
