# Guildwright — 에이전트 작업 지침

이 저장소에서 작업하기 전에 [docs/00-charter.md](docs/00-charter.md)와
[docs/08-design-revision.md](docs/08-design-revision.md)를 먼저 읽으세요.

> ⚠️ **[docs/04-game-design.md](docs/04-game-design.md)는 일부가 폐기되었습니다.**
> 예전에는 이 자리가 04였는데, 무기·직업·스킬·상태 효과·지휘·의뢰·파티·달력이
> 08에서 재설계됐습니다. **판단이 갈리면 08이 이깁니다.** 04에는 낡은 절을 지우고
> 대체 절만 가리켜 두었지만, 순서를 08로 바꾸는 게 안전합니다.
> [docs/07-formulas.md](docs/07-formulas.md)도 같습니다 — 수치는 **코드가 정본**입니다.

**"그 기능 있나요?"는 [docs/09-systems.generated.md](docs/09-systems.generated.md)로 답합니다.**
어셈블리에서 생성되므로 낡지 않습니다 — **목록에 없으면 없는 것입니다.**
왜 없는지·무엇이 막고 있는지는 [docs/09-systems.md](docs/09-systems.md)에 있습니다.
세션 시작 훅이 두 문서를 컨텍스트에 자동으로 넣으므로 대화가 초기화돼도 남습니다.

기억으로 답하지 마세요 — 이미 구현된 전열/후열·전술 규칙·전투 개입을
"없다"고 답한 사고가 실제로 있었습니다.

## 프로젝트 한 줄 요약

판타지 길드 경영 + 모험가 육성 + 전술 규칙 기반 자동 전투.
1인 개발, Steam PC 상업 출시 목표.

## 개발 환경

필요한 것은 **.NET SDK 8.0** 뿐입니다. 게임 엔진은 아직 없습니다 (의도된 것 — [ADR 0001](docs/adr/0001-engine-agnostic-core.md)).

```bash
dotnet build
dotnet test
dotnet run --project src/Guildwright.Console              # 텍스트로 플레이
dotnet run --project src/Guildwright.Console -- sim 400 5 # 배치 시뮬레이션 (시행수 · 연차)
```

`.NET`을 설치하지 않고 돌려보려면 Docker를 씁니다. `-it`가 없으면 입력을 못 받습니다.

```bash
docker build -t guildwright . && docker run -it --rm guildwright
```

## 절대 규칙

1. **`Guildwright.Core`에 엔진 의존성을 추가하지 않는다.**
   Unity/Godot 타입(`Vector2`, `Color`, `MonoBehaviour`, `Node` 등)을 쓰지 마세요.
   좌표·색이 필요하면 코어 안에 자체 타입을 정의합니다.

2. **코어는 부작용이 없어야 한다.**
   - 파일 I/O 금지
   - `DateTime.Now`, `Environment.TickCount` 등 시간 의존 금지
   - `static` 가변 상태 금지
   - **`System.Random`을 직접 쓰지 않는다.** 반드시 주입된 `IRandomSource`를 쓴다

3. **테스트 없는 코어 코드는 머지하지 않는다.**
   AI 생성 코드는 "동작하는 코드"에는 최적화되지만 "올바른 코드"에는 최적화되지 않습니다.
   자동 테스트가 유일한 방어선입니다.

4. **출시물에 생성형 AI 콘텐츠를 넣지 않는다.**
   아트·사운드·대사·현지화·스토어 이미지 전부. 근거: [ADR 0002](docs/adr/0002-no-genai-in-shipped-content.md).
   *코드를 AI로 작성하는 것은 무관하며 허용됩니다.*

5. **런타임 LLM을 추가하지 않는다.** 게임 내 AI는 GOAP / Utility AI / Behavior Tree로 구현합니다.

6. **공개 타입을 추가·삭제·개명하면 인벤토리를 같은 커밋에서 다시 만든다.**

   ```bash
   UPDATE_INVENTORY=1 dotnet test --filter SystemInventory
   ```

   잊어도 `dotnet test`가 깨지므로 놓칠 수 없습니다. 이 규칙은 기억이 아니라 테스트가 지킵니다.

7. **헌장의 "하지 않는다" 목록을 확장하지 않는다.**
   스코프를 넓히는 제안을 하기 전에 [docs/00-charter.md](docs/00-charter.md) §4를 확인하세요.
   추가하고 싶다면 **무엇을 대신 뺄지** 함께 제안하세요.

## 결정론 (중요)

이 게임의 밸런싱은 **배치 시뮬레이션**으로 합니다 — 같은 전투를 수천 번 돌려 승률 분포를 봅니다.
그러려면 **같은 시드 + 같은 입력 → 항상 같은 결과**여야 합니다.

- 난수는 `IRandomSource`를 통해서만 얻는다
- 컬렉션 순회 순서가 결과에 영향을 준다면 정렬을 보장한다
  (`Dictionary`/`HashSet` 순회 결과를 그대로 로직에 쓰지 않는다)
- 부동소수점 누적 순서에 의존하는 로직을 피한다
- 새 시스템을 추가하면 **재현성 테스트**(같은 시드로 두 번 돌려 결과 동일)를 함께 추가한다

## 코드 스타일

- C# 12 / .NET 8
- 도메인 모델은 가능한 한 불변(immutable). 변경은 새 인스턴스를 반환
- 규칙(rule)과 상태(state)를 분리. 규칙은 정적 순수 함수에 가깝게
- 밸런스 수치는 코드에 하드코딩하지 말고 데이터로 분리
- 공개 API에는 XML 문서 주석. 한국어로 써도 됩니다
- 테스트는 xUnit. 테스트 이름은 `메서드_상황_기대결과`

## 작업 분담

| 에이전트가 하는 일 | 사람이 하는 일 |
|---|---|
| 코어 로직 · 테스트 · 리팩터링 | 게임 필 · 페이싱 · 재미 판단 |
| 데이터 스키마 · 직렬화 | 체감 밸런스 튜닝 |
| 배치 시뮬레이션 실행 및 통계 | 아트 · 연출 결정 |
| 문서화 | 스코프 결정 |

**밸런스 수치를 임의로 "적당해 보이게" 바꾸지 마세요.**
대신 배치 시뮬레이션을 돌려 데이터를 제시하고 사람이 판단하게 하세요.

## 커밋

- 작업 브랜치: `claude/game-project-planning-dm9lzh`
- 커밋 메시지는 한국어 또는 영어. 무엇을 왜 바꿨는지 쓸 것
- `dotnet test`가 통과하지 않으면 커밋하지 않는다
