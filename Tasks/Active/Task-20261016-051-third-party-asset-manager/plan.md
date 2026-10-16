# Plan

## Context

대기실 담당자의 외부 에셋 변경 저장이 PM 전용 규칙에 막혔다. PM은 담당자를 에셋 관리자로 지정하고 해당 변경 저장·제출을 승인했다.

## Approach

1. 역할 명부에 지정 계정을 기록하고 에셋 정책·가이드·저장 절차를 맞춘다.
2. 지정 계정은 기존 Task 금지 문구와 무관하게 private 서브모듈 포인터만 공개 저장소에서 갱신할 수 있게 한다.
3. private 저장·main 반영·push를 먼저 수행하는 절차와 다른 공개 파일 권한 제한을 유지한다.
4. 격리 회귀·구역·문서·Unity 컴파일 검사 후 dev에 fix PR을 제출하고 정상 병합한다.

## Files

- setup.md의 검사·규칙·문서와 이 Task 폴더
- 기존 EditorBuildSettings 변경은 내용 수정 없이 별도 커밋

## Risks

- 권한 위임은 폰트·Packages·ProjectSettings·SampleScene·TutorialInfo의 보류 중 변경 승인이 아니다.
- 이전 Task 기록을 덮어쓰지 않는다. Unity 미연결·검사 실패·필수 리뷰 차단을 통과로 처리하지 않는다.
- private 에셋 실제 내용은 이 PR에 포함하지 않는다. 작업자가 최신 규칙을 받아야 권한이 적용된다.

## Auto Verification

- Scripts/verify-scope.ps1 -Base origin/dev, verify-fast.ps1. Unity 컴파일·테스트는 2026-10-16 PM 명시 승인으로 생략
- Scripts/Tests/third-party-scope.test.ps1 (허용·차단 16가지)
- PowerShell 수정 파일 구문 검사

## 공동 테스트 항목

게임 공동 플레이 해당 없음. PM 내부 권한 위임은 격리 계정·gitlink 검사로 확인한다.
1. 지정 관리자의 Task/resource 포인터 저장 검사 통과.
2. 일반 작업자와 다른 공개 파일은 기존 제한 유지.
3. private 저장소 쓰기 권한 및 dev 반영 확인.
