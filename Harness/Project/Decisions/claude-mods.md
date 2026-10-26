---
summary: 팀 공용 Claude Code 모드는 저장소 마켓플레이스(curtaincall)로 배포, 첫 모드는 진행 패널(todo 색 5단계·저장/제출/진행/합치자 버튼·현재 작업 브랜치에서 활성)
status: active
updated: 2026-10-11
source: human
---

> 2026-10-27: [하네스 간소화](/Harness/Project/Decisions/harness-simplification.md)가 우선한다. 아래 PM 전용·구역·인계·순차 착수·부모 자식 제한은 과거 기준이다. 게임 의도·외부 에셋 비공개 보관·참조 보존은 유지한다.


# Claude Code 모드(팀 공용)

## 언제 읽나

진행 패널(`curtaincall-panel`)을 고치거나, 새 모드를 팀에 배포하거나, 팀원이 모드를 켜고 끄는 방법을 안내할 때.

## 결정

- 2026-10-10 / 결정자: @MoHoDu(PM)
- **배포**: 이 저장소가 마켓플레이스다. 목록은 `.claude-plugin/marketplace.json`(이름 `curtaincall`), 모드 본체는 `Harness/Mods/<모드>/`에 둔다. 저장소 `.claude/settings.json`의 `extraKnownMarketplaces`·`enabledPlugins`로 팀 전원에게 켠다. 개인은 `.claude/settings.local.json`에 `"enabledPlugins": { "<모드>@curtaincall": false }`로 끈다.
- **첫 설치**: `enabledPlugins`는 켜기만 하고 설치는 하지 않는다. 저장소 신뢰 때 설치 안내가 안 떴으면 모드가 `/plugin`·`claude plugin list`에 보이지 않는다. 이때 `/plugin`의 `curtaincall` 마켓플레이스에서 설치하거나 `claude plugin install <모드>@curtaincall --scope project`를 실행하고 Claude Code를 재시작한다. 이 명령은 `.claude/settings.json` 서식만 바꾸므로(내용 같음) 그 변경은 되돌린다.
- **선택 모드**: 필수가 아닌 모드는 `marketplace.json`에만 올리고 `enabledPlugins`에는 넣지 않는다. 원하는 사람이 `/plugin`에서 설치한다.
- **업데이트**: 모드를 고치면 그 모드 `plugin.json`의 `version`을 올린다. 올리지 않으면 팀원에게 전달되지 않는다. 받는 쪽은 자동 업데이트(설정에서 켬) 또는 `/plugin marketplace update curtaincall`. 배포는 기본 브랜치 `dev`에 병합된 뒤부터다.
- **진행 패널** `curtaincall-panel` (`Harness/Mods/Panel/`): 채팅창 위에 브랜치·워크트리·gh 계정과 현재 브랜치 Task의 todo를 보인다. Task가 여러 개면 탭으로 고른다.
  - todo 색은 git으로 판단한다. `[ ]` 하양 / 체크했지만 push 전 노랑 / push됨 초록 / PR에 포함 파랑 / `- [-]` 빨강+취소선(취소).
  - 버튼: `저장해줘`(변경·미push 있을 때) → "<Task> 저장해줘", `제출해줘`(push됐고 열린·병합된 PR 없을 때) → "<Task> 제출해줘", `진행해줘`(작업자 절에 남은 항목) → "<Task> 이어서 해줘". 누르면 그 문장을 Claude에게 보내 기존 Skill 절차를 탄다.
  - `integration/*` 브랜치: todo 대신 그 통합으로 온 열린 PR·연결 Task·병합 가능 상태. `합치자` → "<slug> 통합 병합 진행해줘"(`merge-integration`).
  - plan의 Branch로 현재 작업을 찾는다. 같은 작업 브랜치에서는 담당자·PM 조건으로 버튼을 막지 않는다. AI가 작업 중이면 중복 실행만 막으며 실제 병합은 지정 검토와 저장소 권한을 따른다.
  - Task 없는 브랜치: "진행 중인 TASK 없음", 내 다른 진행 Task, 최근 완료 Task.
  - 숨기기: 패널의 `숨기기` 버튼 또는 `/curtaincall-panel`. 숨기면 "패널 숨김" 한 줄과 `보이기` 버튼만 남는다. `[-]`(ctrl+x ctrl+a)로 접기도 된다.

## 이유

팀원이 브랜치·Task 상태와 다음 행동을 채팅창에서 바로 보고, 정해진 말("저장해줘" 등)을 버튼으로 보내 하네스 Skill 흐름을 벗어나지 않게 하려고. 저장소 마켓플레이스는 git으로 버전·리뷰가 남고 팀원 설치가 한 번의 신뢰 확인으로 끝난다.

## 바꾸려면

- 담당자가 모드를 수정할 수 있다. 수정 후 `claude plugin validate Harness/Mods/<모드>`, `claude plugin test Harness/Mods/<모드>`를 통과시키고 `version`을 올린다.
- todo 표기(`[-]` 등)를 바꾸면 `hooks/logic.ts`의 `ITEM`과 테스트도 함께 바꾼다.
- 버튼 문장을 바꾸면 `Harness/Core/Policies/skill-routing.md`의 표현과 맞춘다.
