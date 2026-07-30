#!/bin/bash
#
# 세션이 시작·재개·초기화될 때마다 "이미 있는 것"을 컨텍스트에 넣습니다.
#
# 왜 필요한가:
#   대화 컨텍스트는 길어지면 요약되거나 초기화됩니다. 그때 기억으로 답하면 이미 있는
#   기능을 없다고 말하게 됩니다. CLAUDE.md에 "읽으세요"라고 적는 것으로는 부족합니다 —
#   읽는 것이 선택이면 빠지므로 선택을 없앱니다.
#
# 무엇을 넣는가:
#   docs/09-systems.generated.md — 어셈블리에서 생성된 공개 타입 목록 (낡으면 테스트가 깨짐)
#   docs/09-systems.md          — 없는 것 · 제약에 막힌 것 (기능 부재로 오진하는 것을 막음)
#
# 무엇을 넣지 않는가:
#   명세(04)와 결정 기록(08)은 넣지 않고 "어디를 보라"만 알려줍니다. 긴 컨텍스트는
#   모든 구간에서 정확도를 떨어뜨리고, 특히 의미가 비슷하지만 관련 없는 내용이
#   능동적으로 오답을 유도합니다 (distractor interference). 위 둘은 짧고 사실 위주라
#   그 위험이 낮습니다. 근거: docs/README.md §6.2
#
set -euo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

GENERATED="$ROOT/docs/09-systems.generated.md"
NOTES="$ROOT/docs/09-systems.md"

# 둘 다 없으면 넣을 게 없습니다. 세션 시작을 막을 이유는 없으므로 조용히 넘어갑니다.
[ -f "$GENERATED" ] || [ -f "$NOTES" ] || exit 0

BODY=$(
  cat <<'HEADER'
# 이 저장소에 이미 있는 것 (세션 시작 시 자동 주입)

기능의 존재 여부는 기억이 아니라 아래 생성 목록으로 답한다.

- 목록에 있으면 있는 것이다.
- 목록에 없으면 없는 것이다. 별도 확인이 필요 없다.
- 있는데도 못 쓰는 것 같으면 아래 "제약이 기능을 막고 있는 곳"을 본다.

**먼저 읽을 문서는 `docs/README.md`다** — 어느 문서가 무엇을 답하고 무엇을 고쳐도
되는지가 거기 있다. 요점만:

| 알고 싶은 것 | 볼 곳 |
|---|---|
| 지금 게임이 어떻게 굴러가나 | `docs/04-game-design.md` (현재 명세 · 덮어씀) |
| 왜 · 누가 정했나 | `docs/08-design-revision.md` (결정 기록 · **추가만**) |
| 그 수치가 어디 있나 | `docs/07-formulas.md` (코드 위치 지도 · 값은 코드가 정본) |
| 얼마로 측정됐나 | `docs/06-balance-log.md` (측정 기록 · **추가만**) |

**표기를 구분한다** — [확정] / [방향] / [검토중] / [제안].
[확정]만 착수할 수 있고, **[제안]은 에이전트 안이며 승인된 적이 없다.**
제안을 사람의 결정처럼 인용하면, 사람이 기억하지도 못하는 결정이 근거가 된다.

공개 타입을 추가·삭제했다면 같은 커밋에서 다시 만든다:
`UPDATE_INVENTORY=1 dotnet test --filter SystemInventory`

---

HEADER
  echo
  [ -f "$GENERATED" ] && cat "$GENERATED"
  echo
  echo "---"
  echo
  [ -f "$NOTES" ] && cat "$NOTES"
) || true

if command -v jq >/dev/null 2>&1; then
  jq -n --arg ctx "$BODY" \
    '{hookSpecificOutput:{hookEventName:"SessionStart",additionalContext:$ctx}}'
else
  # jq가 없으면 표준출력으로 내보냅니다 (SessionStart는 stdout도 컨텍스트에 들어갑니다).
  printf '%s\n' "$BODY"
fi
