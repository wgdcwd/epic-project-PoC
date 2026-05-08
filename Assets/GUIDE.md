# 게임 시스템 상세 문서 (Implementation Guide)

> 이 문서는 **실제 구현된 시스템**의 상세 명세이다.
> README.md는 초기 기획서이며, 구현 과정에서 변경/추가된 내용은 이 문서가 기준이다.
> 모르는 사람이 읽어도 이해하고 재현할 수 있도록 모든 공식, 임계값, 케이스를 명시한다.

---

## 목차

1. [개요](#1-개요)
2. [핵심 게임 루프](#2-핵심-게임-루프)
3. [조작](#3-조작)
4. [캐릭터 종류와 진영](#4-캐릭터-종류와-진영)
5. [스탯 시스템](#5-스탯-시스템)
6. [전투 시스템](#6-전투-시스템)
7. [영입 시스템](#7-영입-시스템)
8. [정산 시스템](#8-정산-시스템)
9. [장비 시스템](#9-장비-시스템)
10. [인벤토리 / 지갑 / 회수](#10-인벤토리--지갑--회수)
11. [휴식 시스템](#11-휴식-시스템)
12. [동료 이탈 시스템](#12-동료-이탈-시스템)
13. [Wanderer NPC 자유 행동](#13-wanderer-npc-자유-행동)
14. [반응 시스템 (말풍선 / 상황 로그)](#14-반응-시스템-말풍선--상황-로그)
15. [기력 시스템](#15-기력-시스템)
16. [HUD / UI](#16-hud--ui)
17. [맵 생성기](#17-맵-생성기)
18. [설정 가이드 (사용자 작업)](#18-설정-가이드-사용자-작업)
19. [디버깅 가이드](#19-디버깅-가이드)
20. [시나리오 예시](#20-시나리오-예시)
21. [기획서 대비 변경/확장 사항](#21-기획서-대비-변경확장-사항)

---

# 1. 개요

| 항목 | 내용 |
|---|---|
| 장르 | 생존 / 전략 / 파티 운영 (탑다운 2D) |
| 플랫폼 | Unity 6.3 (URP 2D) |
| 입력 | New Input System (코드 내 InputAction 직접 생성) |
| 렌더링 | Universal Render Pipeline (2D) |
| 핵심 가치 | NPC를 단순 전투 유닛이 아니라 **욕망과 두려움을 가진 존재**로 시뮬레이션 |

플레이어는 적과 싸우면서 NPC를 영입하고, 정산하고, 장비를 빌려주며, 신뢰를 쌓거나 깨면서 생존한다. 전투 자체보다 **사람을 읽고 의심하며 운영하는 과정**이 핵심이다.

---

# 2. 핵심 게임 루프

```
탐색 (방 이동)
 ↓
NPC / 적 대면
 ↓
[영입 / 무시 / 공격]   /   [전투]
 ↓                          ↓
관계 변화                   전리품 획득
 ↓                          ↓
정산 / 장비 대여 / 휴식
 ↓
[떠남 / 도주 / 배신 / 절도 / 협력]
 ↓
다시 탐색
```

---

# 3. 조작

| 키/버튼 | 동작 | 차단 조건 |
|---|---|---|
| W A S D | 8방향 이동 | 휴식 중, 게임오버 |
| 좌클릭 | 마우스 방향으로 투사체 발사 | UI 위, 휴식 중, 게임오버 |
| 우클릭 | 마우스 위치 NPC와 상호작용 | UI 위, 휴식 중, 게임오버 |
| R | 휴식 시작 | 게임오버, 휴식 중, 적 근처 |
| 휴식 버튼 (UI) | 휴식 시작 | R 키와 동일 |

### 우클릭 상호작용 동작
- 마우스 클릭 좌표에 NPC 콜라이더가 있으면 그 NPC 선택
- 콜라이더 정확히 안이 아니어도 0.5 단위 반경 내 가장 가까운 NPC 잡음
- NPC가 플레이어로부터 `interactionRange = 6` 안에 있을 때만 메뉴 오픈
- **차단되는 NPC**: 도주/적대 중인 Wanderer, 도주/적대 중인 Companion (배신/절도/Flee 동료)

### Hover 강조
- 마우스가 가리키는 NPC가 interactionRange 안이면 SpriteRenderer 색상이 노란빛으로 변함
- 다른 NPC 가리키거나 거리 밖으로 나가면 원래 색상 복귀

---

# 4. 캐릭터 종류와 진영

```
CharacterBase (abstract MonoBehaviour)
  ├── PlayerCharacter
  ├── NPCCharacter (abstract)
  │     ├── CompanionCharacter   (동료)
  │     └── WandererCharacter    (비동료 NPC)
  └── EnemyCharacter             (적)
```

| 종류 | 기본 Layer | 진영 (Team) | 비고 |
|---|---|---|---|
| Player | Player | Player | 좌클릭 발사, 우클릭 상호작용 |
| Companion | Companion | Companion | 영입 후 발생. 배신/절도 시 → Enemy 레이어 |
| Wanderer | WandererNPC | Neutral | 자유 행동 중. 적대화 시 → Enemy 레이어 |
| Enemy | Enemy | Enemy | 적. 사망 시 골드 드롭 |

### Layer 전환 규칙
- **CompanionCharacter가 배신/절도** → Layer = Enemy, Shooter Owner = Enemy
- **WandererCharacter가 Hostile** (피격 또는 선제공격) → Layer = Enemy, Shooter Owner = Enemy
- 한 번 Enemy로 변환된 NPC는 동료 메뉴/영입 메뉴에 더 이상 안 잡힘 (InteractableMask에서 제외)

---

# 5. 스탯 시스템

## 5-1. Player 스탯 (`PlayerStats`)

| 스탯 | 기본값 | 설명 |
|---|---:|---|
| BaseHP | 100 | 기본 체력. 장비 보너스가 더해져 최종 MaxHP |
| BaseATK | 10 | 기본 공격력. 장비 + 기력 배율 적용 |
| BaseThreat | 0 (사용자 설정) | 위압감. NPC 영입/배신/도주 점수에 영향 |
| BaseWealth | 0 | 재력 노출도. NPC Greed 반응에 영향 |
| MaxStamina | 100 | 최대 기력 |
| StartingGold | 0 | 시작 골드 |

### 최종 스탯 계산
```
FinalHP     = BaseHP + 장비 HP 합
FinalATK    = (BaseATK + 장비 ATK 합) × 기력 배율
FinalThreat = BaseThreat + 장비 Threat 합
FinalWealth = BaseWealth + 장비 Wealth 합
```

### Player에게는 Morality가 없음
시스템이 플레이어의 선악을 직접 규정하지 않는다. NPC가 각자의 성향에 따라 플레이어 행동을 해석.

## 5-2. NPC 스탯 (`NPCStats`)

모든 NPC(Companion, Wanderer)가 공유.

| 스탯 | 범위 | 설명 |
|---|:---:|---|
| BaseHP | 100 | 기본 체력 |
| BaseATK | 8 | 기본 공격력 |
| MaxStamina | 100 | 최대 기력 |
| Trust | 0~100 | **플레이어에 대한 신뢰**. 변동 가능 |
| Greed | 0~100 | **탐욕**. 정산 민감도, 절도 가능성 |
| Fear | 0~100 | **공포 / 생존 본능**. 도주/거리유지 |
| Morality | 0~100 | **도덕성**. 배신 / 절도 억제 |
| Gold | 0+ | **NPC 지갑**. 절도 / 사냥 시 누적 |

### 랜덤화 (`randomizeOnSpawn`)
체크 시 Awake에서 다음과 같이 자동 설정:
- **이름**: 박민호, 정찬서, 유윤상, 김우성, 이재준, 최도윤, 한지원, 오세훈, 장태현, 남궁민, 권시우, 윤서아, 조하늘, 강현우, 임도현 중 랜덤
- **Trust**: 40 ~ 65
- **Greed**: 20 ~ 95
- **Fear**: 20 ~ 90
- **Morality**: 20 ~ 90

## 5-3. PoC 추천 NPC 프리셋

| 이름 | Trust | Greed | Fear | Morality | 특징 |
|---|---:|---:|---:|---:|---|
| 박민호 | 50 | 30 | 85 | 60 | 겁 많은 생존자. 잘 도망감 |
| 정찬서 | 45 | 90 | 35 | 25 | 탐욕스러운 기회주의자. 배신/절도 |
| 유윤상 | 55 | 20 | 40 | 85 | 원칙적인 동료. 안정적 |

---

# 6. 전투 시스템

## 6-1. 투사체 (Projectile)

| 속성 | 설명 |
|---|---|
| Owner | Player / Companion / Enemy |
| Damage | Shooter에서 설정 |
| Speed | 직선 이동 |
| Lifetime | 5초 후 자동 소멸 |
| Collision | Trigger. HealthComponent 없는 대상(벽)에 닿으면 소멸 |

### Owner-Target 호환성
투사체는 owner에 따라 명중 가능한 대상이 정해진다.

| Owner | 명중 가능 대상 |
|---|---|
| Player | Companion, Enemy, WandererNPC (동료/비동료/적 모두) |
| Companion | Enemy (배신 후엔 Owner가 Enemy로 바뀜) |
| Enemy | Player, Companion |

> 플레이어 투사체는 동료/비동료에게 모두 명중 가능 → **오인사격** 발생.

## 6-2. 오인사격 / 동료 공격

플레이어 투사체가 동료에게 명중하면 즉시 Trust 감소 + 점수 재계산.

### Trust 감소 공식
```
Trust 감소 = -5 × (1 + (Fear - 50) / 100)
```

| Fear | 기본 Trust 감소 |
|---:|---:|
| 0 | -2.5 → -3 |
| 50 | -5 |
| 100 | -7.5 → -8 |

### 즉시 임계값 체크
오인사격 직후 Betrayal/Flee/Retirement 점수 모두 재계산. 임계값 통과 시 즉시 발동.

### Trust 구간별 반응 말풍선
| 현재 Trust | 반응 |
|---:|---|
| 70 이상 | "조심해줘." |
| 40~70 | "지금 뭐 하는 거야." |
| 40 미만 + Fear 60 이상 | "날 죽이려는 거야?!" (도주 가능성↑) |
| 40 미만 + Fear 60 미만 | "한 번만 더 그러면 나도 가만 안 있어." (배신 가능성↑) |

### 디버그 로그
```
[피격 판정] 정찬서  Trust=30  betray=178/140  flee=45/110  retire=62/100
```

## 6-3. 영구 적대 (Aggro Lock)

### EnemyBrain
- 처음에는 `detectionRadius = 8` 안에서만 인식
- 한 번이라도 인식하면 `_isAggro = true` → 이후 detection 무시, 거리 무관 추적
- 타겟이 죽기 전까지 계속 추적
- 사거리 안: 정지 + 발사 / 밖: 추격 (moveSpeed)

### Wanderer Hostile
- Layer가 Enemy로 바뀌어 있음
- 거리 무관 추적 (UpdateHostile)

### Companion 배신 (Hostile)
- Layer Enemy로 전환
- 플레이어 추적 + 공격 (CompanionBrain.UpdateHostile)

### 기습 시 즉시 Aggro
RestSystem이 적 스폰 직후 `EnemyBrain.EnterAggroMode(player.transform)` 호출 → 가만히 있지 않고 바로 플레이어 향해 진격.

## 6-4. 동료 자동 공격

`CompanionBrain.UpdateFollowing`:
- 플레이어 추종 (`followDistance = 2`)
- attackRadius (`7`) 안에 적 발견 시 정지 + 발사
- 적 없으면 플레이어 따라감
- **다른 동료와 분리 force** (separation): 1.5 이내면 멀어지는 방향으로 밀어냄 → 자연스럽게 안 겹침

## 6-5. 동료 분리 (Separation Force)

```csharp
separationDistance = 1.5
separationStrength = 3.5

// 다른 동료 모두에 대해:
diff = self.position - other.position
weight = (1.5 - dist) / 1.5   // 가까울수록 큼
result += diff.normalized × weight
// 최종 속도 = 이동방향 × moveSpeed + result × separationStrength
```

가까이 있을수록 강하게 밀려나서 Layer 충돌 없이도 안 겹침.

---

# 7. 영입 시스템

비동료 NPC를 우클릭 → `[영입 제안]` 선택 → 점수 판정.

## 7-1. 영입 점수 공식
```
영입 점수 = 50
         + Player.FinalThreat × 0.2
         + Player.FinalWealth × 0.1
         - NPC.Fear × 0.3
         + NPC.Greed × 0.1
         + (NPC.Morality - 50) × 0.1
```

영입 점수 ≥ 50 → 성공
영입 점수 < 50 → 거절

### 예시 (Threat 0, Wealth 0)
| NPC | 점수 | 결과 |
|---|---:|---|
| 박민호 (Fear 85, Greed 30) | 28.5 | 거절 |
| 정찬서 (Fear 35, Greed 90) | 46 | 거절 |
| 유윤상 (Fear 40, Greed 20) | 43.5 | 거절 |

### Threat 35로 올리면
| NPC | 점수 | 결과 |
|---|---:|---|
| 박민호 | 35.5 | 거절 (어려움) |
| 정찬서 | 53 | **영입** |
| 유윤상 | 50.5 | **영입** |

## 7-2. 거절 반응
| NPC 성향 | 거절 대사 |
|---|---|
| Fear ≥ 70 | "난 너랑 못 가." / "위험해 보여." |
| Greed ≥ 70 | "조건이 별로인데?" / "내 몫은 확실한 거지?" |
| 그 외 | "당신을 아직 믿을 수 없군." |

## 7-3. 영입 성공 시
```
NPC가 파티 합류
미정산금 = 0 (시작)
NPC 인벤토리 유지
초기 Trust = max(NPC.Trust, 40 + (영입점수 - 50) × 0.2)
NPC 자신의 Gold도 유지 (이전에 누적된 거)
Layer Companion으로 변경
Shooter Owner = Companion
```

영입 시 Wanderer 컴포넌트 제거, CompanionCharacter / Brain / Relationship / Reaction 자동 추가 (`RequireComponent`).

---

# 8. 정산 시스템

## 8-1. 미정산금 누적
적이 죽었을 때 `LootSystem.HandleEnemyDeath(gold, attacker)` 호출.

| Attacker | 동작 |
|---|---|
| Player Layer | 골드 → 플레이어 + 동료 모두 미정산금 누적 |
| Companion Layer | 동일 |
| Wanderer Layer | 그 NPC 자기 지갑에만 누적 (사용자 무관) |
| 그 외 (Enemy 등) | 무시 |

### 분배 공식
```
heads = 동료 수 + 1 (플레이어)
플레이어 골드 += goldDrop
각 동료 미정산금 += goldDrop / heads
```

> **주의**: 플레이어는 전체 골드를 받지만, 동료는 1/heads만큼이 미정산으로 누적된다. 이는 "플레이어가 자신의 골드에서 동료 몫을 결정해 지급"하는 구조다.

## 8-2. 정산 UI

동료 우클릭 → 동료 관리창 → `[정산하기]`

```
┌─────────────────────────────────┐
│  미정산금 400G  (보유 1,200G)    │
│                                 │
│  지급 320 G                      │
│  ─────●──────────                │
│                                 │
│  예상 : 정찬서이(가) 만족할 것입니다. │
│  Trust +2.5                     │
│                                 │
│  [정산]   [취소]                  │
└─────────────────────────────────┘
```

### 슬라이더
- 범위: 0 ~ min(보유 골드, 미정산금 × 1.5)
- 정수 단위 (Whole Numbers)
- 기본값: 정확히 정산 금액 (보유 골드가 부족하면 보유 골드까지)

### 예상 반응 미리보기
슬라이더 움직일 때마다 실시간 갱신. 실제 정산 결과와 동일한 함수로 계산.

| Trust 변화 | 표현 |
|---|---|
| ≤ -12 | "○○이(가) 크게 분노할 것입니다." |
| ≤ -5 | "○○이(가) 실망할 것입니다." |
| < 0 | "○○이(가) 약간 실망할 수 있습니다." |
| ≤ +3 | "○○이(가) 만족할 것입니다." |
| ≤ +8 | "○○이(가) 기뻐할 것입니다." |
| > +8 | "○○이(가) 매우 기뻐할 것입니다." |

## 8-3. Trust 변화 공식

```
ratio = goldPaid / unpaidAmount

if ratio == 0:                  baseDelta = -15        (미지급)
else if ratio < 0.8:            baseDelta = -5 - (0.8 - ratio) × 25   (부족)
else if ratio <= 1.0:           baseDelta = +3         (허용)
else:                           baseDelta = +3 + min((ratio - 1) × 20, 10)  (후한)

Greed 보정 = clamp(1 + (Greed - 50) / 100, 0.7, 1.5)
최종 Trust 변화 = baseDelta × Greed 보정
```

### Greed 보정 효과
같은 정산 비율이라도 NPC Greed에 따라 결과가 다름:

| Greed | 보정 배율 | 100% 정산 (base +3) | 50% 정산 (base ≈ -12.5) |
|---:|---:|---:|---:|
| 0 | 0.7 | +2.1 | -8.75 |
| 50 | 1.0 | +3.0 | -12.5 |
| 100 | 1.5 | +4.5 | -18.75 |

## 8-4. 정산 1회 클리어 (변경된 정책)

> 기획서 초안: 미정산금 = max(0, 미정산금 - 지급골드) → 잔금 이월
>
> **현재 구현**: 1회 정산하면 미정산금 = 0으로 클리어. 부족 지급/후한 지급 무관 잔금 이월 없음.

이유: 사용자가 30G 미정산에 28G 지급하면 그것으로 끝나길 원함. NPC의 만족/불만은 그 한 번의 정산 비율로 결정.

이후 다음 전투부터 새 미정산금이 다시 누적된다.

---

# 9. 장비 시스템

## 9-1. 장비 종류 (`EquipmentData` ScriptableObject)
| 종류 | 주요 능력치 |
|---|---|
| 무기 (Weapon) | ATK 중심 |
| 갑옷 (Armor) | HP 또는 Threat 중심 |
| 장신구 (Accessory) | Wealth 또는 보조 |

### 장비 스탯 필드
- `atk`, `hp`, `threat`, `wealth` (실제 능력치)
- `value` (정산 미정산금에는 직접 안 들어가지만 잠재적 사용)
- `icon` (UI 표시)

## 9-2. 인벤토리 구조
플레이어와 모든 NPC가 **각자 독립적인 EquipmentSlots**:
```
무기 슬롯 1개
방어구 슬롯 1개
장신구 슬롯 1개
+ 보유 리스트 (장착 외 보관)
```

## 9-3. 대여 / 회수 흐름

### 대여
1. 동료 우클릭 → 동료 관리창 → `[장비 대여]`
2. 플레이어 인벤토리 팝업 (보유 장비 모두 표시)
3. 아이템 선택 → 플레이어 인벤토리에서 제거 → NPC 인벤토리로 이동
4. NPC가 자동 착용 점수 비교 (다음 섹션)

> **장비 대여 자체는 Trust에 영향 없음.** 다만 NPC가 말풍선으로 반응할 수 있음.

### 회수
1. 동료 관리창 → `[장비 회수]`
2. NPC 인벤토리 팝업
3. 아이템 선택 → NPC 인벤토리에서 제거 → 플레이어 인벤토리로 이동
4. **불만 조건** 만족 시 임시 불만 점수 +5

#### 불만 조건
- 회수 장비가 현재 착용 중이었음
- NPC.Trust ≤ 40
- NPC.Stamina ≤ 30

조건 만족 시 말풍선:
- "지금 가져간다고?"
- "그거 없으면 좀 불안한데."

## 9-4. NPC 자동 착용 점수

NPC 인벤토리에 장비가 추가될 때 점수 계산. 새 장비 점수 > 현재 착용 장비 점수 면 교체.

### 성향별 가중치
```csharp
score = atk × wAtk + hp × wHp + threat × wThreat + wealth × wWealth
```

| 성향 조건 | wAtk | wHp | wThreat | wWealth |
|---|---:|---:|---:|---:|
| Greed ≥ 70 (탐욕형) | 1.0 | 0.8 | 0.5 | **1.4** |
| Fear ≥ 70 (공포형) | 0.8 | **1.5** | 0.7 | 0.2 |
| Morality ≤ 30 (기회주의형) | **1.3** | 0.7 | 1.1 | 0.8 |
| 그 외 (기본형) | 1.0 | 1.0 | 0.7 | 0.4 |

> Greed/Fear가 동시에 70 이상이면 Greed가 우선 (코드 if-else 순서).

## 9-5. 장비와 이탈

| 이탈 종류 | 장비 처리 |
|---|---|
| 정산 후 내보내기 (Trust ≥ 50) | 모든 장비 플레이어에게 반환 |
| 정산 후 내보내기 (Trust < 50) | NPC가 일부 가지고 떠남 (PoC: 그대로 사라짐) |
| 자진 탈퇴 (Trust ≥ 50) | 장비 반환 후 떠남 |
| 자진 탈퇴 (Trust < 50) | 일부/전체 들고 떠남 |
| 도주 (Fleeing) | 가지고 도망. 죽이면 회수 |
| 배신 (Hostile) | 가지고 적대 진영으로. 죽이면 회수 |
| 절도 (Theft) | 골드 + 장비 들고 도주/적대. 죽이면 회수 |

---

# 10. 인벤토리 / 지갑 / 회수

## 10-1. NPC 지갑 (`NPCStats.gold`)

| 누적 경로 | 설명 |
|---|---|
| Wanderer가 적 사냥 | 자기 지갑에 dropGold 누적 (사용자 무관) |
| Companion 절도 | 훔친 골드를 자기 지갑에 보관 |

## 10-2. 사망 시 회수 (`NPCDropHandler`)

### 죽인 게 Player/Companion일 때
- NPC.gold → 플레이어 골드로 인계
- NPC.인벤토리 모든 장비 → 플레이어 인벤토리로 인계
- 로그: "○○에게서 200G 회수." / "○○에게서 장비 3개 회수."

### 그 외 (Enemy가 죽인 경우)
- 사라짐
- 로그: "○○이(가) 가지고 있던 것들이 사라졌다."

## 10-3. 절도 보유물 회수 시나리오
1. 휴식 중 정찬서 절도 → 200G + 장비 2개 들고 적대화
2. 플레이어가 정찬서 추격해서 처치
3. 자동으로 200G + 장비 2개 회수
4. 로그에 명시

---

# 11. 휴식 시스템

## 11-1. 휴식 조건
- `RestSystem.TryRest()` 호출 (R 키 또는 UI 휴식 버튼)
- **적이 enemyCheckRadius (`10`) 안에 없을 때만** 시작 가능
- 이미 휴식 중이면 무시

## 11-2. 휴식 진행 (정상 흐름)

```
T=0      휴식 시작 → "휴식을 시작했다..."
         ├─ Ambush 확률 판정 (적 스폰 가능)
         ├─ 플레이어 정지 (PlayerMovement)
         └─ Following/Waiting 동료 정지 (CompanionBrain)

T=0.5    동료 자유 시간 판정 (CheckRestOpportunity)
         ├─ 절도 ≥ 130 → 절도 발동 (조용히)
         ├─ 배신 ≥ 120 → 배신 발동 (전체 깨움)
         ├─ 탈퇴 ≥ 85 → 자진 탈퇴
         └─ 도주 ≥ 100 → 도주

T=5      휴식 종료
         ├─ 플레이어 HP +30%, Stamina +70
         ├─ 각 동료 HP +30%, Stamina +70
         └─ 각 동료 CheckAfterRest (정산 요구 등)
```

## 11-3. 휴식 중 정지 규칙

| 대상 | 휴식 중 동작 |
|---|---|
| Player | 정지 (PlayerMovement) |
| Companion (Following / Waiting) | 정지 (자고 있음) |
| Companion (Hostile / Fleeing) | **정상 동작** (이미 적대 진영) |
| Wanderer (모든 모드) | 정상 동작 (남이라 안 잠) |
| Enemy | 정상 동작 (위협) |

> 자고 있는 동료들은 적/절도범을 인식하지 못한다. **사용자 의도**: 신뢰 높은 동료가 절도범을 알아채고 공격하는 충돌 방지.

## 11-4. 깨우기 트리거 (`ForceWake`)

`RestSystem.ForceWake(reason)` 호출 시:
- 코루틴 즉시 중단
- 회복 없이 종료
- `OnRestFinished(false)` 발생 (UI 갱신)
- 로그: `잠에서 깨어났다! ({reason})`

### 트리거되는 경우

| 트리거 | 동작 |
|---|---|
| 플레이어 피격 | **현재 HP × 0.5 추가 데미지** + 깨우기 |
| 동료 피격 | 즉시 깨우기 |
| 동료 배신 (Hostile 전환) | 다른 동료들에 "○○이(가) 배신했어!" 말풍선 + 깨우기 |
| 동료 절도 (조용히 도망) | **안 깨움** (휴식 끝나야 발견) |

### 휴식 중 치명타 데미지
```
원본 데미지 + (플레이어 현재 HP × restCriticalRatio)
```
restCriticalRatio = 0.5 (인스펙터 조정)

> 재진입 방지를 위해 `_handlingRestDamage` 플래그로 가드. OnDamaged 안에서 TakeDamage를 다시 호출해도 무한 재귀 안 일어남.

## 11-5. 적 습격 (Ambush)

휴식 시작 직후 확률 판정.

| 동료 수 | 확률 |
|---:|---:|
| 0명 (혼자) | 50% |
| 1명 | 15% (= 20% - 5%) |
| 2명 | 10% |
| 3명+ | 5% |

```
chance = 동료 0명 ? ambushChanceAlone (0.5)
                  : max(0, ambushChanceWithCompanions (0.2) - 동료수 × 0.05)
```

### 스폰 동작
- 스폰 적 수: ambushSpawnCountRange (기본 1~3)
- 위치: 플레이어로부터 `ambushSpawnDistance = 10` 거리, 임의 방향
- **스폰 직후 EnterAggroMode 호출** → detection 무시하고 즉시 플레이어 추적

### Ambush 활성화 조건
- `ambushEnemyPrefab` 인스펙터에 연결되어 있을 때만 작동
- 연결 안 하면 Ambush 영구 비활성

## 11-6. 휴식 중 동료 결심 (CheckRestOpportunity)

평소보다 임계값을 낮춰서 휴식 중 더 자주 발동.

| 이벤트 | 평소 임계값 | 휴식 중 임계값 |
|---|---:|---:|
| 배신 | 140 | **120** |
| 탈퇴 | 100 | **85** |
| 도주 | 110 | **100** |
| 절도 | — | **130 (신규)** |

### 절도 점수 공식
```
절도 = (100 - Trust)
     + Greed
     + (100 - Morality)
     + Player.FinalWealth × 0.5
     + 미정산금 / 10
     - Fear × 0.4
```

Fear 높으면 (겁나서) 못 훔침. Wealth/미정산금 많으면 훔치고 싶음.

### 절도 발동 효과 (`HandleTheft`)
- 골드 절도: `min(플레이어 골드, NPC.Greed × 2)` → NPC.gold에 누적
- NPC 인벤토리 보유 장비도 그대로 들고 도주
- Layer Enemy로 전환
- Fear ≥ 60: Fleeing 모드 (도망), Fear < 60: Hostile 모드 (공격)
- PartyRoster 제거
- 말풍선 "잘 챙겨갈게."
- 로그: "○○이(가) 200G와 장비 2개를 들고 도망쳤다!"

> 절도는 **조용히** 발생. 휴식 깨우지 않음. 다른 동료들도 모름.

---

# 12. 동료 이탈 시스템

이탈 종류는 5가지: 플레이어가 내보냄 / 자진탈퇴 / 도주 / 배신 / 절도.

## 12-1. 플레이어가 내보냄

동료 관리창 → `[정산 후 내보내기]` 또는 `[그냥 내보내기]`

### 정산 후 내보내기
- 정산 UI 오픈 → 정산 완료 후 자동으로 내보내기 처리
- Trust ≥ 50: 모든 장비 플레이어에게 반환
- Trust < 50: 일부 들고 떠남 (PoC: 사라짐)

### 그냥 내보내기
- 미정산금 남아있으면 Trust 변화 = `-5 - (미정산금 / 100)`
- 그 후 동일하게 내보냄

## 12-2. 자진 탈퇴 (Retirement)

### 탈퇴 점수 공식
```
탈퇴 점수 = (100 - Trust)
          + 미정산금 / 20
          + (Stamina ≤ 30 일 때) +15
          + 최근 불만 행동 점수 (장비 회수 등에서 +5씩)
```

### 임계값
| 점수 | 동작 |
|---:|---|
| ≥ 80 | 탈퇴 경고 (말풍선) |
| ≥ 100 | **자진 탈퇴 발동** (휴식 중엔 ≥ 85) |

### 발동 시 (`HandleRetirement`)
- Trust ≥ 50: "이만 가볼게." + 모든 장비 반환 후 떠남
- Trust < 50: "장비 일부를 들고 떠남" (PoC: 그대로 사라짐)
- PartyRoster 제거 → 0.5초 후 Destroy

## 12-3. 전투 중 도주 (Flee)

### 도주 점수 공식
```
도주 점수 = Fear
          + (적이 가까이 + HP ≤ 30%) +20
          + (Stamina ≤ 20) +15
          - Trust × 0.3
```

### 임계값
| 점수 | 동작 |
|---:|---|
| ≥ 90 | 도주 준비 (PoC 미구현) |
| ≥ 110 | **전투 중 도주 발동** (휴식 중엔 ≥ 100) |

### 발동 시 (`HandleFlee`)
- Brain.SetState(Fleeing)
- PartyRoster 제거
- "못 하겠어. 미안." 말풍선
- 도주 모드: 12 거리까지 멀어지면 멈춤 (사라지지 않음 → 다시 마주칠 수 있음)
- 가까이 가면 다시 도망

## 12-4. 배신 (Betrayal)

### 배신 점수 공식
```
배신 점수 = (100 - Trust)
          + Greed
          + (100 - Morality)
          + Player.FinalWealth
          + (Player.HP ≤ 30%) +20
          + (Player.Stamina ≤ 20) +15
          - Player.FinalThreat
          - Fear × 0.3
```

### 임계값
| 점수 | 동작 |
|---:|---|
| ≥ 120 | 배신 징조 (말풍선) |
| ≥ 140 | **배신 발동** (휴식 중엔 ≥ 120) |

### 발동 시 (`HandleBetrayal`)
- Brain.SetState(Hostile)
- Layer = Enemy, Shooter Owner = Enemy
- "미안하지만... 이게 낫겠어." 말풍선
- 휴식 중이면:
  - 다른 동료들에 "○○이(가) 배신했어!" 말풍선
  - RestSystem.ForceWake("○○의 배신!")
- PartyRoster 제거

## 12-5. 절도 (Theft) - 휴식 중에만

11-6 휴식 중 동료 결심 참조.

## 12-6. 패시브 점수 체크

### 트리거 1: 플레이어 피격
플레이어 OnDamaged → 모든 동료 `CheckImmediateThresholds()` 즉시 호출.
HP 약화 보정이 배신 점수에 반영되어 자연스럽게 평소에도 배신 가능.

### 트리거 2: 6초 주기
CompanionCharacter.Update에서 `_passiveCheckTimer`가 6초마다 점수 재계산.
점수 임계값 통과하면 즉시 발동.

> 평상시에도 운명이 결정될 수 있음.

## 12-7. CheckImmediateThresholds (오인사격/주기적 체크용)
```
betray ≥ 140 → 배신
flee   ≥ 110 → 도주
retire ≥ 100 → 탈퇴
betray ≥ 120 → 배신 경고만
retire ≥  80 → 탈퇴 경고만
```

## 12-8. CheckAfterRest (휴식 종료 시)

위와 거의 동일하지만 추가로 정산 요구 판정.

### 정산 요구 점수
```
정산 요구 점수 = Greed
              + 미정산금 / 10
              + Player.FinalWealth
              - Trust
```

| 점수 | 동작 |
|---:|---|
| ≥ 80 | 로그 "○○이(가) 자기 몫을 계산하는 듯하다." |
| ≥ 100 | 말풍선 "내 몫은 언제 줄 거야?" + 로그 |

---

# 13. Wanderer NPC 자유 행동

## 13-1. 모드 (`WandererMode`)
| 모드 | 진영 | Layer | 특징 |
|---|---|---|---|
| Idle | Neutral | WandererNPC | 평화 시 기본. wander, 적 사냥, 거리유지, 선제공격 |
| Fleeing | Neutral | WandererNPC | 도주. 12 멀어지면 idle 복귀 |
| Hostile | Enemy 진영 | Enemy | 영구 추적, 적대 |

## 13-2. Idle 동작 우선순위

매 FixedUpdate:
1. **근처 적 사냥** (enemyDetectRadius=7 안에 적 있을 때)
   - 사거리 안: 발사 / 밖: 추격 (idleAttackChaseSpeed=2.5)
2. **선제공격 판정** (2초마다 1회)
3. **Fear 거리유지** (Fear ≥ 70인 경우)
4. **Wander** + 가끔 대사

## 13-3. 선제공격 (Aggression)

```
공격성 점수 = (100 - Morality)
            + Greed
            + 약화 보너스
            - Fear
            - Player.FinalThreat × 0.3
```

### 약화 보너스
| 조건 | 보너스 |
|---|---:|
| Player HP < 40% | +30 |
| Player HP < 70% | +10 |
| Player Stamina < 30 | +10 |

### 발동 조건
- 플레이어가 aggressionDetectRange (`6`) 안에 있음
- 점수 ≥ aggressionThreshold (`110`)

### 발동 시
- WandererMode = Hostile
- Layer = Enemy
- Shooter Owner = Enemy
- "쉬운 먹잇감이군." / "기회다." / "약해 보이는데?" / "한 방이면 끝나겠어."
- 로그: "○○이(가) 선제공격을 시작했다! (공격성 ○○)"

## 13-4. Fear 거리유지

Fear ≥ 70인 NPC가 플레이어와 keepAwayDistance (`4`) 안에 들어오면:
- 플레이어 반대 방향으로 keepAwaySpeed (`2.2`) 이동
- 5초 쿨타임으로 말풍선:
  - 거리 < 2: "더 가까이 오지 마!"
  - 거리 2~4: "거리 좀 둬."

## 13-5. Wander
- wanderRadius (`3`) 안에 임의 목표 설정
- wanderIntervalRange (`3~7초`)마다 새 목표
- wanderSpeed (`1.5`)로 천천히 이동

## 13-6. Idle Chatter (대사)
8~18초마다 발화. 플레이어가 8 거리 안이면 성향 우선:
| 조건 | 대사 풀 |
|---|---|
| Greed ≥ 70 + Player.Wealth ≥ 30 | "장비가 꽤 좋아 보이는데." 등 |
| Fear ≥ 70 + Player.Threat ≥ 30 | "저 사람 위험해 보여." 등 |
| 그 외 | "오늘도 살아남자." / "여기 위험한 곳이군." 등 |

## 13-7. 피격 시
플레이어가 공격 → OnDamaged → Idle 모드면 모드 전환:
- Fear ≥ 60: **Fleeing** ("살려줘!" / "왜 이러는 거야!")
- Fear < 60: **Hostile** ("감히 나한테!")

> 적 투사체에 맞은 건 무시 (모드 전환 안 함, 자기방어로 idle 적 공격 계속).

## 13-8. Fleeing 모드
- 도주 방향으로 fleeSpeed (`4`) 이동
- 플레이어로부터 fleeStopDistance (`12`) 이상 멀어지면 → **Idle 복귀** ("휴, 멀어졌네.")
- fleeDespawnDistance (`30`) 이상은 사라짐

## 13-9. Hostile 모드
- 거리 무관 추격 (hostileMoveSpeed `3`)
- hostileAttackRadius (`7`) 안: 정지 + 발사

---

# 14. 반응 시스템 (말풍선 / 상황 로그)

## 14-1. 말풍선 (`SpeechBubble` + `BubbleManager`)

### 풀링
- BubbleManager에 8개 풀 사전 생성
- 부족하면 동적 Instantiate

### NPC당 1개 보장 (3중 보호)
1. `_activeBubbles` Dictionary로 target → bubble 매핑
2. 자식 전체 검사 (Dict가 깨졌을 때 보정)
3. destroyed reference 자동 스킵

### 동일 NPC 새 풍선 → 기존 풍선 재사용 + 텍스트/타이머 즉시 교체

### 표시 시간
- 3.5초 표시 + 0.5초 페이드 → 풀에 반환

### 말풍선 위치
- target.position + (0, 1.5, 0) offset, LateUpdate에서 따라다님

## 14-2. 상황 로그 (`LogManager` + `SituationLogView`)

### 정적 진입점
```csharp
LogManager.AddLog("정찬서가 200G를 들고 도망쳤다!");
```

### Queue 50개까지 보관, 새 로그 발생 시 OnLogAdded 이벤트로 UI 갱신.
SituationLogView는 최근 5줄만 표시.

## 14-3. NPC 반응 트리거 (`CompanionReaction`)

### 쿨타임
- 로그 쿨타임: 15초
- 말풍선 쿨타임: 7초

### 트리거 종류
| 트리거 | 조건 | 출력 |
|---|---|---|
| Greed + Wealth | Greed ≥ 70 | 로그 + 말풍선 |
| Fear + Threat | Fear ≥ 70 | 로그 + 말풍선 |
| Trust 하락 | 40 이하/25 이하 첫 진입 | 로그 + 말풍선 |
| Stamina 부족 | 30 이하/10 이하 첫 진입 | 로그 + 말풍선 |
| 장비 대여 | 장비 받았을 때 | 로그 + 말풍선 |
| 장비 회수 | 불만 조건 충족 시 | 로그 + 말풍선 |
| 배신 경고 | 배신 점수 ≥ 120 | 로그 |
| 탈퇴 경고 | 탈퇴 점수 ≥ 80 | 로그 + 말풍선 |

### 확률 출력 예시
```
Greed 90 NPC, Wealth 50 플레이어
출력 확률 = 20% + (90 - 70) × 1% + (50 - 50) × 0.5% = 40%
```

---

# 15. 기력 시스템

## 15-1. 감소 조건
| 행동 | 감소량 |
|---|---:|
| 시간 경과 | -1 / 10초 |
| 원거리 공격 | -1 |
| 전투 참여 (PoC 미구현) | -5 |
| 피격 (PoC 미구현) | -3 |

## 15-2. 상태별 ATK 배율

| Stamina | 상태 | ATK 배율 |
|---:|---|---:|
| 70 ~ 100 | 정상 | 1.0 |
| 40 ~ 69 | 피로 | 1.0 (PoC: 공격 주기 증가 미구현) |
| 20 ~ 39 | 지침 | 0.8 |
| 1 ~ 19 | 탈진 직전 | 0.6 |
| 0 | 탈진 | 0 |

---

# 16. HUD / UI

## 16-1. 플레이어 HUD (좌상단)

```
HP        ████████░░  80/100
Stamina   ██████░░░░  60/100
Gold      1,250 G
ATK 25  |  위압 30  |  재력 20
```

`PlayerStats` 이벤트 구독으로 자동 갱신.

## 16-2. 동료 카드 (우측)

```
정찬서
HP      ████████░░
Stamina ██████░░░░
Trust   ████░░░░░░  (색상: 초록/노랑/빨강)
미정산 400G
[무기][방어구][장신구]
⚠ 탈퇴 경고  /  🗡 배신 징조  /  💤 대기 중
```

- Trust 색상: ≥70 초록, 40~70 노랑, < 40 빨강
- 경고 아이콘:
  - 탈퇴 경고: 탈퇴 점수 ≥ 80
  - 배신 징조: 배신 점수 ≥ 120
  - 대기: Brain.State == Waiting

`PartyRoster.OnMemberAdded/Removed` 이벤트로 카드 자동 추가/제거.

## 16-3. 상호작용 강조
마우스 hover 시 NPC SpriteRenderer 색상이 highlightColor로 변환. 거리 밖이거나 다른 NPC로 이동 시 원래 색상 복귀.

## 16-4. 메뉴

### Wanderer 메뉴 (비동료)
```
[영입 제안] [상태 확인] [무시]
```

### Companion 관리창 (동료)
```
정찬서
Trust 40  |  미정산 400G

[정산하기]    [장비 대여]
[장비 회수]   [상태 확인]
[대기 지시]   [정산후내보내기]
[그냥 내보내기] [닫기]
```

### 정산 UI
8-2 참조. 슬라이더 + 예상 반응 미리보기.

### 인벤토리 팝업 (대여/회수 공용)
- 모드별 타이틀: "○○에게 대여할 장비 선택" / "○○에게서 회수할 장비 선택"
- 슬롯 클릭 → 즉시 처리 + 닫힘

## 16-5. 게임오버 화면
- 플레이어 HP 0 → GameOverScreen 활성화
- Time.timeScale = 0
- 재시작 버튼 → SceneManager.LoadScene 현재 씬 재로드

---

# 17. 맵 생성기 (`MapGenerator`)

## 17-1. 격자 배치
- gridX × gridY 칸의 그리드 생성
- 각 칸 위치: `(x × roomSize, y × roomSize, 0)`
- 각 칸에 roomPrefabs 중 하나 랜덤 인스턴스화

## 17-2. NPC/Enemy 스폰
각 방에서 (Safe Room 제외):
- Wanderer 0~2명 (인스펙터 조정)
- Enemy 2~6명 (인스펙터 조정)
- 방 중심에서 spawnAreaRatio (`0.35`) 안 임의 위치

## 17-3. Safe Room
`safeRoomCoord` (기본 (0, 0))는 적/NPC 안 스폰. 플레이어 시작 위치.
`movePlayerToSafeRoom = true`면 플레이어를 자동으로 safe 방 중심으로 이동.

## 17-4. 시드
seed = 0이면 매번 다른 맵, 다른 값이면 고정 시드로 같은 맵 재현.

## 17-5. 에디터 우클릭 메뉴
`MapGenerator` 컴포넌트 우클릭 → "Generate" 클릭 → 즉시 재생성.

---

# 18. 설정 가이드 (사용자 작업)

## 18-1. Unity Layer 추가
`Edit → Project Settings → Tags and Layers`

| 인덱스 | 이름 |
|---:|---|
| 6 | Player |
| 7 | Companion |
| 8 | Enemy |
| 9 | WandererNPC |

> 코드가 문자열로 매칭하므로 정확히 일치해야 함.

## 18-2. Physics 2D Layer Collision Matrix
`Edit → Project Settings → Physics 2D`

같은 레이어끼리 충돌 끄기:
- Player ↔ Player
- Companion ↔ Companion
- Enemy ↔ Enemy
- WandererNPC ↔ WandererNPC

## 18-3. Active Input Handling
`Project Settings → Player → Other Settings`
- `Both` 또는 `Input System Package (New)`

## 18-4. Manager GameObject (씬 루트)

```
Managers
 ├── GameManager  (gameOverScreenObj 연결)
 ├── PartyRoster
 ├── RestSystem   (ambushEnemyPrefab 연결 권장)
 ├── LogManager
 └── BubbleManager (bubblePrefab 연결)
```

## 18-5. 프리팹 제작

### Projectile
- Rigidbody2D (Gravity 0, Body Type Dynamic)
- CircleCollider2D **(Is Trigger ON, Radius 0.1~0.2)**
- Projectile.cs
- SpriteRenderer

### SpeechBubble (비활성 상태 저장)
- SpeechBubble.cs
- 자식 TextMeshPro - Text (3D, World Space)

### Player
- Layer = Player
- Rigidbody2D (Gravity 0, Freeze Rotation Z)
- CircleCollider2D **(Is Trigger OFF)** ← 벽 통과 방지
- HealthComponent (Max HP 100)
- PlayerStats (BaseHP, BaseATK, BaseThreat 등 인스펙터로 설정)
- PlayerInputHandler
- PlayerMovement (Move Speed 5)
- EquipmentSlots
- PlayerInventory
- Shooter (Owner=Player, Projectile Prefab 연결)
- StaminaSystem
- InteractionDetector (메뉴 View들 연결)
- PlayerCharacter
- SpriteRenderer

> Main Camera를 Player의 자식으로. (0, 0, -10) 위치.

### Wanderer
- Layer = WandererNPC
- Rigidbody2D, CircleCollider2D (Is Trigger OFF)
- HealthComponent, NPCStats (`Randomize On Spawn` 체크 권장)
- EquipmentSlots, NPCInventory
- StaminaSystem
- Shooter (Owner=Companion ← idle 적 공격용. EnterHostileMode 시 자동 Enemy로 변경)
- WandererCharacter
- SpriteRenderer

### Enemy
- Layer = Enemy
- Rigidbody2D, CircleCollider2D
- HealthComponent
- Shooter (Owner=Enemy)
- EnemyBrain (detection/attack radius 조정)
- EnemyCharacter (gold drop 범위 설정)
- SpriteRenderer

### 벽 (Walls)
- BoxCollider2D **(Is Trigger OFF)**
- Rigidbody2D **(Body Type = Static)** ← 투사체 충돌 위해 필수
- SpriteRenderer

### 방 프리팹 (MapGenerator용)
- 빈 GameObject 안에 4방향 벽들
- 벽 사이 출입구는 비워둠
- 동일 크기로 통일

## 18-6. UI Canvas 구성
17-1~5 참조. 각 View 컴포넌트의 SerializeField에 정확히 연결.

## 18-7. EquipmentData ScriptableObject
`03. SOs/`에서 우클릭 → Create → PoC → Equipment

| 이름 | Type | ATK | HP | Threat | Wealth | Value |
|---|---|---:|---:|---:|---:|---:|
| 단검 | Weapon | 5 | 0 | 5 | 0 | 100 |
| 고급검 | Weapon | 12 | 0 | 10 | 5 | 300 |
| 가죽갑옷 | Armor | 0 | 30 | 0 | 0 | 150 |
| 금장갑옷 | Armor | 0 | 50 | 5 | 20 | 400 |
| 반지 | Accessory | 0 | 0 | 0 | 30 | 200 |

## 18-8. RestSystem 인스펙터
- Ambush Enemy Prefab ← Enemy 프리팹 (필수)
- Rest Critical Ratio = 0.5 (휴식 중 피격 추가 데미지 비율)
- Ambush Chance Alone = 0.5
- Ambush Chance With Companions = 0.2
- Ambush Spawn Distance = 10
- Ambush Spawn Count Range = (1, 3)
- Enemy Check Radius = 10 (휴식 시작 가능 거리)

---

# 19. 디버깅 가이드

## 19-1. Console 로그 의미

| 로그 | 의미 |
|---|---|
| `[Input] Fire pressed` | 좌클릭 입력 들어옴 (디버그용 임시) |
| `[피격 판정] 정찬서  Trust=30  betray=178/140 ...` | 동료 피격 후 점수 |
| `[휴식 중 기회] 정찬서  theft=145/130 ...` | 휴식 시작 직후 절도/배신/탈퇴/도주 점수 |
| `[휴식 판정] 정찬서  betray=120/140 ...` | 휴식 종료 시 점수 |
| `[CompanionBrain] 정찬서 : Following → Waiting` | 동료 상태 전환 |
| `[ToggleWait] 정찬서 ...` | 대기 토글 호출 확인 |
| `[RestSystem] 휴식 중 기회 판정 - 2명` | 동료 N명 대상 판정 시작 |

## 19-2. 점수 임계값 빠른 참조

| 이벤트 | 평소 | 휴식 중 |
|---|---:|---:|
| 배신 (Betrayal) | 140 | 120 |
| 자진 탈퇴 (Retirement) | 100 | 85 |
| 전투 도주 (Flee) | 110 | 100 |
| 절도 (Theft) | — | 130 |
| 배신 경고 (warning) | 120 | — |
| 탈퇴 경고 (warning) | 80 | — |
| 정산 요구 (강) | 100 | — |
| 정산 요구 (약) | 80 | — |
| Wanderer 선제공격 | 110 | — |
| 영입 (Recruitment) | 50 | — |

## 19-3. 운에 의존하는 부분

### 영입
- 박민호(Fear 85) 영입은 거의 불가능 (Threat 100 필요)
- Threat 35 이상이면 정찬서/유윤상 영입 가능
- 랜덤 NPC면 Greed/Fear 분포에 따라 다름

### 배신
- Greed 90, Morality 25 NPC면 좌클릭 5번 이내 배신 도달
- Greed 30, Morality 90 NPC는 좌클릭 10번도 안 됨
- 사용자 Threat가 높으면 배신 점수 깎임

### 절도
- Greed 90 + Fear 30 NPC가 미정산금 100G 이상 → 휴식 시작 시 절도 거의 확정
- Fear 80 NPC는 Greed 90이라도 절도 거의 안 함

## 19-4. 자주 발생하는 문제

| 증상 | 원인 / 해결 |
|---|---|
| 좌클릭 안 됨 | Active Input Handling 설정. Camera 태그. Shooter 프리팹 연결 |
| 영입 거절만 됨 | 영입 점수 < 50. Player BaseThreat 올리기 |
| 동료가 자도 변화 없음 | 점수 임계값 미달. Console에서 `[휴식 중 기회]` 점수 확인 |
| 휴식 중 적 안 옴 | RestSystem.AmbushEnemyPrefab 인스펙터 연결 안 됨 |
| 기습 적이 가만히 있음 | (해결됨) EnterAggroMode 호출 |
| 말풍선 겹침 | (해결됨) BubbleManager 3중 보호 |
| 투사체 벽 통과 | 벽에 Static Rigidbody2D 추가 |
| 동료 겹침 | (해결됨) Separation Force |

---

# 20. 시나리오 예시

## 20-1. 표준 영입 → 배신 시나리오
1. 플레이어 BaseThreat 35로 설정
2. Wanderer 박민호 만남 → 영입 거절 (점수 35.5)
3. Wanderer 정찬서 만남 → 영입 성공 (점수 53)
4. 적 사냥 → 200G 획득, 정찬서 미정산 100G
5. 정산 안 함, 휴식 시도
6. **0.5초 후 절도 발동** (점수 ~150) → 정찬서가 100G + 장비 들고 도망
7. 휴식 종료 후 로그 발견: "정찬서가 100G와 장비 N개를 들고 도망쳤다!"
8. 추격 → 플레이어가 쏨 → Layer Enemy니까 데미지 정상
9. 정찬서 처치 → 자동 회수: "정찬서에게서 100G 회수.", "장비 N개 회수."

## 20-2. 신뢰 동료와 절도범 충돌 (해결)
1. 동료 A(유윤상, Trust 70), B(정찬서, Trust 45) 영입
2. 휴식 시작 → A, B 모두 정지
3. B 절도 → Layer Enemy, Fleeing 모드, 조용히 도망
4. **자고 있는 A는 인식 못 함** (CompanionBrain이 IsResting 체크)
5. 휴식 5초 종료 → A 깨어남
6. 시야 안에 B(Layer Enemy)가 있으면 그제야 적으로 인식 → 공격
7. 또는 B는 이미 멀리 도망쳐서 안 보임

## 20-3. 휴식 중 기습 → 치명타
1. 혼자 휴식 시작 (50% 확률 발동)
2. 적 2마리 스폰, 즉시 EnterAggroMode → 플레이어 향해 직진
3. 적 사거리 도달 → 플레이어 공격
4. 플레이어 HP 100 → 원 데미지 25 + 추가 데미지 (현재 HP 75 × 0.5 = 37.5) → HP 37.5
5. ForceWake → "잠에서 깨어났다! (적의 기습!)"
6. 즉시 전투 가능 (좌클릭, WASD 회복)

## 20-4. 배신 → 다른 동료 깨움
1. 동료 A, B 휴식 중
2. B 배신 점수 ≥ 120 → HandleBetrayal 발동
3. B에 "미안하지만... 이게 낫겠어." 말풍선
4. **A에 "B이(가) 배신했어!" 말풍선**
5. ForceWake("B의 배신!") → 휴식 강제 종료
6. A 깨어나서 자동으로 B(Layer Enemy) 공격 시작

## 20-5. Wanderer 선제공격
1. 적과 싸우다 HP 30%로 떨어짐
2. 근처 Wanderer (Morality 25, Greed 90, Fear 35) 선제공격 점수:
   ```
   75 + 90 + 30 - 35 - 0 = 160
   ```
3. 점수 ≥ 110 → "쉬운 먹잇감이군." → Hostile 모드, 즉시 공격

## 20-6. Fear NPC 거리 유지
1. Fear 85 박민호와 만남
2. 플레이어가 거리 4 안으로 진입
3. 박민호 뒷걸음질 + 5초마다 "거리 좀 둬."
4. 거리 2 안: "더 가까이 오지 마!"
5. 더 다가가면 영입 메뉴 가능 (interactionRange 6 안이면 hover 강조 + 우클릭)
6. 좌클릭으로 공격하면 → Fear ≥ 60이라 Fleeing 모드 → 도망감
7. 12 거리 멀어지면 멈춤 → 다시 마주칠 수 있음

---

# 21. 기획서 대비 변경/확장 사항

## 21-1. 변경된 부분

| 기획서 (README.md) | 구현 |
|---|---|
| 정산 후 미정산 = max(0, 미정산 - 지급) | **1회 정산 시 미정산 = 0 클리어** |
| 휴식 시 시간 정지 (모두) | **플레이어 + Following/Waiting 동료만 정지**, 적 정상 동작 |
| EnemyCharacter는 NPCCharacter 상속 | **CharacterBase 직접 상속** (Trust/Greed 등 불필요) |
| 영입 거절은 단순 거절 | 거절 점수도 로그에 표시 |
| NPC 인벤토리 단일 상위 슬롯 | 무기/방어구/장신구 각 1슬롯 + 리스트 |

## 21-2. 추가된 부분 (기획서에 없거나 부분적인 것)

- **NPC 지갑 시스템**: NPCStats.gold, 절도/사냥 누적, 사망 시 회수
- **NPCDropHandler**: 적대 NPC 사망 시 골드/장비 자동 회수 (죽인 게 Player/Companion일 때)
- **휴식 중 절도** (16-4 기획서엔 있지만 구체 공식 없음): TheftScore 공식 + Fear 분기 (도주/즉시공격)
- **휴식 중 기회 판정** (`CheckRestOpportunity`): 평소보다 낮은 임계값
- **휴식 중 깨우기** (`ForceWake`): 피격 / 배신 트리거
- **휴식 중 치명타** (현재 HP × 0.5 추가)
- **적 습격** (Ambush): 휴식 시작 시 확률 적 스폰, 즉시 EnterAggroMode
- **Wanderer Idle 동작**: Wander, 적 사냥, Fear 거리유지, 선제공격, 풍부한 대사
- **Wanderer 선제공격**: 성격 + 약화 보정 점수
- **영구 적대**: 한 번 인식한 적은 detection 무시 끝까지 추적
- **분리 force**: 동료들이 자연스럽게 안 겹침
- **상호작용 강조**: 마우스 hover SpriteRenderer 색상
- **정산 슬라이더 + 예상 반응 미리보기**
- **맵 생성기 (MapGenerator)**: 그리드 절차적 배치
- **NPCStats 랜덤화**: 단일 프리팹으로 다양한 NPC 생성 (이름 + 성향)
- **패시브 점수 체크**: 6초마다 + 플레이어 피격 시 즉시
- **클릭 위치 NPC 상호작용**: 가장 가까운 NPC가 아닌 마우스 클릭 위치
- **휴식 버튼 UI**: R 키 외 버튼

## 21-3. PoC에서 미구현 (기획서에 있음)

- 기력 보정에서 "공격 주기 증가" (40~69 구간) - ATK 배율만 적용
- 요구 시스템: 휴식 요구, 거리 유지 요구 (정산 요구만 구현)
- "도주 준비" 단계 (점수 90~110 사이의 경고 상태)
- NPC 반응 시스템 카테고리별 30초 쿨타임 (단순 log/bubble 쿨타임만 사용)

이들은 PoC 범위 밖으로 분류. 추후 확장 시 추가.

---

> **마지막 메모**: 이 문서는 2025-mid 기준 구현 상태 기준이다. 코드 변경 시 이 문서도 함께 업데이트 권장.
