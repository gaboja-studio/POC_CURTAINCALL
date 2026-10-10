---
summary: 팀 공용 Claude Code 모드는 저장소 마켓플레이스(curtaincall)로 배포, 첫 모드는 진행 패널(todo 색 5단계·저장/제출/진행/합치자 버튼·담당자만 활성)
status: active
updated: 2026-10-10
source: human
---

# Claude Code 모드(팀 공용)

## 언제 읽나

진행 패널(`curtaincall-panel`)을 고치거나, 새 모드를 팀에 배포하거나, 팀원이 모드를 켜고 끄는 방법을 안내할 때.

## 결정

- 2026-10-10 / 결정자: @MoHoDu(PM)
- **배포**: 이 저장소가 마켓플레이스다. 목록은 `.claude-plugin/marketplace.json`(이름 `curtaincall`), 모드 본체는 `Harness/Mods/<모드>/`에 둔다. 저장소 `.claude/settings.json`의 `extraKnownMarketplaces`·`enabledPlugins`로 팀 전원에게 켠다. 개인은 `.claude/settings.local.json`에 `"enabledPlugins": { "<모드>@curtaincall": false }`로 끈다.
- **선택 모드**: 필수가 아닌 모드는 `marketplace.json`에만 올리고 `enabledPlugins`에는 넣지 않는다. 원하는 사람이 `/plugin`에서 설치한다.
- **업데이트**: 모드를 고치면 그 모드 `plugin.json`의 `version`을 올린다. 올리지 않으면 팀원에게 전달되지 않는다. 받는 쪽은 자동 업데이트(설정에서 켬) 또는 `/plugin marketplace update curtaincall`. 배포는 기본 브랜치 `dev`에 병합된 뒤부터다.
- **진행 패널** `curtaincall-panel` (`Harness/Mods/Panel/`): 채팅창 위에 브랜치·워크트리·gh 계정과 현재 브랜치 Task의 todo를 보인다. Task가 여러 개면 탭으로 고른다.
  - todo 색은 git으로 판단한다. `[ ]` 하양 / 체크했지만 push 전 노랑 / push됨 초록 / PR에 포함 파랑 / `- [-]` 빨강+취소선(취소).
  - 버튼: `저장해줘`(변경·미push 있을 때) → "<Task> 저장해줘", `제출해줘`(push됐고 열린·병합된 PR 없을 때) → "<Task> 제출해줘", `진행해줘`(작업자 절에 남은 항목) → "<Task> 이어서 해줘". 누르면 그 문장을 Claude에게 보내 기존 Skill 절차를 탄다.
  - `integration/*` 브랜치: todo 대신 그 통합으로 온 열린 PR·연결 Task·병합 가능 상태. `합치자` → "<slug> 통합 병합 진행해줘"(`merge-integration`).
  - 담당자(`meta.md` Assignee = gh 계정)가 아니거나 Claude가 작업 중이면 버튼을 흐리게 하고 눌러도 동작하지 않는다. `합치자`는 PM(`Facts/team.md`)만.
  - Task 없는 브랜치: "진행 중인 TASK 없음", 내 다른 진행 Task, 최근 완료 Task.
  - 숨기기: 패널의 `숨기기` 버튼 또는 `/curtaincall-panel`. 숨기면 "패널 숨김" 한 줄과 `보이기` 버튼만 남는다. `[-]`(ctrl+x ctrl+a)로 접기도 된다.

## 이유

팀원이 브랜치·Task 상태와 다음 행동을 채팅창에서 바로 보고, 정해진 말("저장해줘" 등)을 버튼으로 보내 하네스 Skill 흐름을 벗어나지 않게 하려고. 저장소 마켓플레이스는 git으로 버전·리뷰가 남고 팀원 설치가 한 번의 신뢰 확인으로 끝난다.

## 바꾸려면

- 모드 수정은 PM 영역(하네스)이다. 수정 후 `claude plugin validate Harness/Mods/<모드>`, `claude plugin test Harness/Mods/<모드>`를 통과시키고 `version`을 올린다.
- todo 표기(`[-]` 등)를 바꾸면 `hooks/logic.ts`의 `ITEM`과 테스트도 함께 바꾼다.
- 버튼 문장을 바꾸면 `Harness/Core/Policies/skill-routing.md`의 표현과 맞춘다.
