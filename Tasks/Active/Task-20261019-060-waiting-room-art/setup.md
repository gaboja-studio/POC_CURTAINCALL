# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: 미정 (PM이 인계 전 기입)

순차 gate 통합(`Active Task` 있음)은 실제 `https://.../browse/CC-번호` URL이 필수이며 없음·미정은 배정할 수 없다.

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/WaitingRoom/Base/` — `WaitingRoomBase.unity`와 그 `Materials/`·`Prefabs/`
- 스크립트 폴더: `Assets/Scripts/WaitingRoom/`
- 에셋 폴더: `Assets/Resources/`
- Task 문서: 이 Task 폴더 (자동 포함)

## PM 허용 작업 (2026-10-19)

작업 구역 판단에는 쓰이지 않는 권한 설명이다.

- 새 에셋 임포트 및 위 구역 안에서의 활용·배치·이동(폴더 정리 포함)
- 재질(Material)·폴더 생성
- `Assets/_ThirdParty/` 에셋을 씬·프리팹에 가져다 배치·참조 (파일 수정·추가는 금지, 필요하면 PM 요청)
- 구현 중 필요한 `plan.md` 수정, 작업 구역 안 버그 수정
- 기존 그레이박스 루트(`WaitingRoom`·`대기실`)는 비활성 상태로 dev에 반영되어 있다. 기능 연결(스폰·상호작용 지점 등)을 새 아트로 옮긴 뒤 정리 방식은 공동 테스트 전에 PM과 정한다.

## 예약 구역

후속 단계의 예정 경로만 적는다. 예약은 Allowed가 아니다. PM이 앞 단계의 Allowed를 반환한 뒤 해당 단계의 작업 구역·배정된 공용 파일에만 부여한다. 소유권 이관 이력은 통합 준비 문서에 보존한다.

- 미정

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- 없음

## 수정 금지

수정 금지여도 `team-roles.md` "모든 작업자에게 허용"(빌드 씬 목록·공동 시스템 이용·게임 세팅 값·새 에셋 추가)은 허용한다.

- `Assets/Scenes/` — 위 테스트 씬 폴더만 예외
- `Assets/Scripts/Systems/` — 공용. 읽기/기존 인터페이스 재사용만
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM 또는 프로젝트에서 지정한 에셋 관리자만(역할 예외는 외부 에셋 정책 참조)

## 인계 전 체크 (PM)

- [ ] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨) — 기존 `WaitingRoomBase.unity` 사용
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`) — 기존 폴더 사용
- [ ] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 기술 배정 준비를 올리고 작업 브랜치를 만든다. 기존 준비 branch는 `-UsePreparedBranch`로 검증 후 배정 문서 sync PR을 만든다(자동 병합·강제 이동 없음). 실제 전달 todo는 PM이 별도로 확인한다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
