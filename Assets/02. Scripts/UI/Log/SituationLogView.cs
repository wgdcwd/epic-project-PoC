using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 하단 상황 로그 패널. 최근 5줄 표시.
/// LogManager.OnLogAdded 이벤트를 구독.
/// </summary>
public sealed class SituationLogView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private int             maxLines = 5;

    private readonly Queue<string> _lines = new();

    void OnEnable()  => LogManager.OnLogAdded += AppendLine;
    void OnDisable() => LogManager.OnLogAdded -= AppendLine;

    void Start()
    {
        // 기존 로그 복원
        foreach (var log in LogManager.GetRecentLogs())
            AppendLine(log);
    }

    private void AppendLine(string message)
    {
        _lines.Enqueue(message);
        if (_lines.Count > maxLines) _lines.Dequeue();

        if (logText != null)
            logText.text = string.Join("\n", _lines);
    }
}
