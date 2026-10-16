# Plan

## Context

패널 PR #65가 dev에 반영됐다. 최신 기획 5종과 공연장 담당 변경을 문서화하고,
부모 통합·준비 브랜치·단일 활성 단계·실제 인계 구분을 최소 확장한다.
승인 실행 계획: 공연장 인간 대포 작업공간 준비(2026-10-15).

## Approach

1. Confluence 5종 최신판과 기존 PM 결정을 대조하고 짧은 지식 파일로 반영한다.
2. 새 통합 Base, PR 방향, 준비 브랜치 배정·착수 게이트를 기존 도우미로 검증한다.
3. 기술 배정과 실제 전달 todo를 분리하고 기존 Task에는 순차 게이트를 소급 강제하지 않는다.
4. 구역·문서·컴파일·회귀 검사를 기록한 뒤 fix PR을 dev 대상으로 Squash 제출한다.
5. dev 반영 후 PM 준비 예외로 공연장 기록·단계별 작업공간을 별도 준비한다.

## Files

- 수정 예정: setup.md의 하네스·Scripts·지도·팀 안내 경로
- 읽기 전용: Confluence 10454669 / 11633076 / 15564804 / 14254330 / 18251778, CC-124
- 신규 회귀: Scripts/Tests/workspace-flow.test.ps1

## Risks

- 생존 2명 공통 규칙은 삭제하지 않고 인간 대포 1명 실제 도착 예외·연결 의존성을 명시한다.
- 예약 구역은 Allowed가 아니다. 다음 단계는 앞 통합·공동 검증·소유권 반환 후에만 배정한다.
- 다른 세션 폴더 직접 수정, 보호·hook 우회, force/reset/discard, 임의 Jira 생성 금지.
- 실제 전달 확인 전 인계 todo 체크 금지. 인간 대포 기능·4인 플레이 검증은 이번에 하지 않는다.

## Auto Verification

- 구역 검사: Scripts/verify-scope.ps1
- 1 문서/규칙: Scripts/verify-fast.ps1
- 2 컴파일: Scripts/check-work.ps1의 Unity 컴파일
- 3 테스트: PowerShell 구문·격리 Git fixture 회귀 + Unity EditMode
- 4 실행 로그: 회귀 출력 및 handoff 요약

## 공동 테스트 항목

불필요 — PM 내부 준비 자동화는 격리 fixture로 검증한다. 각 인간 대포 Task의 4인 공동 테스트는 개발 후 별도 실행한다.
