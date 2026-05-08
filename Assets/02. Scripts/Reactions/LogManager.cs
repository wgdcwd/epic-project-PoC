using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상황 로그 싱글톤. 정적 이벤트로 UI가 구독.
/// </summary>
public sealed class LogManager : MonoBehaviour
{
    public static LogManager Instance { get; private set; }

    private const int MaxLogs = 50;
    private static readonly Queue<string> _logs = new();

    // UI SituationLogView가 구독
    public static event Action<string> OnLogAdded;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static void AddLog(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        _logs.Enqueue(message);
        if (_logs.Count > MaxLogs) _logs.Dequeue();

        OnLogAdded?.Invoke(message);
        Debug.Log($"[LOG] {message}");
    }

    public static IEnumerable<string> GetRecentLogs() => _logs;
}
