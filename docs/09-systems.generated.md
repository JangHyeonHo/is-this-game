# 구현 현황 — 자동 생성

> ⚠ **이 파일은 손으로 고치지 않습니다.** `Guildwright.Core` 어셈블리에서 생성됩니다.
> 코드와 어긋나면 `SystemInventoryTests`가 깨집니다.
>
> 다시 만들기: `UPDATE_INVENTORY=1 dotnet test --filter SystemInventory`
>
> **여기 없는 공개 타입은 존재하지 않는 것입니다.** 그게 이 파일의 쓸모입니다.
> 수치(상수)는 일부러 넣지 않았습니다 — 코드를 직접 보세요.
> 설계 맥락·미구현 목록·막혀 있는 기능은 [09-systems.md](09-systems.md)에 있습니다.

공개 타입 90개

## Adventurers

- `class Adventurer` — 모험가 한 명
- `enum AdventurerStatus`<br>　└ `Active` · `Retired` · `Crippled` · `Dead`
- `static class Appraiser` — 숨겨진 성장 곡선을 부정확하게 추정합니다
- `enum BloomTiming` — 개화 시기<br>　└ `Early` · `Normal` · `Late`
- `enum DeploymentOutcome`<br>　└ `Unharmed` · `Injured` · `Crippled` · `Died`
- `class DerivedBonuses` — 원천 능력치에 더해지는 파생 보정치
- `enum DerivedStat` — 전투에 실제로 쓰이는 수치<br>　└ `MaxHp` · `MaxMana` · `PhysicalPower` · `PhysicalGuard` · `MagicPower` · `MagicGuard` · `ActionSpeed` · `CritChance` · `EvasionChance`
- `static class DerivedStats` — 원천 능력치와 보정치로부터 전투 수치를 계산합니다
- `record GrowthProfile` — 한 모험가의 성장 곡선
- `enum PrimaryStat` — 원천 능력치<br>　└ `Strength` · `Agility` · `Finesse` · `Vitality` · `Intellect` · `Spirit`
- `static class PrimaryStatNames`
- `struct PrimaryStats` — 원천 능력치 묶음
- `record ScoutingReport` — 플레이어가 볼 수 있는 모험가 평가서
- `enum Temperament` — 기질<br>　└ `Studious` · `Balanced` · `Battleborn`
- `enum YearActivity` — 한 해에 무엇을 했는지<br>　└ `Training` · `Deployment`
- `record YearRecord`

## Balance

- `record BatchResult`
- `static class BatchSimulator` — 같은 조건의 전투를 반복 실행해 승률 분포를 냅니다
- `static class TrainingSimulator` — 훈련 방침을 배치로 돌려 성장 분포를 냅니다
- `record TrainingTrial` — 한 방침으로 여러 해를 육성했을 때의 결과 분포

## Careers

- `record BattleReport` — 그 해에 실제로 치른 전투가 어떻게 끝났는지, 그리고 이 사람이 그 안에서 쓰러졌는지
- `static class CareerRules` — 경력 시뮬레이션의 밸런스 상수
- `static class CareerSimulator` — 한 해를 진행시킵니다
- `class CombatExperience` — 실전에서 겪은 것이 무엇을 키우는지
- `record Contract` — 길드가 받는 의뢰
- `static class ContractGenerator` — 의뢰를 절차적으로 생성합니다
- `enum ContractKind` — 의뢰의 성격<br>　└ `Combat` · `Gathering` · `Exploration`
- `static class ContractResolver`
- `record ContractSupport` — 파티가 의뢰에 가져오는 비전투 역량의 총합과 그 효과
- `record Encounter` — 조우한 무리와, 피할 수 있는 가능성
- `static class EncounterGenerator` — 의뢰에 맞는 적을 만듭니다
- `enum FieldAction` — 파견 나간 한 달 동안 무엇을 할지<br>　└ `Search` · `Patrol` · `Camp`
- `record FieldMonth` — 한 달에 실제로 일어난 일
- `static class FieldRules` — 파견 월 단위 진행의 밸런스 상수
- `record FieldYearResult` — 파견 1년의 결과
- `class FieldYearSession` — 파견 1년을 월 단위로 진행합니다
- `enum JobRank` — 직업 등급<br>　└ `Apprentice` · `Journeyman` · `Adept` · `Master` · `Grandmaster`
- `static class JobRanks`
- `record Mentorship` — 선배가 후배 육성에 주는 보너스
- `enum SupportSkill` — 비전투 역량<br>　└ `TrapSense` · `Scouting` · `Portering` · `Gathering` · `Appraisal`
- `class SupportSkillSet` — 한 모험가의 비전무 역량 수준
- `static class SupportSkills`

## Combat

- `struct AttackResult` — 한 번의 공격이 어떻게 끝났는지
- `class BattleLog` — 전투 기록 수집기
- `enum BattleOutcome`<br>　└ `PlayerVictory` · `EnemyVictory` · `Draw`
- `class BattleResolver` — 전투를 끝까지 진행시킵니다
- `record BattleResult`
- `class BattleState` — 전투 한 판의 상태
- `struct ChosenAction` — 실제로 실행될 행동 하나
- `class CombatContribution` — 한 전투에서 이 캐릭터가 실제로 무엇을 얼마나 했는지
- `class Combatant` — 전투에 참여하는 한 명
- `static class CombatantFactory` — 육성한 모험가를 전투원으로 변환합니다
- `struct CommandOrder` — 플레이어의 전투 개입 요청
- `static class CommandRules`
- `static class DamageModel` — 데미지·회복 계산
- `interface IBattleCommander` — 전투 중 플레이어가 끼어들 수 있게 하는 통로
- `record StatusEffect`
- `enum StatusEffectKind` — 상태 효과<br>　└ `Empowered` · `Warded` · `Weakened` · `Sundered` · `Poisoned` · `Slowed` · `Taunted`
- `enum TacticAction` — 전술 규칙이 지시하는 행동<br>　└ `AttackNearest` · `AttackWeakest` · `AttackStrongest` · `AttackBackRow` · `AttackAll` · `HealAlly` · `BuffAlly` · `DebuffEnemy` · `Taunt` · `UsePotion` · `Defend` · `MoveBack` · `MoveFront`
- `enum TacticCondition` — 전술 규칙의 발동 조건<br>　└ `Always` · `SelfHpBelow` · `AllyHpBelow` · `EnemyHpBelow` · `SelfInFrontRow` · `SelfInBackRow` · `FrontRowEmpty`
- `struct TacticRule` — FF12 감빗과 유사한 조건-행동 규칙
- `static class TacticalBrain` — 전투 중 한 캐릭터가 무엇을 할지 결정하는 Utility AI
- `enum Team`<br>　└ `Player` · `Enemy`

## Rng

- `class DeterministicRandom` — xoshiro256** 기반 결정론적 난수 생성기
- `interface IRandomSource` — 코어의 유일한 난수 공급원

## Training

- `static class AutoTrainer` — 방침에 따라 훈련 1년을 자동으로 진행합니다
- `enum Condition` — 컨디션<br>　└ `Terrible` · `Poor` · `Normal` · `Good` · `Excellent`
- `static class ConditionExtensions`
- `struct DerivedForecast` — 원천 능력치가 그만큼 오르면 전투 수치가 얼마나 달라지는지
- `enum MonthGrade` — 한 달의 성과 등급<br>　└ `Failure` · `Poor` · `Success` · `GreatSuccess`
- `static class MonthGrades`
- `record MonthOutcome`
- `struct StatForecast`
- `static class TrainingActivities` — 활동 목록과 가중치표
- `enum TrainingActivity` — 한 달 동안 시키는 활동<br>　└ `Strength` · `Endurance` · `Technique` · `Study` · `Meditation` · `Sparring` · `Rest`
- `record TrainingActivityProfile` — 활동 하나가 무엇을 얼마나 키우는지
- `static class TrainingForecaster` — 1년 계획의 예상 성장을 계산합니다
- `record TrainingPolicy` — 훈련 방침
- `static class TrainingRules` — 월 단위 훈련의 밸런스 상수
- `class TrainingYearSession` — 훈련 1년을 월 단위로 진행하는 세션
- `record YearForecast` — 1년 계획의 예상 결과 전체

## Weapons

- `enum AptitudeGrade` — 무기 적성 등급<br>　└ `E` · `D` · `C` · `B` · `A` · `S`
- `static class AptitudeGrades`
- `enum Row` — 전투에서의 위치<br>　└ `Front` · `Back`
- `record StyleCapability` — 스타일이 여는 전술적 능력
- `class WeaponAptitudes` — 스타일별 무기 적성
- `enum WeaponClass` — 무기종<br>　└ `Blade` · `Blunt` · `Axe` · `Pierce`
- `class WeaponProficiency` — 스타일별 숙련도
- `enum WeaponStyle` — 장비 형태<br>　└ `SwordAndShield` · `DualWield` · `TwoHanded` · `Bow` · `Crossbow` · `Staff` · `Polearm`
- `static class WeaponStyles`
