using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동료 1명의 HUD 카드.
/// NPCCharacter 할당 후 이벤트를 구독해 자동 갱신.
/// </summary>
public sealed class CompanionCardView : MonoBehaviour
{
    [Header("Info")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider          hpBar;
    [SerializeField] private Slider          staminaBar;
    [SerializeField] private Slider          trustBar;
    [SerializeField] private TextMeshProUGUI unpaidText;

    [Header("Equipment Icons")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private Image armorIcon;
    [SerializeField] private Image accessoryIcon;

    [Header("Warning Icons")]
    [SerializeField] private GameObject retirementWarningIcon;
    [SerializeField] private GameObject betrayalWarningIcon;
    [SerializeField] private GameObject waitingIcon; // 대기 상태 표시 (옵션)

    private NPCCharacter _companion;

    public void Bind(NPCCharacter companion)
    {
        Unbind();
        _companion = companion;

        companion.Stats.OnTrustChanged   += OnTrustChanged;
        companion.Stats.OnStaminaChanged += RefreshStamina;
        companion.Health.OnHPChanged        += RefreshHP;
        companion.Relationship.OnUnpaidChanged    += RefreshUnpaid;
        companion.Relationship.OnBetrayalWarning  += ShowBetrayalWarning;
        companion.Relationship.OnRetirementWarning += ShowRetirementWarning;
        companion.Inventory.Slots.OnEquipmentChanged += RefreshEquipment;
        companion.OnBehaviorChanged += OnBehaviorChanged;

        RefreshAll();
    }

    private void OnBehaviorChanged(NPCBehavior behavior)
    {
        if (waitingIcon != null) waitingIcon.SetActive(behavior == NPCBehavior.Waiting);
    }

    public void Unbind()
    {
        if (_companion == null) return;
        _companion.Stats.OnTrustChanged   -= OnTrustChanged;
        _companion.Stats.OnStaminaChanged -= RefreshStamina;
        _companion.Health.OnHPChanged        -= RefreshHP;
        _companion.Relationship.OnUnpaidChanged    -= RefreshUnpaid;
        _companion.Relationship.OnBetrayalWarning  -= ShowBetrayalWarning;
        _companion.Relationship.OnRetirementWarning -= ShowRetirementWarning;
        _companion.Inventory.Slots.OnEquipmentChanged -= RefreshEquipment;
        _companion.OnBehaviorChanged -= OnBehaviorChanged;
        _companion = null;
    }

    void OnDestroy() => Unbind();

    private void RefreshAll()
    {
        if (_companion == null) return;
        if (nameText != null) nameText.text = _companion.Stats.NPCName;
        RefreshHP();
        RefreshStamina();
        OnTrustChanged(_companion.Stats.Trust);
        RefreshUnpaid(_companion.Relationship.UnpaidAmount);
        RefreshEquipment();
        OnBehaviorChanged(_companion.Behavior);
    }

    private void RefreshHP()
    {
        if (hpBar != null && _companion != null)
            hpBar.value = _companion.Health.HPRatio;
    }

    private void RefreshStamina()
    {
        if (staminaBar != null && _companion != null)
            staminaBar.value = _companion.Stats.Stamina / _companion.Stats.MaxStamina;
    }

    private void OnTrustChanged(float trust)
    {
        if (trustBar != null) trustBar.value = trust / 100f;

        // Trust 색상
        if (trustBar != null)
        {
            var fill = trustBar.fillRect?.GetComponent<Image>();
            if (fill != null)
                fill.color = trust >= 70f ? Color.green : trust >= 40f ? Color.yellow : Color.red;
        }

        // 경고 갱신
        RefreshWarnings();
    }

    private void RefreshUnpaid(float amount)
    {
        if (unpaidText != null) unpaidText.text = $"미정산 {amount:F0}G";
    }

    private void RefreshEquipment()
    {
        if (_companion == null) return;
        var slots = _companion.Inventory.Slots;
        if (weaponIcon    != null) weaponIcon.sprite    = slots.Weapon?.icon;
        if (armorIcon     != null) armorIcon.sprite     = slots.Armor?.icon;
        if (accessoryIcon != null) accessoryIcon.sprite = slots.Accessory?.icon;

        bool hasWeapon    = slots.Weapon    != null;
        bool hasArmor     = slots.Armor     != null;
        bool hasAccessory = slots.Accessory != null;
        if (weaponIcon)    weaponIcon.enabled    = hasWeapon;
        if (armorIcon)     armorIcon.enabled     = hasArmor;
        if (accessoryIcon) accessoryIcon.enabled = hasAccessory;
    }

    private void RefreshWarnings()
    {
        if (_companion == null) return;
        int retire = _companion.Relationship.CalculateRetirementScore();
        int betray = _companion.Relationship.CalculateBetrayalScore();

        if (retirementWarningIcon != null) retirementWarningIcon.SetActive(retire >= 80);
        if (betrayalWarningIcon   != null) betrayalWarningIcon.SetActive(betray >= 120);
    }

    private void ShowBetrayalWarning()   => RefreshWarnings();
    private void ShowRetirementWarning() => RefreshWarnings();
}
