using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 비동료 NPC 우클릭 메뉴: [영입 제안] [상태 확인] [무시]
/// </summary>
public sealed class InteractionMenuView : MonoBehaviour
{
    [SerializeField] private GameObject       panel;
    [SerializeField] private TextMeshProUGUI  npcNameText;
    [SerializeField] private Button           recruitBtn;
    [SerializeField] private Button           statusBtn;
    [SerializeField] private Button           ignoreBtn;

    private NPCCharacter _target;

    void Awake()
    {
        recruitBtn.onClick.AddListener(OnRecruit);
        statusBtn.onClick .AddListener(OnStatus);
        ignoreBtn.onClick .AddListener(Close);
        panel.SetActive(false);
    }

    public void Open(NPCCharacter wanderer)
    {
        _target = wanderer;
        if (npcNameText != null) npcNameText.text = wanderer.Stats.NPCName;
        panel.SetActive(true);
    }

    public void Close()
    {
        _target = null;
        panel.SetActive(false);
    }

    private void OnRecruit()
    {
        if (_target == null) { Close(); return; }

        var ps = PlayerCharacter.Instance?.Stats;
        if (ps == null) { Close(); return; }
        var npcSt = _target.Stats;
        float score = RecruitmentSystem.CalculateScore(ps, npcSt);

        if (RecruitmentSystem.CanRecruit(score))
        {
            float initTrust = RecruitmentSystem.CalculateInitialTrust(npcSt, score);
            _target.JoinAsCompanion(initTrust);
            LogManager.AddLog($"{npcSt.NPCName}이(가) 파티에 합류했다. (영입점수 {score:F1}, 초기 Trust {initTrust:F0})");
        }
        else
        {
            string line = RecruitmentSystem.GetRejectionLine(npcSt);
            BubbleManager.ShowBubble(_target.transform, line);
            LogManager.AddLog($"{npcSt.NPCName}이(가) 영입을 거절했다. (영입점수 {score:F1} / 50)");
        }

        Close();
    }

    private void OnStatus()
    {
        if (_target == null) { Close(); return; }
        var s = _target.Stats;
        LogManager.AddLog($"[{s.NPCName}] Trust:{s.Trust:F0} Greed:{s.Greed:F0} Fear:{s.Fear:F0} Morality:{s.Morality:F0}");
        Close();
    }
}
