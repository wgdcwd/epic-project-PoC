using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 HUD: HP/Stamina 바, Gold, ATK/Threat/Wealth 텍스트.
/// PlayerStats와 HealthComponent 이벤트를 구독해 갱신.
/// </summary>
public sealed class PlayerHUDView : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider staminaBar;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI atkText;
    [SerializeField] private TextMeshProUGUI threatText;
    [SerializeField] private TextMeshProUGUI wealthText;

    private PlayerStats     _stats;
    private HealthComponent _health;

    void Start()
    {
        var player = PlayerCharacter.Instance;
        if (player == null) return;

        _stats  = player.Stats;
        _health = player.Health;

        _stats.OnStatsChanged   += RefreshBars;
        _stats.OnGoldChanged    += RefreshGold;
        _health.OnHPChanged     += RefreshBars;

        RefreshAll();
    }

    void OnDestroy()
    {
        if (_stats  != null) { _stats.OnStatsChanged -= RefreshBars; _stats.OnGoldChanged -= RefreshGold; }
        if (_health != null) _health.OnHPChanged -= RefreshBars;
    }

    private void RefreshAll()
    {
        RefreshBars();
        RefreshGold(_stats.Gold);
    }

    private void RefreshBars()
    {
        if (_health != null && hpBar      != null) hpBar.value      = _health.HPRatio;
        if (_stats  != null && staminaBar != null) staminaBar.value = _stats.Stamina / _stats.MaxStamina;
        if (atkText    != null) atkText.text    = $"ATK {_stats.FinalATK:F0}";
        if (threatText != null) threatText.text = $"위압 {_stats.FinalThreat:F0}";
        if (wealthText != null) wealthText.text = $"재력 {_stats.FinalWealth:F0}";
    }

    private void RefreshGold(int gold)
    {
        if (goldText != null) goldText.text = $"{gold:N0} G";
    }
}
