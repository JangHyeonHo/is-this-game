# Guildwright *(working title)*

판타지 길드를 운영하며 모험가를 육성하고, 그들로 파티를 짜서 의뢰에 내보내는
1인 개발 PC 인디게임. Steam 상업 출시를 목표로 합니다.

> **상태:** 기획 단계 (2026-07 착수). 코어 아키텍처 구축 중.

---

## 한 줄 소개

> 모험가는 스스로 싸운다. 당신이 키운 만큼만.

## 핵심 루프

```
[길드 메타 · 수십 시간]
   영입 → 육성 런 → 파티 편성 → 파견/전투 → 보상·평판 → 은퇴/사망 → 다시 영입
             ↑                        ↓
       [육성 런 · 30~40분]      [전투 · 육성 결과의 검증]
```

전투는 **자동 진행**됩니다. 다만 캐릭터가 얼마나 똑똑하게 싸우는지는
육성으로 얻은 **판단력 스탯**과 플레이어가 편성한 **전술 규칙(Tactic)** 이 결정하고,
플레이어는 제한된 **개입권**으로 결정적 순간에만 끼어듭니다.

자세한 설계는 [docs/04-game-design.md](docs/04-game-design.md).

---

## 저장소 구조

```
src/
  Guildwright.Core/         순수 C# 게임 코어. 엔진 참조 0.
                            육성 규칙 · 전투 해석기 · 전술 AI · 절차적 생성 · 길드 경제
tests/
  Guildwright.Core.Tests/   xUnit. 밸런스 회귀 테스트 포함.
docs/
  00-charter.md             프로젝트 목적 · 제약 · 성공 기준
  01-research-market.md     Steam 시장 리서치 (2026-07)
  02-research-tech.md       엔진 · 스택 · AI 리서치 (2026-07)
  03-research-art.md        아트 파이프라인 리서치 (2026-07)
  04-game-design.md         게임 디자인 문서
  05-roadmap.md             로드맵 · 마일스톤
  adr/                      주요 기술 결정과 그 근거
```

**게임 코어에는 엔진 의존성이 없습니다.** 렌더링·입력·UI를 담당할 엔진
(Unity 또는 Godot)은 얇은 표현 레이어로 나중에 붙습니다. 이유는
[docs/adr/0001-engine-agnostic-core.md](docs/adr/0001-engine-agnostic-core.md) 참조.

---

## 개발 시작하기

필요한 것은 .NET SDK 8.0 뿐입니다. 게임 엔진은 아직 필요하지 않습니다.

```bash
dotnet build
dotnet test
```

