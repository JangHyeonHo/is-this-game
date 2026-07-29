#!/bin/bash
#
# 세션이 시작·재개·초기화될 때마다 "이미 구현된 것"을 컨텍스트에 밀어넣습니다.
#
# 왜 필요한가:
#   대화 컨텍스트는 길어지면 요약되거나 초기화됩니다. 그때 기억에 의존해 답하면
#   이미 있는 기능을 없다고 말하는 사고가 납니다. 실제로 났습니다 —
#   Row / TacticRule / IBattleCommander 전부 있는데 없다고 답했습니다.
#
#   CLAUDE.md에 "읽으세요"라고 적는 것만으로는 부족합니다. 읽는 것이 선택이면 빠집니다.
#   그래서 선택을 없앱니다.
#
# 무엇을 넣는가:
#   docs/09-systems.generated.md — 어셈블리에서 생성된 공개 타입 목록 (낡으면 테스트가 깨짐)
#   docs/09-systems.md           — 사람이 쓴 판단 (왜 없는지, 무엇이 막고 있는지)
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

기능의 존재 여부를 답하기 전에 아래를 먼저 확인하세요. 기억으로 답하지 마세요.

- 생성 목록에 있으면 **있는 것**입니다.
- 생성 목록에 없으면 **없는 것**입니다. 별도 확인이 필요 없습니다.
- 있는데도 못 쓰는 것 같으면 "제약이 기능을 막고 있는 곳"을 보세요.
  실제 사고의 원인은 기능 부재가 아니라 그쪽이었습니다.

**설계 항목은 [확정] / [검토중] / [제안] 표기를 반드시 구분하세요.**
[제안]은 에이전트가 낸 안일 뿐 승인된 적이 없습니다. 그걸 사용자의 결정처럼
인용하면, 사용자는 기억하지도 못하는 결정을 근거로 작업이 진행됩니다.
실제로 그 사고가 났습니다. 표기가 없으면 확정으로 취급하지 마세요.

공개 타입을 추가·삭제했다면 생성 목록을 같은 커밋에서 다시 만드세요:
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
