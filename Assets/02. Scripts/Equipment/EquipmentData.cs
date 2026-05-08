using UnityEngine;

public enum EquipmentType { Weapon, Armor, Accessory }

/// <summary>
/// 장비 데이터 ScriptableObject. 03. SOs 폴더에 에셋 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "PoC/Equipment")]
public sealed class EquipmentData : ScriptableObject
{
    [Header("Info")]
    public string        itemName = "장비";
    public EquipmentType type     = EquipmentType.Weapon;
    public Sprite        icon;

    [Header("Stats")]
    public float atk     = 0f;
    public float hp      = 0f;
    public float threat  = 0f;
    public float wealth  = 0f;

    [Header("Settlement")]
    public int   value   = 100; // 미정산금 계산에 포함되는 가치

    /// <summary>NPC 성향별 장비 점수 계산</summary>
    public float ScoreFor(float greed, float fear, float morality)
    {
        // 탐욕형
        if (greed >= 70f)
            return atk * 1.0f + hp * 0.8f + threat * 0.5f + wealth * 1.4f;

        // 공포형
        if (fear >= 70f)
            return atk * 0.8f + hp * 1.5f + threat * 0.7f + wealth * 0.2f;

        // 기회주의형 (Morality 낮음)
        if (morality <= 30f)
            return atk * 1.3f + hp * 0.7f + threat * 1.1f + wealth * 0.8f;

        // 기본형
        return atk * 1.0f + hp * 1.0f + threat * 0.7f + wealth * 0.4f;
    }
}
