# 구현 현황 — 자동 생성

> **"그 기능 있나"에 답하는 파일이다.** 여기 없는 공개 타입은 존재하지 않으므로,
> 별도 확인 없이 없는 것으로 판단해도 된다.
>
> ⚠ **손으로 고치지 않는다.** `Guildwright.Core` 어셈블리에서 생성되며,
> 코드와 어긋나면 `SystemInventoryTests`가 깨진다.
> 다시 만들기: `UPDATE_INVENTORY=1 dotnet test --filter SystemInventory`
>
> 수치(상수)는 넣지 않는다 — 튜닝마다 흔들려 스냅샷이 무의미해진다. 코드를 직접 본다.
> 설계 맥락·미구현 목록·막혀 있는 기능은 [05-gaps.md](05-gaps.md)에 있다.

공개 타입 118개

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
- `static class Calendar` — 달과 계절
- `static class CareerRules` — 경력 시뮬레이션의 밸런스 상수
- `static class CareerSimulator` — 한 해를 진행시킵니다
- `class CombatExperience` — 실전에서 겪은 것이 무엇을 키우는지
- `record Contract` — 길드가 받는 의뢰 한 건
- `static class ContractBoard` — 의뢰 게시판 — 매달 랜덤으로 발생합니다
- `static class ContractFlavor` — 의뢰 이름 풀
- `enum ContractForm` — 의뢰의 형태 — 완료 판정으로 가릅니다<br>　└ `Subjugate` · `Defend` · `Gather` · `Discover`
- `static class ContractNames` — 형태·출처의 한국어 이름
- `enum ContractSource` — 의뢰의 출처<br>　└ `Realm` · `Village` · `Guild`
- `enum DeploymentFailure` — 파견이 실패한 이유<br>　└ `None` · `Wiped` · `Retreated` · `Abandoned` · `ObjectiveLost` · `NotFound` · `Unfinished`
- `record DeploymentMonth` — 한 달의 기록
- `record DeploymentResult` — 파견 한 건의 결과
- `static class DeploymentRules` — 파견 진행의 수치
- `class DeploymentSession` — 파견 한 건을 달 단위로 진행합니다
- `static class EncounterGenerator` — 의뢰에 맞는 적을 만듭니다
- `record Mentorship` — 선배가 후배 육성에 주는 보너스
- `enum MonthWork` — 그 달에 무엇을 했는가<br>　└ `Work` · `Rest`
- `enum RewardKind` — 보상의 성격<br>　└ `Pay` · `Renown`
- `enum Season` — 계절<br>　└ `Spring` · `Summer` · `Autumn` · `Winter`
- `record Supplies` — 파견에 들려 보내는 보급

## Combat

- `enum ActionRestriction` — 막히는 행동의 종류<br>　└ `None` · `Movement` · `ManaSkills`
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
- `enum CureItem` — 이 상태를 푸는 소모품<br>　└ `None` · `Antidote` · `BurnSalve` · `Bandage` · `FrostSalve` · `ParalysisCure` · `HolyWater`
- `static class DamageModel` — 데미지·회복 계산
- `enum EffectMechanism` — 상태 효과의 기전<br>　└ `StatShift` · `DamageOverTime` · `Incapacitate` · `RestrictAction` · `LoseControl` · `TargetShift` · `Barrier` · `Recovery`
- `enum EffectName` — 상태 효과의 이름<br>　└ `PowerUp` · `PowerDown` · `GuardUp` · `GuardDown` · `AccuracyUp` · `AccuracyDown` · `EvasionUp` · `EvasionDown` · `SpeedUp` · `SpeedDown` · `Poison` · `Burn` · `Bleed` · `Frostbite` · `Paralysis` · `Freeze` · `Petrify` · `Bind` · `Silence` · `Fear` · `Confusion` · `Taunt` · `Hidden` · `Barrier` · `Regen` · `Curse`
- `record EffectProfile` — 이름 하나의 설정
- `enum GrowthMode` — 지속 피해가 커지는 방식<br>　└ `None` · `PerStack` · `PerAction`
- `interface IBattleCommander` — 전투 중 플레이어가 끼어들 수 있게 하는 통로
- `enum ShiftTarget` — 수치 증감이 건드리는 대상<br>　└ `None` · `Power` · `Guard` · `Accuracy` · `Evasion` · `Speed`
- `record StatusEffect` — 한 캐릭터에게 걸려 있는 상태 효과 하나
- `static class StatusEffects` — 상태 효과 목록과 조회
- `enum TacticAction` — 전술 규칙이 지시하는 행동<br>　└ `AttackNearest` · `AttackWeakest` · `AttackStrongest` · `AttackBackRow` · `AttackAll` · `HealAlly` · `BuffAlly` · `DebuffEnemy` · `Taunt` · `UsePotion` · `GivePotion` · `SwitchWeapon` · `Defend` · `MoveBack` · `MoveFront`
- `enum TacticCondition` — 전술 규칙의 발동 조건<br>　└ `Always` · `SelfHpBelow` · `AllyHpBelow` · `EnemyHpBelow` · `SelfInFrontRow` · `SelfInBackRow` · `FrontRowEmpty`
- `struct TacticRule` — FF12 감빗과 유사한 조건-행동 규칙
- `static class TacticalBrain` — 전투 중 한 캐릭터가 무엇을 할지 결정하는 Utility AI
- `enum Team`<br>　└ `Player` · `Enemy`

## Parties

- `enum AdmissionProblem` — 증원이 안 되는 이유<br>　└ `None` · `NotEnoughMonths` · `AlreadyInRegularParty` · `Disbanded` · `AlreadyMember` · `RankTooLow` · `InvalidComposition`
- `enum FormationProblem` — 조합이 성립하지 않는 이유<br>　└ `None` · `TooFewMembers` · `SoloingLocked` · `TooManyPorters` · `NoCombatant` · `NotDeployable`
- `class Party` — 정규 파티 — 등록된 조합
- `class PartyComposition` — 한 조합 — 누가 같이 나갔는가
- `static class PartyFormation` — 조합이 성립하는지 판정합니다
- `class PartyLedger` — 파티 장부 — 가상 파티의 누적과 정규 파티의 소속을 함께 관리합니다
- `static class PartyRules` — 파티 규칙의 수치
- `enum Rank` — 등급 F ~ SS<br>　└ `F` · `E` · `D` · `C` · `B` · `A` · `S` · `SS`
- `static class Ranks` — 등급 눈금을 다루는 헬퍼
- `enum RegistrationProblem` — 정규 등록이 안 되는 이유<br>　└ `None` · `NotEnoughMonths` · `AlreadyInRegularParty` · `InvalidComposition`

## Rng

- `class DeterministicRandom` — xoshiro256** 기반 결정론적 난수 생성기
- `interface IRandomSource` — 코어의 유일한 난수 공급원

## Skills

- `record Job` — 직업 하나
- `enum JobId` — 직업 이름<br>　└ `SwordApprentice` · `Swordsman` · `TwinBlade` · `Blademaster` · `SwordSaint` · `ShieldApprentice` · `Shieldbearer` · `Guardsman` · `Knight` · `Warden` · `GreatApprentice` · `Warrior` · `Veteran` · `Champion` · `Warlord` · `SpearApprentice` · `Spearman` · `Lancer` · `SpearAdept` · `SpearSaint` · `BowApprentice` · `Archer` · `Marksman` · `Sharpshooter` · `Divineshot` · `BoltApprentice` · `Crossbowman` · `Sniper` · `Deadeye` · `Piercer` · `StaffApprentice` · `Mage` · `HighMage` · `Archmage` · `Sage` · `Axeman` · `Berserker` · `Maceman` · `Warpriest` · `Miner` · `Prospector` · `Porter` · `SkilledPorter` · `Quartermaster` · `SpellArcher` · `SpellBlade`
- `static class Jobs` — 직업 목록과 해금 판정
- `record Skill` — 스킬 하나의 정의
- `static class SkillBook` — 스킬 목록과 조회
- `enum SkillForm` — 패시브인가 액티브인가<br>　└ `Passive` · `Active`
- `enum SkillId` — 스킬 이름<br>　└ `Cure` · `Empower` · `Enfeeble` · `Provoke` · `Sweep` · `HandPotion` · `PiercingShot` · `TwinStrike` · `HeavyBlow` · `Shielding` · `SteadyAim` · `Packcraft` · `Careful` · `Reckless` · `Cheerful` · `Stubborn`
- `enum SkillSource` — 스킬이 어디서 오는가<br>　└ `Innate` · `Job`

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
- `enum Hand` — 어느 손인가<br>　└ `Right` · `Left`
- `enum Hands` — 몇 손으로 드는가<br>　└ `One` · `Two`
- `class Loadout` — 장착 4칸 — 주무기(좌·우) + 보조무기(좌·우)
- `enum Reach` — 사거리<br>　└ `Melee` · `Extended` · `Ranged`
- `enum Row` — 전투에서의 위치<br>　└ `Front` · `Back`
- `class WeaponAptitudes` — 스타일별 무기 적성
- `enum WeaponKind` — 무기 종류<br>　└ `None` · `Sword` · `Axe` · `Mace` · `Greatsword` · `Spear` · `Shield` · `Bow` · `Crossbow` · `Staff` · `Pickaxe` · `Backpack`
- `class WeaponProficiency` — 스타일별 숙련도
- `enum WeaponSet` — 어느 세트인가<br>　└ `Primary` · `Secondary`
- `record WeaponSpec` — 무기 하나의 명세
- `static class Weaponry` — 무기 목록과 조회
