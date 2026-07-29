#!/bin/bash
#
# 세션이 시작·재개·초기화될 때마다 "이미 구현된 것"을 컨텍스트에 밀어넣습니다.
#
# 왜 필요한가:
#   대화 컨텍스트는 길어지면 요약되거나 초기화됩니다. 그때 에이전트가 기억에 의존해
#   답하면 "포지션 개념이 없다"처럼 이미 있는 기능을 없다고 말하는 사고가 납니다.
#   실제로 났습니다 — Row / TacticRule / IBattleCommander 전부 있는데 없다고 했습니다.
#
#   CLAUDE.md에 "읽으세요"라고 적는 것만으로는 부족합니다. 읽는 것이 선택이면 빠집니다.
#   그래서 선택을 없애고 훅이 직접 넣습니다.
#
# 무엇을 넣는가: docs/09-systems.md (구현 현황 인벤토리)
#   시스템을 추가·삭제하면 그 문서를 같은 커밋에서 고쳐야 합니다.
#
set -euo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
DOC="$ROOT/docs/09-systems.md"

if [ ! -f "$DOC" ]; then
  # 문서가 사라졌으면 조용히 넘어갑니다. 세션 시작을 막을 이유가 없습니다.
  exit 0
fi

HEADER='아래는 이 저장소에 **이미 구현되어 있는 것**의 목록입니다 (docs/09-systems.md).

기능의 존재 여부를 답하기 전에 반드시 이 목록을 먼저 확인하세요.
목록에 있으면 있는 것입니다. 목록의 "아직 없는 것"에 있으면 없는 것입니다.
어느 쪽에도 없으면 그때 코드를 확인하고, 확인한 결과를 이 문서에 반영하세요.

---
'

BODY="$HEADER$(cat "$DOC")"

if command -v jq >/dev/null 2>&1; then
  jq -n --arg ctx "$BODY" \
    '{hookSpecificOutput:{hookEventName:"SessionStart",additionalContext:$ctx}}'
else
  # jq가 없으면 그냥 표준출력으로 내보냅니다 (SessionStart는 stdout도 컨텍스트에 들어갑니다).
  printf '%s\n' "$BODY"
fi
