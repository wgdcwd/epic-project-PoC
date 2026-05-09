using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장비 대여/회수용 인벤토리 팝업.
/// 모드에 따라 플레이어 인벤토리 또는 NPC 인벤토리를 나열.
/// </summary>
public sealed class InventoryView : MonoBehaviour
{
    public enum Mode { LendToNPC, RetrieveFromNPC }

    [SerializeField] private GameObject       panel;
    [SerializeField] private Transform        slotContainer;
    [SerializeField] private GameObject       slotPrefab;    // Button + Text + Icon
    [SerializeField] private TextMeshProUGUI  titleText;
    [SerializeField] private Button           cancelBtn;

    private NPCCharacter _target;
    private Mode               _mode;
    private Action             _onClose;

    void Awake()
    {
        cancelBtn.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    public void OpenLend(NPCCharacter companion, Action onClose = null)
    {
        _target  = companion;
        _mode    = Mode.LendToNPC;
        _onClose = onClose;
        if (titleText != null) titleText.text = $"{companion.Stats.NPCName}에게 대여할 장비 선택";
        PopulateSlots();
        panel.SetActive(true);
    }

    public void OpenRetrieve(NPCCharacter companion, Action onClose = null)
    {
        _target  = companion;
        _mode    = Mode.RetrieveFromNPC;
        _onClose = onClose;
        if (titleText != null) titleText.text = $"{companion.Stats.NPCName}에게서 회수할 장비 선택";
        PopulateSlots();
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
        _onClose?.Invoke();
        _target  = null;
        _onClose = null;

        // 슬롯 정리
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);
    }

    private void PopulateSlots()
    {
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);

        IReadOnlyList<EquipmentData> items = _mode == Mode.LendToNPC
            ? PlayerCharacter.Instance?.Inventory.Slots.Inventory
            : _target?.Inventory.Slots.Inventory;

        if (items == null) return;

        foreach (var item in items)
        {
            var go  = Instantiate(slotPrefab, slotContainer);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TextMeshProUGUI>();
            var img = go.GetComponentInChildren<Image>();

            if (txt != null) txt.text = item.itemName;
            if (img != null && item.icon != null) img.sprite = item.icon;

            var captured = item; // 클로저 캡처
            btn?.onClick.AddListener(() => OnItemSelected(captured));
        }
    }

    private void OnItemSelected(EquipmentData item)
    {
        if (_target == null) { Close(); return; }

        var playerInv = PlayerCharacter.Instance?.Inventory;
        if (playerInv == null) { Close(); return; }

        bool success = _mode == Mode.LendToNPC
            ? playerInv.LendTo(item, _target.Inventory)
            : playerInv.RetrieveFrom(item, _target.Inventory);

        if (success)
        {
            string action = _mode == Mode.LendToNPC ? "대여" : "회수";
            LogManager.AddLog($"{_target.Stats.NPCName}에게 {item.itemName} {action}.");
        }

        Close();
    }
}
