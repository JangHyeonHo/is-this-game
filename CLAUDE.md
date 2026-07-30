# Guildwright — 에이전트 작업 지침

판타지 길드 경영 + 모험가 육성 + 전투(주인공 동행 시 수동 조작 · 비동행은 규칙 기반 자동).
1인 개발, Steam PC 상업 출시 목표.

이 파일은 이 저장소에서 작업할 때 지켜야 하는 것만 담는다. 설계 내용은 담지 않는다 —
분량이 늘면 지침이 읽히지 않으므로 세부는 `docs/`로 빼고 링크만 둔다.

## 어디를 보나

대화는 매번 초기화된다. 기능의 존재 여부와 설계 결정은 기억이 아니라 문서로 확인한다.

**[docs/README.md](docs/README.md)를 먼저 읽는다.** 어느 문서가 무엇을 답하고
무엇을 고쳐도 되는지가 거기 있다.

| 알고 싶은 것 | 볼 곳 |
|---|---|
| 그 기능 있나 | [docs/04-implemented.generated.md](docs/04-implemented.generated.md) — 코드 생성. 없으면 없는 것 |
| 지금 게임이 어떻게 굴러가나 | [docs/01-game-design.md](docs/01-game-design.md) — 현재 명세 |
| 코드가 어떻게 나뉘어 있나 | [docs/02-architecture.md](docs/02-architecture.md) — 설계서 |
| 왜 · 누가 정했나 | [docs/07-decisions.md](docs/07-decisions.md) — 결정 기록 |
| 그 수치는 어디 있나 | [docs/03-formulas.md](docs/03-formulas.md) — 코드 위치 지도 |
| 왜 아직 없나 · 무엇이 막고 있나 | [docs/05-gaps.md](docs/05-gaps.md) |
| 만들면 안 되는 건가 | [docs/00-charter.md](docs/00-charter.md) §4 |

세션 시작 훅이 생성 목록을 컨텍스트에 자동으로 넣는다. 읽는 것이 선택이면 빠지므로
선택으로 두지 않는다.

## 개발 환경

필요한 것은 **.NET SDK 8.0** 뿐이다. 게임 엔진은 아직 없다
(의도된 것 — [ADR 0001](docs/adr/0001-engine-agnostic-core.md)).

```bash
dotnet build && dotnet test
dotnet run --project src/Guildwright.Console              # 텍스트로 플레이
dotnet run --project src/Guildwright.Console -- sim 400 5 # 배치 시뮬레이션
docker build -t guildwright . && docker run -it --rm guildwright   # .NET 없이
```

`-it`가 없으면 입력을 못 받아 첫 질문에서 종료된다.
`.dockerignore`에서 **`docs/`와 `CLAUDE.md`를 제외하지 않는다** — 인벤토리 테스트가 쓴다.

## 절대 규칙

아래는 협상 대상이 아니다. 각 규칙 밑에 그 규칙이 무엇을 지키는지 적었다 —
이유를 모르면 편의를 위해 되돌리게 된다.

1. **`Guildwright.Core`에 엔진 의존성을 추가하지 않는다.**
   Unity/Godot 타입(`Vector2`, `Color`, `MonoBehaviour`, `Node` 등) 금지.
   좌표·색이 필요하면 코어 안에 자체 타입을 정의한다.

2. **코어는 부작용이 없어야 한다.**
   - 파일 I/O 금지 · `DateTime.Now`·`Environment.TickCount` 등 시간 의존 금지
   - `static` 가변 상태 금지
   - **`System.Random`을 직접 쓰지 않는다.** 반드시 주입된 `IRandomSource`를 쓴다

3. **테스트 없는 코어 코드는 머지하지 않는다.**
   AI 생성 코드는 "동작하는 코드"에는 최적화되지만 "올바른 코드"에는 최적화되지 않는다.
   자동 테스트가 유일한 방어선이다.

4. **출시물에 생성형 AI 콘텐츠를 넣지 않는다.**
   아트·사운드·대사·현지화·스토어 이미지 전부. 근거: [ADR 0002](docs/adr/0002-no-genai-in-shipped-content.md).
   *코드를 AI로 작성하는 것은 무관하며 허용된다.*

5. **런타임 LLM을 추가하지 않는다.** 게임 내 AI는 GOAP / Utility AI / Behavior Tree로 만든다.

6. **공개 타입을 추가·삭제·개명하면 인벤토리를 같은 커밋에서 다시 만든다.**

   ```bash
   UPDATE_INVENTORY=1 dotnet test --filter SystemInventory
   ```

   잊어도 `dotnet test`가 깨진다. 이 규칙은 기억이 아니라 테스트가 지킨다.

7. **헌장([docs/00](docs/00-charter.md))과 어긋나는 것을 만들지 않는다.**
   스코프를 넓히려면 **무엇을 대신 뺄지** 함께 제안하고, 합의되면 헌장을 먼저 고친다.

8. **결정 기록(`docs/07`)을 제자리에서 고치지 않는다.** 추가만 한다.
   바뀌면 새 항목을 쓰고 옛 항목의 상태만 바꾼다. 고치면 그때 무엇이 사실이었는지 알 수 없다.

9. **명세(`docs/01`)에는 폐기된 설계를 남기지 않는다.** 묘비도 두지 않는다 —
   묘비가 다음 판단의 근거로 인용된다. 이력은 결정 기록과 git이 가진다.

## 결정론

밸런싱을 배치 시뮬레이션으로 한다 — 같은 전투를 수천 번 돌려 승률 분포를 본다.
그러려면 같은 시드 + 같은 입력 → 항상 같은 결과여야 한다. 이것이 깨지면
밸런스 판단의 근거가 전부 사라진다.

- 난수는 `IRandomSource`를 통해서만 얻는다. `string.GetHashCode()` 금지 (실행마다 다르다)
- `Dictionary`/`HashSet` 순회 순서를 로직에 쓰지 않는다 — 정렬을 보장한다
- 여러 보정을 겹칠 때는 **덧셈으로 모아 마지막에 한 번 곱한다**
- 새 시스템에는 **재현성 테스트**(같은 시드로 두 번 → 같은 결과)를 함께 추가한다

## 코드 스타일

- C# 12 / .NET 8. 도메인 모델은 가능한 한 불변. 변경은 새 인스턴스를 반환한다
- 규칙(rule)과 상태(state)를 분리한다. 규칙은 정적 순수 함수에 가깝게 둔다
- **밸런스 수치는 코드에 두고 데이터 표로 분리한다.** 문서에 값을 복사하지 않는다
- 공개 API에는 XML 문서 주석을 단다. 한국어로 써도 된다
- 테스트는 xUnit. 이름은 `메서드_상황_기대결과`

## 작업 분담

무엇이 사람의 판단인지 미리 갈라 둔다. 체감 문제를 에이전트가 대신 정하면
그 값이 결정으로 굳는다.

| 에이전트가 하는 일 | 사람이 하는 일 |
|---|---|
| 코어 로직 · 테스트 · 리팩터링 | 게임 필 · 페이싱 · 재미 판단 |
| 데이터 스키마 · 직렬화 | 체감 밸런스 튜닝 |
| 배치 시뮬레이션 실행 및 통계 | 아트 · 연출 결정 |
| 문서화 | 스코프 결정 |

**밸런스 수치를 임의로 "적당해 보이게" 바꾸지 않는다.**
배치 시뮬레이션을 돌려 데이터를 제시하고 사람이 판단하게 한다.

## 커밋

- 작업 브랜치: `claude/game-project-planning-dm9lzh`
- **스쿼시 머지 뒤에는 브랜치를 새 master에서 다시 시작한다.**
  안 하면 같은 내용이 양쪽에 있어 병합 충돌이 난다
- `dotnet test`가 통과하지 않으면 커밋하지 않는다
- **작업이 끝나면 브랜치 푸시로 끝내지 않고 PR까지 올린다.**
  master에 없으면 플레이할 수 없다
