---
summary: 브랜치 7종·PR/Squash와 Merge commit·선언된 부모 통합/준비 브랜치 동기화·순차 인계·PM 전용 영역
status: active
updated: 2026-10-15
source: human (2026-09-29·10-02·10-09 팀 결정, 2026-10-15 PM 승인 작업 준비 계획)
---

# 브랜치 전략

## 언제 읽나

브랜치·PR 대상·병합 방식을 고를 때. 전문은 `Harness/Core/Policies/git-workflow.md`.

## 결정

- 2026-09-29 / PM(@MoHoDu): 기본 dev. feat 기능/refactor 정리→integration 기능 묶음→dev→builds, tests는 합치지 않음. 작업→통합 Squash, 통합→dev Merge commit, 연결도 별도 Task.
- dev/integration/builds는 PR만. PM의 integration 준비/마무리 문서·빈 씬/폴더 직접 반영만 예외. dev 직접 push·보호/리뷰 우회는 허용하지 않는다.
- PM 전용: Harness·AGENTS/CLAUDE·루트 Scripts·.github·Docs/Guides·Skill·프로젝트 MCP. 권한은 team-roles. 데일리 미팅은 하네스에 넣지 않는다.
- 2026-10-02: fix/resource는 dev 또는 특정 integration에 바로 Squash PR 가능(builds 직행 금지). fix는 보통 Task·급하면 Task 없이, resource는 리소스 전용·보통 Task 없이. 기존 “예외 없이 통합만” 결정을 이 범위에서 대체.
- 2026-10-09: dev 공용 변경→integration은 Merge commit으로 내려받는다.
- 2026-10-15 / PM 승인 계획:
  - 통합 **Base 기본 dev**, 부모 integration 선택 가능. 원격 최신 부모에서 생성·메타 계보/순환 검사. 자식→메타 Base와 선언 부모→자식은 Merge commit, 임의 통합 간 PR/자식 dev 직행 금지.
  - 인간 대포는 `integration/main-stage-human-cannon`, Base `integration/main-stage`. Git ref 충돌 때문에 하위 슬래시 이름 대신 확정했고 기존 main-stage는 유지.
  - 준비 Task 브랜치 동기화는 소속 통합→그 Task만 Merge commit. 준비 표 `Task | 브랜치 | 준비 기준 SHA | 승인 SHA`와 동일 Task/Type/Integration·부모 승인 이력을 확인, 독립 구현 commit이면 배정 거부.
  - Active Task 선택값이 있으면 한 단계만 배정/착수. 후속 scaffolded·예약 구역(Allowed 아님), 실제 Jira URL·앞 integrated/실공동 검증/소유 반환 후 다음 단계 개방.
  - 기술 배정과 실전달 todo 분리. 실제 전달은 PM 확인, 준비만으로 자동 체크 없음. 브랜치 강제 이동/재생성·자동 merge 금지.
  - 기존 close-integration은 Base=dev 전용. 자식 통합 마감/Done 흐름은 이번에 확장하지 않음.
- 작성자는 gh 로그인 계정만, AI 작성자·Co-Authored-By/Generated with 줄 금지.

## 이유

작업자는 내 구역의 구현·제출만 하고, PM이 계보·병합·인계와 충돌을 관리한다. 준비만 된 다음 단계가 앞 검증을 건너뛰지 않도록 한다.

## 바꾸려면

git-workflow·new-pr/assign/start 스크립트·해당 Skill/템플릿·팀 안내를 함께 갱신하고 자동 회귀로 정상/차단 경로를 확인한다.
