# Integration-waiting-room-physical-flow

- **Feature:** 실제 대기실 복귀·물리 출구 진행 연결
- **Status:** active
- **Branch:** integration/waiting-room-physical-flow
- **Base:** dev
- **PM:** @MoHoDu
- **Updated:** 2026-10-20

## 목표

- PM 직접 로컬 수정. 첫 퇴장 시 탐색 타이머만 시작하며 실제 대기실과 나머지 인원을 유지한다.
- 선행 Task-064는 사용자 명시 승인으로 로컬 커밋 6bd83ca에 분리했다. 새 로컬 통합/작업 브랜치는 이 커밋을 포함한다. 원격 통합 생성·push·PR·병합 없음.
- 로컬 검증은 verify-scope.ps1 -Base integration/waiting-room-physical-flow 사용. 원격 기준을 꾸며 만들지 않는다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261020-065 | fix | @MoHoDu | fix/waiting-room-physical-flow | 없음 | working |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Scripts/Systems/Settings/BaseGameSettings.cs | Task-20261020-065 | 같은 씬 유지 정책 |
| Assets/Resources/Settings/GameSettings/BaseGameSettings.asset | Task-20261020-065 | 실제 물리 출구 경로 설정 |
| Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity | Task-20261020-065 | 실제 맵·기능 배치 연결 |

## Decisions

- Decided: 실제 로비 복귀, 기존 무대 통로 문에서 퇴장 판정, R10 무대 입구 집결. 새 공간 제작 제외.
- Open: 실제 2인 공동 검증.
