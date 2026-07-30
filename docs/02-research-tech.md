# 02. 리서치 — 엔진 · 스택 · 게임 내 AI

조사: 2026-07 · 결론은 [ADR 0001](adr/0001-engine-agnostic-core.md), [ADR 0002](adr/0002-no-genai-in-shipped-content.md)

---

## 1. 엔진 (2026년 7월 현재)

### Unity

- **Runtime Fee는 2024년 9월 완전 철회.** 이후 부활 조짐 없음
- **Personal 무료. 매출/펀딩 상한 $100,000 → $200,000로 상향.** Unity 6부터 스플래시 선택제
- Pro는 2025년 1월 8%, 2026년 1월 5% 추가 인상 (약 $2,310/seat/년)
- **Unity 6.3이 현행 LTS** (2027년 12월까지 지원). 6.4는 2026-03-19 출시. 6.0 LTS는 2026년 10월 지원 종료
- 신규 Steam 릴리스의 약 35~38% (2023년 피크 ~45%에서 하락 후 안정)
- 인디 정서: "never again" → "cautiously watching"으로 이동. 회복은 됐으나 애정은 안 돌아옴

→ **이 프로젝트에는 사실상 무료입니다.** $200k 매출 전까지 0원이고, 그 지점이면 이미 성공한 상태입니다.

### Godot

- **4.7 stable: 2026-06-18 출시.** 6개월 릴리스 주기 정착
- Jolt Physics 기본 채택, SSR 재작성, 라이트매퍼 개선, 새 Asset Store, LibGodot 임베딩
- **Steam 출시 편수가 매년 거의 2배**: 618편(2023-24) → 1,500편(2024-25) → **2,864편(2025-26)**, 신규의 8~10%
- 상업작: **Brotato 1,000만 장**, **Backpack Battles $5.2M**, Cassette Beasts(Raw Fury),
  Road to Vostok(1인 3D FPS, 2026-04 EA), Buckshot Roulette
- C#은 프로덕션 레디. compute-bound 작업에서 GDScript 대비 수 배 빠름. Steamworks.NET 래퍼 존재

**약점**
- **C#은 웹 익스포트 불가** (Steam PC 타깃이면 무관)
- 콘솔 퍼스트파티 익스포트 없음 → W4 Games 등 제3자 포팅사 **$10K~50K**
- 에셋 라이브러리 약 3,000개 (Unity 80,000개+의 1/27)
- 3D 최고사양 부재 (Nanite/Lumen 대응물 없음). 스타일라이즈드~중간 충실도까지가 현실적 범위

### Unreal

- 5% 로열티, 누적 총매출 $1M 초과분부터. Unity 대비 손익분기는 누적 약 $2M
- **비추천 이유는 요금이 아니라 `.uasset` 바이너리** — AI 에이전트가 프로젝트를 볼 수 없음

### 기타

| 엔진 | 평가 |
|---|---|
| **Bevy** | 코드 온리라 이론상 AI 친화적이나, **API 불안정으로 LLM 환각이 최악**. 0.17에서 모델이 "거의 항상 틀린다"는 실증 보고 |
| GameMaker | 2D 전용, 독자 언어(GML) → LLM 훈련 데이터 얇음 |
| Defold | 모바일 지향, Steam PC 타깃과 어긋남 |
| LÖVE / MonoGame | 프레임워크지 엔진 아님. 1인이 만들 게 너무 많음 |

---

## 2. ★ AI 코딩 에이전트 친화도

### 파일 직렬화 방식

| | Godot | Unity | Unreal |
|---|---|---|---|
| 씬/프리팹 | `.tscn`/`.tres` — **사람이 읽는 텍스트** | `.unity`/`.prefab` — YAML이나 **모든 참조가 fileID + GUID** | `.uasset` — **바이너리** |
| LLM이 직접 읽고 수정 | 가능 | 사실상 불가 | 불가 |
| git diff / merge | 의미 있는 diff | conflict 빈발 | 불가 |

`.tscn` 하나가 노드 타입, 부모 관계, 프로퍼티, 스크립트, **시그널 연결, 애니메이션 키프레임,
PBR 머티리얼 파라미터까지** 전부 텍스트로 담습니다. GUID DB도, 실행 중인 에디터도 불필요합니다.

### MCP 도구 현황

- **Godot**: hybridindie/godot-mcp, IvanMurzak/Godot-MCP 등 다수.
  **`godot --headless --script`로 CLI 테스트 가능** → 에이전트 자율 루프 구성 가능
- **Unity**: **공식 지원 존재** — `com.unity.ai.assistant` 패키지, 서드파티 CoplayDev/unity-mcp v10.1.0 (2026-07, 25+ 도구)
  - 단, Unity 공식 문서가 **"AI Assistant는 베타이며 프로덕션 워크플로에 바로 투입하지 말라"**고 명시
  - 더 근본적으로 **에디터가 실행 중이어야** 동작 → CI/헤드리스 자율 루프에 부적합

### ★ Godot의 진짜 약점 — 훈련 데이터

> "LLM에서 좋은 GDScript를 뽑아내는 가장 어려운 부분은 모델을 똑똑하게 만드는 게 아니라,
> **Godot 3를 쓰지 못하게 막는 것**이다."

- GPT-4o가 `button.connect("pressed", self, "_on_pressed")` 생성 →
  문법은 유효하나 Godot 4에서 시그니처가 바뀌어 **조용히 아무것도 안 함** (최악의 버그 유형)
- 커뮤니티 보고: Godot 3와 4 함수를 섞고 존재하지 않는 함수를 만들어냄
- **Unity C#은 모든 엔진 중 LLM 훈련 데이터가 가장 많음**

**완화책:** Godot을 쓴다면 **C#을 쓰고**, 공식 문서를 프로젝트에 넣고
`CLAUDE.md`에 "Godot 4.x C# API만 사용, Godot 3 문법 금지"를 명시적으로 그라운딩할 것.

### 모든 엔진 공통의 천장

> "Godot이라 해도 에이전트는 **씬 파일을 읽을 뿐 씬을 실행하지 못한다.
> Play를 누르고 라이브 디버거 에러를 읽을 수 없다.**"

MCP로도 완전히 해결되지 않습니다. **"돌려보고 느껴보는" 루프는 여전히 사람 몫입니다.**

### 실측 (이 프로젝트의 원격 개발 컨테이너, 2026-07)

| 항목 | 결과 |
|---|---|
| Godot 4.4 mono 다운로드 → 헤드리스 실행 | ✅ `4.4.stable.mono.official` |
| .NET SDK | ✅ 8.0.129 |
| Unity 다운로드 | ❌ egress 정책상 403 |

---

## 3. 게임 내 AI

### Steam AI 공개 정책 (2026년 1월 개정)

**초점: "게임과 함께 배포되어 플레이어가 소비하는 콘텐츠"**

| 용도 | 공개 의무 |
|---|---|
| **코드 어시스턴트 · 디버깅 도구** | **면제** (Valve 명시) |
| 게임 내 최종 에셋 | 의무 |
| **마케팅 자료 · 스토어 페이지** | **의무** |
| 런타임 생성 | 의무 + 가드레일 서술 필수 (미비 시 제거 가능) |

### 시장 페널티 (실측)

- Steam 게임 **53,597종 전수 조사**: AI 공개 시 **리뷰 수 약 53% 감소**, 리뷰 내용도 더 부정적
- 리소스 있는 스튜디오는 **매출 40~60% 감소** 추정. 출시 전 위시리스트도 유의하게 적음
- AI 공개 게임 비율: 2024년 10.9% → 2025년 19.9% → 2026년 30.8%
- GDC 2026 조사(2,300명+): 개발자 **52%가 genAI가 산업에 부정적** (2년 전 18%)
- Tim Sweeney(Epic)는 Steam의 AI 태그를 "현대판 주홍글씨"라 공개 비판 — 정책 논쟁은 진행 중

### 런타임 LLM의 경제성

- 인디 실측: **하루 $9/플레이어**, "3일에 한 번 5시간" 기준 **첫 달 플레이어당 $50**
- AI Dungeon: 대역폭 **$20,000 초과**로 일시 중단 → 구독제 전환
- **패키지 판매는 매출 1회, LLM은 플레이할수록 비용 증가. 수학적으로 화해 불가능**
- BYOK(유저가 자기 키 입력)는 비용을 0으로 만들지만 일반 Steam 유저 대상으로는 진입장벽이 치명적

### 온디바이스 (2026년 현재 "된다, 단 조건부")

- **Gemma 4 E4B**: VRAM 3GB · **Phi-4-mini (3.8B)**: Q4 양자화 시 ~3GB VRAM
- llama.cpp / Ollama / LM Studio 지원. 사이드카 프로세스 + 루프백 HTTP 패턴은 엔진 중립적
- **함정:** LLM 디코드는 메모리 대역폭 바운드이고 주류 런타임은 아직 NPU를 제대로 안 씀
  → GPU VRAM 3~4GB를 게임 렌더링과 나눠 써야 함. **최소 사양이 크게 오름**
- Copilot+ PC(NPU 40 TOPS)는 2026년 초 기준 신규 Windows PC 판매의 25~30%뿐

### LLM 탑재 게임의 실제 평가

| 게임 | 결과 |
|---|---|
| Where Winds Meet | 성공했으나 **호평의 주 요인은 비주얼과 전투. LLM은 부가 요소** |
| Suck Up! | LLM이 게임플레이 코어인 드문 성공 사례 (전 EA/The Sims 출신) |
| Whispers from the Star | **"AI 캐릭터가 성장하지 않는다"** — 기억은 있어도 대화 결과로 달라지지 않음. 서사적 공허함이 실패 요인 |
| Hidden Door | 초기 평가는 긍정적이었으나 스케일 실패 |

가장 많이 추천된 Steam 리뷰 중 하나: **"실제로 결말에 영향 주는 선택은 딱 둘뿐"**
→ LLM을 넣어도 플레이어가 느끼는 서사 자유도는 생각만큼 오르지 않습니다.

### ★ 대안 — 전통적 게임 AI

- **Utility AI**: 여러 고려사항에 점수를 매겨 최고점 행동 선택.
  **심즈가 캐릭터 행동 대부분을 utility 기반으로 처리** — "살아있음"의 대표 사례
- **GOAP**: 목표 달성 행동 시퀀스를 계획. 자원과 상황 변화에 적응
- **현대적 표준은 하이브리드**: GOAP로 무엇을 할지 결정 → Behavior Tree로 실행,
  목표 선택에는 utility 스코어링

**결정적 장점:** Steam 공개 의무 없음, 리뷰 페널티 없음, 런타임 비용 0,
결정론적이라 디버깅 가능, **그리고 AI 코딩 에이전트가 가장 잘 짜는 코드 유형**
(순수 로직 + 훈련 데이터 풍부 + 유닛 테스트로 검증 가능).

---

## 4. 1인 + AI 에이전트의 실제

### 잘 된 사례

- **Void Balls** — 1인 + Claude Code 에이전트 8개 병렬, **10일** 완성.
  C# 29,000줄 / 173개 스크립트 / **88개 테스트 파일**, 적 5종, 파워업 15종, 보스전
- **Blackholio** — Claude Code + SpacetimeDB + Phaser, 상세 프롬프트 1회 + 개선 4회로 "첫 시도에 동작"

### 안 된 것 (실증)

1. **API 드리프트 환각** — 빠르게 변하는 스택(Bevy, Godot 3↔4)에서 존재하지 않는 API 생성
2. **런타임 블라인드니스** — 씬을 읽을 뿐 실행하지 못함. Play를 눌러 확인할 수 없음
3. **아트/에셋** — "TileMap 노드는 작성하지만 실제 타일 그래픽은 당신이 만들어야 한다"
4. **경제성** — 자율 헤드리스 루프가 플랜 한도에 걸려 멈춤. 취미 게임들이 토큰 비용을 회수 못 함
5. **기술부채 (정량)** — 도입 후 **기술부채 30~41% 증가, 코드 중복 48% 증가,
   리팩터링 활동 60% 감소. 90일차에 스프린트 캐파의 20~30%를 AI 생성 코드 유래 버그에 소모.**
   AI 생성 코드의 최대 45%에 보안 취약점

> "AI는 **'동작하는 코드'에 최적화되지, '올바른 코드'에 최적화되지 않는다.**
> 엣지케이스 처리, 시스템 불변식 존중, 기존 코드와의 깔끔한 통합 — 그 격차가 기술부채가 사는 곳이다"

6. **구조적으로 못하는 영역** — 게임 필 · 페이싱 · 재미 판단 / 레벨 디자인
   ("자의적으로 느껴진다") / 대사 ("밋밋하다") / 탐색적 테스팅

### 함의

> AI는 팀을 대체하지 않고 **"고용할 수 없었던 주니어 3~5명"을 대체**합니다.
> 사람의 역할은 아키텍처 · 제품 결정 · 품질 통제 — **잠 안 자는 팀의 매니저**입니다.

**기술부채의 유일한 실효 방어책은 자동 테스트입니다.**
이것이 [ADR 0001](adr/0001-engine-agnostic-core.md)에서 순수 C# 코어를 택한 핵심 이유입니다.

---

## 주요 출처

[Unity Runtime Fee 철회](https://unity.com/blog/unity-is-canceling-the-runtime-fee) ·
[Unity 6 지원 일정](https://unity.com/releases/unity-6/support) ·
[Godot 4.7 stable](https://github.com/godotengine/godot/releases/tag/4.7-stable) ·
[Godot vs Unity vs Unreal 2026](https://www.strayspark.studio/blog/godot-vs-unity-vs-unreal-2026) ·
[Why AI Writes Better Game Code in Godot](https://dev.to/mistyhx/why-ai-writes-better-game-code-in-godot-than-in-unity-10hf) ·
[Unity MCP 공식](https://unity.com/blog/unity-ai-mcp-how-to-get-started) ·
[GDScript LLM 환각](https://www.summerengine.com/blog/best-llm-for-godot) ·
[Steam AI 공개 폼 개정 (PC Gamer)](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/) ·
[AI 매출 감소 연구](https://www.digitalcitizen.life/steam-games-that-disclose-ai-use-may-sell-far-less-new-study-finds/) ·
[GDC 2026 State of the Industry](https://gdconf.com/article/gdc-2026-state-of-the-game-industry-reveals-impact-of-layoffs-generative-ai-and-more/) ·
[GOAP](https://tonogameconsultants.com/goap/) ·
[Claude Code for Game Development](https://chierhu.medium.com/claude-code-for-game-development-7a88fcd19992) ·
[기술부채 90일 리포트](https://thevibelog.dev/blog/vibe-coding-technical-debt-2026/)
