# Plan

## Context

new-pr는 현재 checkout용이고 기존 PR을 오류로 처리한다. 부모→직계 자식 PR을 안전하게 생성/재사용하는 별도 진입점이 필요하다.

## Approach

1. sync-integration -Child [-Parent] [-DryRun]. gh PM 계정·origin 확인, 필요한 remote ref만 재귀 없이 fetch.
2. 고정 SHA의 자식 Base와 dev까지 계보 검증. 임의/상향/준비 Task/builds/취소·종료 통합 차단.
3. 같은 저장소·정확한 head/base·open PR 재사용. child...parent ahead/diverged만 생성, 실패는 no-op 아님.
4. tip 재확인·생성 경쟁 복구·결과 재검증. branch 기반 API는 원자적 SHA 고정 불가; 변경 감지 시 재검토 보고.
5. PR 템플릿 공유, merge-integration Update에 일반 작업 head 가드, CI·Skill 연결.
6. switch/merge/push/update-branch/force/bypass/사용자 파일 수정 없음. 구현 결과 미커밋 유지.

## Files

- setup.md 명시 구역만. common의 Get-IntegrationMetadata/Get-RefFile/Assert-IntegrationLineage 재사용.
- A 마감 문서는 별도 로컬 commit에 보관하며 B에 포함하지 않는다.

## Risks

- GitHub 생성 중 원격 tip 변경은 삭제·수정으로 복구하지 않는다.
- API/JSON/인증 실패는 명확히 차단한다. 필수 CI 성공·실공동 테스트를 꾸며내지 않는다.

## Auto Verification

- 구역·문서: verify-scope, verify-fast, diff check, PowerShell 구문.
- 컴파일·테스트: check-work -Mode EditMode. 환경 차단은 그대로 기록.
- 격리 Git/gh mock: 생성·재사용·비교 4상태·경쟁·조회/JSON/fetch/인증 실패·계보·tip 변경.
- 보존: 매 경로 HEAD/index/dirty/private 불변, 금지 명령 0회, DryRun 외부 쓰기 0회.
- 기존 workspace-flow·안전 전환·scope 회귀. 실제 원격 한 쌍은 DryRun만.

## 공동 테스트 항목

1. 정상: PM이 대상 한 쌍 DryRun 방향·본문을 확인; 별도 승인 후 생성→재실행 시 같은 PR URL.
2. 차단: 잘못된 부모·cancelled·원격 오류/변경에서 쓰기 없이 중단. 실제 PR에는 리뷰·필수 CI·Merge commit 요구.
3. 보존: dirty 파일·index·HEAD/private 변화 없음, 부모 head PR에 Update 금지. Unity 씬·호스트/참가 역할 불필요.

실제 팀 공동 테스트·PR 생성/required CI E2E는 이번에 수행하지 않고 SKIPPED로 기록한다.
