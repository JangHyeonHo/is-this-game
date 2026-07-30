# 02. 소프트웨어 설계 — 개요

Guildwright의 코드가 어떻게 나뉘어 있고 왜 그런 모양인지를 적는다.
[01-game-design.md](01-game-design.md)가 게임의 규칙을 적는다면, 이 문서는 그 규칙을
담는 그릇의 구조를 적는다. 코드를 고치기 전에, 새 기능을 어디에 넣을지 정할 때 본다.

이 문서는 전체 구조와 시스템을 관통하는 원칙까지만 담는다. **시스템 하나하나의 설계는
[design/](design/) 아래 파일 하나씩이다** — 설계는 계속 자라므로, 자라는 부분을
시스템별 파일로 나눠 어느 문서도 비대해지지 않게 한다.

| 시스템 | 설계 문서 |
|---|---|
| 모험가 — 능력치 · 성장 · 평가서 | [design/adventurer.md](design/adventurer.md) |
| 무기 · 직업 · 스킬 — 데이터 표 | [design/weapons-jobs-skills.md](design/weapons-jobs-skills.md) |
| 전투 — 해석기 · AI · 상태 효과 | [design/combat.md](design/combat.md) |
| 훈련 — 달 단위 세션 · 피로 · 예보 | [design/training.md](design/training.md) |
| 의뢰와 파견 — 게시판 · 진행 · 결산 | [design/deployment.md](design/deployment.md) |
| 파티와 등급 — 두 층 · 장부 | [design/party.md](design/party.md) |

여기 적힌 것은 확정된 설계만이다. 아직 없는 것은 [05-gaps.md](05-gaps.md)에 있다.

## 1. 전체 구조 — 코어와 껍데기

프로젝트는 넷으로 나뉜다.

```
src/Guildwright.Core      게임 규칙 전부. 순수 C#, 외부 의존 없음
src/Guildwright.Console   콘솔 프론트엔드. 코어를 불러서 텍스트로 보여준다
src/Guildwright.Web       웹 프론트엔드 (Blazor WASM). 07 §21 화면 구조의 프로토타입
tests/                    xUnit 테스트. 코어만 대상으로 한다
```

**게임 엔진은 아직 없고, 코어는 엔진을 모른다.** Unity로 가든 Godot으로 가든 코어는
그대로 두고 껍데기만 바꾼다. 콘솔이 첫 번째 껍데기, 웹이 두 번째 껍데기이고, 엔진이
정해지면 그것이 다음 껍데기가 된다. 배경은 [adr/0001](adr/0001-engine-agnostic-core.md)에 있다.

이 구조가 지키는 것은 두 가지다. 엔진 선택을 미룰 수 있고(M2에서 정한다), 게임 규칙
전체를 엔진 없이 테스트와 시뮬레이션으로 돌릴 수 있다.

껍데기에는 게임 규칙이 없다. 입력을 코어 호출로 바꾸고 결과를 화면으로 바꾸는 것까지만
한다. 규칙 검사를 껍데기에 두면 다음 껍데기에서 그 검사가 사라진다.

## 2. 코어의 순수성

코어는 바깥 세계를 건드리지 않는다.

| 금지 | 대신 쓰는 것 |
|---|---|
| 파일 입출력 | 껍데기가 한다. 코어는 값을 주고받기만 한다 |
| `DateTime.Now` 등 현재 시각 | 게임 내 달력만 쓴다 |
| `static` 가변 상태 | 상태는 인스턴스에 담고, 공유 데이터는 읽기 전용 표로 둔다 |
| `System.Random` 직접 생성 | 주입받은 `IRandomSource` |
| `string.GetHashCode()` | FNV-1a 안정 해시 (실행마다 값이 같다) |

다섯 가지가 전부 같은 이유에서 나온다 — **같은 입력이면 언제 어디서 돌려도 같은 결과가
나와야 한다.** 밸런스를 배치 시뮬레이션으로 잡는 프로젝트라서, 재현이 안 되는 코어는
밸런스 판단의 근거를 통째로 무너뜨린다.

## 3. 결정론 — 난수와 순서

재현성이 실제로 깨지는 지점은 난수와 순회 순서다. 그래서 이 둘을 설계로 못 박았다.

- **난수** — 모든 난수는 `Rng/IRandomSource`에서 나온다. 구현은
  `DeterministicRandom`(xoshiro256\*\* + SplitMix64 시드 확장)이고, `Fork(label)`로
  스트림을 가지 쳐서 시스템마다 독립된 난수열을 쓴다. 라벨 해시는 FNV-1a다
- **순서** — `Dictionary`·`HashSet`의 순회 순서를 로직에 쓰지 않는다. 순서가 결과에
  닿는 곳은 전부 `Ordinal` 정렬을 거친다
- **보정 계산** — 여러 보정이 겹치면 덧셈으로 모아 마지막에 한 번 곱한다. 곱셈을
  누적하면 적용 순서에 따라 결과가 달라진다
- **관전 무간섭** — 전투 기록을 켜든 끄든 난수 소비가 같다

새 시스템에는 재현성 테스트(같은 시드로 두 번 → 같은 결과)를 함께 넣는다.
`DeterministicRandomTests`와 `FloatingPointStabilityTests`가 기반을 지킨다.

## 4. 모듈 구성

코어는 아홉 개 네임스페이스로 나뉜다. 대체로 아래에서 위로 의존한다 — `Rng`가 바닥,
그 위에 도메인(모험가·무기·스킬), 그 위에 활동(전투·훈련·파견·파티), 맨 위에 도구.

| 모듈 | 맡는 것 | 중심 타입 |
|---|---|---|
| `Rng` | 결정론적 난수 | `IRandomSource` · `DeterministicRandom` |
| `Adventurers` | 모험가 | `Adventurer` · `GrowthProfile` · `Appraiser` |
| `Weapons` | 무기 표 · 장착 · 숙련 · 적성 | `Weaponry` · `Loadout` |
| `Skills` | 직업 표 · 스킬 표 | `Jobs` · `SkillBook` |
| `Combat` | 자동 전투 | `BattleResolver` · `TacticalBrain` |
| `Training` | 달 단위 훈련 | `TrainingYearSession` |
| `Careers` | 의뢰 · 파견 · 결산 | `ContractBoard` · `DeploymentSession` |
| `Parties` | 등급 · 파티 · 장부 | `Rank` · `PartyLedger` |
| `Balance` | 배치 시뮬레이터 | `BatchSimulator` |

경계가 하나 있다. **육성 세계와 전투 세계는 `CombatantFactory` 한 곳에서만 만난다.**
정보 은닉도 한 곳에 모여 있다 — 성장 곡선은 `Appraiser`의 추정으로만 껍데기에 나간다.
자세한 것은 각 시스템의 설계 문서에 있다.

## 5. 규칙과 상태의 분리

도메인 모델은 가능한 한 불변으로 두고, 변경은 새 인스턴스를 반환한다. 규칙은 상태를
갖지 않는 정적 순수 함수에 가깝게 둔다 — `DerivedStats` · `DamageModel` ·
`PartyFormation`이 그 형태다. 규칙이 순수 함수면 테스트가 입력과 출력만 보면 되고,
시뮬레이션에서 같은 규칙을 수천 번 불러도 상태 오염이 없다.

## 6. 콘텐츠는 코드가 아니라 데이터다

무기·직업·스킬·상태 효과·의뢰 이름은 전부 **기전(코드)과 이름(데이터 표)을 분리**해
놓았다. 새 콘텐츠를 추가하는 일은 표에 한 줄을 넣는 일이고, `switch`가 늘어난다면
축을 잘못 잡은 것이다. 표별 상세는 [design/weapons-jobs-skills.md](design/weapons-jobs-skills.md)와
[design/combat.md](design/combat.md)에 있다.

밸런스 수치는 전부 코드에 있고 문서에 복사하지 않는다. 어느 파일에 어떤 값이 있는지는
[03-formulas.md](03-formulas.md)가 안내한다.

## 7. 테스트 전략

코어 코드는 테스트 없이 병합하지 않는다. 테스트는 세 층이다.

1. **규칙 테스트** — 각 시스템의 규칙이 명세대로 움직이는지. 이름은 `메서드_상황_기대결과`
2. **설계 대조 테스트**(`DesignConformanceTests`) — 구현이 결정 기록의 확정 사항과
   어긋나지 않는지. 규칙이 코드 리뷰가 아니라 테스트로 지켜진다
3. **재현성 테스트** — 같은 시드로 두 번 돌려 같은 결과가 나오는지

여기에 스냅숏 장치가 하나 있다. 공개 타입 목록을 어셈블리에서 생성해
[04-implemented.generated.md](04-implemented.generated.md)로 두고, 코드와 어긋나면
`SystemInventoryTests`가 깨진다. Docker 빌드는 빌드 단계에서 전체 테스트를 돌리므로
테스트가 깨진 이미지는 만들어지지 않는다.
