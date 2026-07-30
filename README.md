# Guildwright *(working title)*

판타지 길드를 운영하며 모험가를 육성하고, 그들로 파티를 짜서 의뢰에 내보내는
1인 개발 PC 인디게임. Steam 상업 출시를 목표로 합니다.

> **상태:** 코어 프로토타입 (2026-07 착수). 콘솔에서 텍스트로 플레이할 수 있습니다.

---

## 한 줄 소개

> 모험가는 스스로 싸운다. 당신이 키운 만큼만.

## 핵심 루프

```
[길드 메타 · 수십 시간]
   영입 → 매달 훈련/파견 → 파티 편성 → 전투 → 보상·평판 → 은퇴/사망 → 다시 영입
                                        ↓
                              [전투 · 육성 결과의 검증]
```

전투는 **자동 진행**됩니다. 캐릭터가 얼마나 똑똑하게 싸우는지는 육성으로 얻은
**판단력**과 플레이어가 미리 짠 **전술 규칙**이 결정하고, 플레이어는 전투 중
언제든 지시로 끼어들 수 있습니다.

자세한 설계는 [docs/01-game-design.md](docs/01-game-design.md).

---

## 저장소 구조

```
src/
  Guildwright.Core/         순수 C# 게임 코어. 엔진 참조 0.
                            육성 규칙 · 전투 해석기 · 전술 AI · 절차적 생성 · 길드 경제
tests/
  Guildwright.Core.Tests/   xUnit. 밸런스 회귀 테스트 포함.
docs/
  00-charter.md             프로젝트 헌장 — 목적 · 제약 · 성공 기준
  01-game-design.md         게임 디자인 — 규칙 전부
  02-architecture.md        소프트웨어 설계
  06-roadmap.md             로드맵 · 마일스톤
  09~11-research-*.md       시장 · 기술 · 아트 리서치 (2026-07)
  adr/                      주요 기술 결정과 그 근거
```

문서 전체의 안내는 [docs/README.md](docs/README.md)에 있습니다.

**게임 코어에는 엔진 의존성이 없습니다.** 렌더링·입력·UI를 담당할 엔진
(Unity 또는 Godot)은 얇은 표현 레이어로 나중에 붙습니다. 이유는
[docs/adr/0001-engine-agnostic-core.md](docs/adr/0001-engine-agnostic-core.md) 참조.

---

## 플레이해보기 — Docker (설치할 것 없음)

**.NET을 깔지 않아도 됩니다.** Docker만 있으면 됩니다.

```bash
docker build -t guildwright .

docker run -it --rm guildwright              # 플레이
docker run -it --rm guildwright sim 400 5    # 배치 시뮬레이션 (시행수 · 연차)
docker run -it --rm guildwright 12345        # 시드 지정 — 같은 숫자는 같은 세계
```

> ⚠️ **`-it`를 꼭 붙이세요.** 없으면 입력을 못 받아 첫 질문에서 바로 종료됩니다.

이미지를 만들 때 테스트가 함께 돌아갑니다. 깨진 채로는 빌드가 끝나지 않습니다.

## 개발 시작하기

직접 고치실 거라면 .NET SDK 8.0이 필요합니다. 게임 엔진은 아직 필요하지 않습니다.

```bash
dotnet build
dotnet test

dotnet run --project src/Guildwright.Console              # 플레이
dotnet run --project src/Guildwright.Console -- sim 400 5 # 배치 시뮬레이션
```

