# Handoff

## 지금 상태

- 단계: in progress — PM 승인으로 Task-002 문서 작업 공간과 공용 발견 파일 소유를 확정하고 초안 작성 완료.
- 작성: `Docs/Guides/assets-folder-rules.md`에 현재 Assets·Resources 계층, planned 코드 구역, 예외와 재점검 절차. `Docs/Domains/assets-structure.md`와 두 index에서 에이전트·사람의 발견 경로 연결.
- 브랜치: `feat/assets-folder-rules`. 다른 브랜치의 변경을 가져오지 않음. PR·병합·커밋·push 없음.

## 다음 할 일

1. 현재 폴더 대조와 Task별 스크립트 구역 배정 예시를 공동 리뷰한다.
2. 문서·구역 검증을 실행할 수 있는 환경에서 검사 후 결과를 기록한다.
3. 제출은 별도 요청 후 `integration/document-project` 대상으로 진행한다.

## PM이 확인할 것

- 에셋 이동·이름 변경·삭제 및 템플릿 정리는 범위 밖이며 별도 승인 필요.
- LFS 체크아웃으로 기존 변경 표시된 `Assets/TutorialInfo/Icons/URP.png`는 손대지 않았음.

## Verification

- 구역 검사: `verify-scope.ps1` 미실행 — 이 격리 세션의 명령 보호가 PowerShell 스크립트 실행을 거부함. `setup.md`·Integration 공용 파일 표와 변경 파일 목록을 수동 대조; URP.png 기존 변경은 허용 구역 밖이며 이 작업에서 수정하지 않음.
- 1 문서/규칙: `git diff --check` 통과. `verify-fast.ps1`도 동일한 보호로 미실행; 구조·의미는 실제 Assets 트리와 읽기 전용 대조(2026-09-30).
- 2 컴파일: 생략 — 문서만 작성, Unity 코드·에셋 변경 없음.
- 3 테스트: 미실행 — 프로젝트 테스트 어셈블리 없음(`NO_PROJECT_TESTS`), PASS로 간주하지 않음.
- 4 실행 로그: 게임 실행 생략 — 문서 작업.
- 5 공동 테스트: 아직 미실시 — 폴더 분류, 스크립트 구역 배정 예시, 재점검 체크리스트 리뷰 필요.
