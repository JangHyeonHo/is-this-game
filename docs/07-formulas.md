# 07. 수치가 어디 있는가 — 코드 위치 지도

밸런스를 만지거나 규칙 하나를 고치려면 그 값이 어느 파일에 있는지부터 찾아야 한다.
이 문서는 그 탐색을 없앤다 — "무엇이 어디에 있는지"만 적는다.

**값 자체는 적지 않는다.** 수치는 코드가 정본이다. 틀린 값은 사람을 속이지만
틀린 파일 경로는 파일을 열자마자 드러나므로, 이 문서는 낡아도 사람을 속이지 않는다.

| 알고 싶은 것 | 볼 곳 |
|---|---|
| 지금 규칙이 무엇인가 | [04-game-design.md](04-game-design.md) |
| 왜 그 숫자인가 | [06-balance-log.md](06-balance-log.md) |
| 문서 쓰는 규칙 | [README.md](README.md) |

---

## 0. 임시값 표시 규칙

밸런스 수치는 대부분 아직 검증되지 않았다. 검증된 값과 섞이면 검증된 것처럼 읽히므로
코드에 표시를 남긴다.

```csharp
/// <summary>
/// ⚠️ 임시값 — 배치 시뮬레이션으로 검증하고 근거를 docs/06-balance-log.md에 남기세요.
/// </summary>
```

**⚠️ 표시가 있는 값을 감으로 고치지 않는다.** 시뮬레이션을 돌려 데이터를 가져오고
판단은 사람이 한다. 남아 있는 임시값 목록은 `06-balance-log.md` #41 · #51에 있다.

---

## 1. 난수 — 모든 것의 전제

배치 시뮬레이션이 성립하려면 난수가 재현되어야 한다. 그래서 난수는 다른 어떤 수치보다
먼저 본다 — 여기가 깨지면 아래 모든 값의 측정이 무의미해진다.

| 무엇 | 어디 |
|---|---|
| 난수원 인터페이스 | `Rng/IRandomSource.cs` |
| 구현 (xoshiro256** + SplitMix64) | `Rng/DeterministicRandom.cs` |
| 스트림 분기 (`Fork`) · 안정 해시 | 같은 파일 |

- **`System.Random`을 직접 쓰지 않는다.** 주입된 `IRandomSource`만 쓴다
- **`string.GetHashCode()`를 쓰지 않는다** — 실행마다 달라진다. `Fork`는 FNV-1a를 쓴다
- 재현성 테스트: `DeterministicRandomTests` · `FloatingPointStabilityTests`

---

## 2. 모험가

원천 능력치와 성장 곡선이 여기 있다. 육성 체감을 만지려면 성장 곡선부터 본다.

| 무엇 | 어디 |
|---|---|
| 원천 능력치 6종 | `Adventurers/PrimaryStats.cs` |
| 파생 수치 공식 (원천 → 전투 수치) | `Adventurers/DerivedStats.cs` |
| 파생 보정 (겪은 것이 쌓이는 값) | `Adventurers/DerivedBonuses.cs` |
| 성장 곡선 · 개화 · 기질 · 노화 | `Adventurers/GrowthProfile.cs` |
| 평가서 (부정확한 추정) | `Adventurers/Appraiser.cs` |
| 모험가 엔티티 · 나이 누적 · 등급 · 전직 | `Adventurers/Adventurer.cs` |

---

## 3. 무기 · 직업 · 스킬

셋 다 **데이터 표**다. 직업 · 스킬 · 무기를 추가하는 것은 표에 줄을 넣는 일이고,
코드 경로를 늘리는 일이 아니다.

| 무엇 | 어디 |
|---|---|
| 무기 표 (위력 · 속도 · 사거리 · 손 · 적재량) | `Weapons/WeaponKind.cs` → `Weaponry.Table` |
| 장착 4칸 · 위력·속도 합산 · 전환 | `Weapons/Loadout.cs` |
| 숙련도 · 효율 곡선 · 획득량 | `Weapons/WeaponProficiency.cs` |
| 적성 굴림 (잠재력과의 상관 + 노이즈) | `Weapons/WeaponAptitudes.cs` |
| 직업 표 (요구 숙련 · 슬롯 · 수주 난이도 · 유지비) | `Skills/Job.cs` → `Jobs.Table` |
| 스킬 표 (마나 · 쿨다운 · 요구 무기 · 보정) | `Skills/Skill.cs` → `SkillBook.Table` |

---

## 4. 전투

| 무엇 | 어디 |
|---|---|
| 피해 · 회피 · 치명타 · 지속 피해 계산 | `Combat/DamageModel.cs` |
| 라운드 진행 · 행동 실행 · 지휘 개입 | `Combat/BattleResolver.cs` |
| 전투원 상태 (HP · 마나 · 쿨다운 · 상태 효과) | `Combat/Combatant.cs` |
| 전열/후열 · 표적 선정 · 행동 순서 | `Combat/BattleState.cs` |
| AI 결정 (전술 규칙 + 효용 + 판단력 노이즈) | `Combat/TacticalBrain.cs` |
| 전술 규칙 정의 | `Combat/TacticRule.cs` |
| 상태 효과 기전 8종 · 이름 표 · 치료제 | `Combat/StatusEffect.cs` |
| 적 무리 생성 | `Careers/EncounterGenerator.cs` |
| 육성 → 전투 변환 | `Combat/CombatantFactory.cs` |

`CombatantFactory`가 육성과 전투를 잇는 **유일한 다리**다. 육성 결과가 전투에 어떻게
반영되는지 확인할 때 여기만 보면 된다.

---

## 5. 육성

| 무엇 | 어디 |
|---|---|
| 활동 7종 · 가중치 · 피로 비용 | `Training/TrainingActivity.cs` |
| 달 단위 진행 · 컨디션 · 피로 · 성과 등급 · 부분 결산 | `Training/TrainingYearSession.cs` |
| 문턱 상수 (피로 · 실패선 · 회복량) | `Training/TrainingRules.cs` |
| 예보 (예상 성장 · 피로 · 실패 확률) | `Training/TrainingForecaster.cs` |
| 자동 방침 | `Training/TrainingPolicy.cs` |

---

## 6. 파견 · 의뢰

| 무엇 | 어디 |
|---|---|
| 의뢰 정의 (형태 · 출처 · 기간 · 강도 · 승급) | `Careers/Contract.cs` |
| 게시판 생성 (랭크 · 계절 · 강도 산출) | `Careers/ContractBoard.cs` |
| 파견 진행 (달 단위 · 조우 · 휴식 · 판정) | `Careers/DeploymentSession.cs` |
| 파견 상수 · 보급 짐 한도 | `Careers/DeploymentRules.cs` |
| 결산 (성장 · 보수 · 사고 위험 · 승급) | `Careers/CareerSimulator.cs` |
| 경력 상수 (위험 · 보수 · 판단력) | `Careers/CareerRules.cs` |
| 전투 결과 → 결산 변환 | `Careers/BattleReport.cs` |
| 겪은 것 → 성장 방향 | `Careers/CombatExperience.cs` |
| 멘토 | `Careers/Mentorship.cs` |

---

## 7. 파티 · 등급

| 무엇 | 어디 |
|---|---|
| 등급 F~SS · 눈금 연산 · 화면 표기 | `Parties/Rank.cs` |
| 파티 규칙 상수 (인원 · 6개월 · 자격 격차 · 평가 문턱) | `Parties/PartyRules.cs` |
| 조합 식별 (멤버 집합 = 식별자) | `Parties/PartyComposition.cs` |
| 조합 규칙 · 가입 자격 판정 | 같은 파일 → `PartyFormation` |
| 정규 파티 (등급 · 평가 · 승급 · 해체) | `Parties/Party.cs` |
| 장부 (가상 누적 + 정규 소속) | `Parties/PartyLedger.cs` |

---

## 8. 도구

수치를 고치기 전과 후에 돌리는 것들이다. 감으로 고치는 대신 여기서 숫자를 뽑는다.

| 무엇 | 어디 |
|---|---|
| 배치 시뮬레이터 | `Balance/BatchSimulator.cs` · `Balance/TrainingSimulator.cs` |
| 콘솔 (플레이 가능한 프로토타입) | `src/Guildwright.Console/` |
| 인벤토리 생성기 | `tests/Guildwright.Core.Tests/SystemInventory.cs` |

```bash
dotnet run --project src/Guildwright.Console              # 플레이
dotnet run --project src/Guildwright.Console -- sim 400 5 # 배치 시뮬레이션
docker build -t guildwright . && docker run -it --rm guildwright
```

---

## 9. 아직 검증하지 않은 것

아래 값들은 코드에 있지만 설계와 맞는지 확인되지 않았다. 이 근처를 고칠 때는
값 하나를 고치는 것으로 끝나지 않는다는 뜻이므로 먼저 본다.

| 무엇 | 상태 |
|---|---|
| 능력치 999 스케일 | 하드캡은 정해졌으나 공식이 구 스케일이다. 회피·명중이 여기 걸려 있다 |
| 회피 상한 | 설계는 "격차 900이어도 100%를 안 넘는다"인데 현재 상한이 훨씬 낮다 |
| 감산 방어식의 후반 거동 | 위력이 커지면 방어가 무의미해지는 구간이 온다 |
| 마나가 실제로 제약이 되는가 | 8달 파견에서 제약이 된 달이 0이었다 (`06` #48) |
| 난이도 6 이상 전투 | 견습 파티가 진다. 절대 수치 문제다 (`06` #43) |

자세한 것은 [09-systems.md](09-systems.md)의 "제약이 기능을 막고 있는 곳"에 있다.
