# Task-20261029-078

- **Title:** 설정 키 매핑·줄 매달림 구조 개선과 로비 참가자 초기 끊김 수정
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/controls-and-rope-rescue
- **Base:** dev

## Context
사용자 요청의 설정 키 매핑과 줄 매달림 팀원 구조 개선을 한 작업 브랜치에서 진행한다. 후속 요청으로 구조 변경과 사용자가 편집한 Lobby 씬을 먼저 저장하고, 두 번째 참가자의 로비 입장 직후 3~5초 끊김을 조사·수정한다. Unity 조작은 CLI만 사용한다.

## 계획
1. 조작 탭의 이동 두 항목만 전방 → 후방 순서로 바꾸고 다른 항목의 순서는 유지한다(사용자 확정).
2. 이미 사용 중인 키를 지정하면 “이미 OOO에 할당된 키입니다. 키 설정 시 할당된 조작을 다시 설정해야 합니다.” 문구와 예/아니오 팝업을 표시한다.
3. 예를 누르면 기존 조작의 키를 비우고 선택한 조작에 키를 할당한다. 아니오는 기존 draft를 유지한다.
4. 키가 없는 조작이 하나라도 있으면 적용을 막는다. 적용 전 변경은 draft에만 유지하고 취소·닫기로 폐기한다. 적용 시에만 저장한다.
5. 줄에 매달린 팀원을 상호작용으로 올릴 때 수평 거리 기본 1.5→2.5m, 발 높이 허용 오차 0.75m로 완화하고 이동 중 F도 받는다. 호스트 승인 동안 구조자의 균형·추락 위험 누적·균형 충격을 멈춘다. 종료·취소·대상 이탈 후 일반 균형 처리를 복원한다.
6. 키 중복 확인/취소·빈 키 적용 차단·적용 전 저장 방지와 구조 범위/균형 보호·종료 복원·온라인 호스트 판정을 검증하고 묶음 완료 시 check-work를 실행한다.
7. 로비 입장 끊김은 클라이언트 연결·씬 로딩·플레이어 생성·음성/로그 초기화 비용을 재현·측정해 원인을 좁힌 뒤 수정한다. 호스트와 참가자 경로를 구분하고 입장 후 프레임 시간과 관련 회귀를 확인한다.

## 조사 진입점
- 지도: `Docs/Domains/player-control.md`.
- 설정: `Assets/Scripts/UI/SceneIntegration/CommonSettingsUI.cs`, `Assets/Scripts/Systems/Player/InputBindingPreferences.cs`, `Harness/Project/Facts/settings-ui.md`.
- 구조: `Assets/Scripts/MainStage/Tightrope/Rope/TightropeHangSystem.cs`, `Assets/Scripts/Systems/Player/PlayerBalance.cs`, `Assets/Scripts/Systems/Settings/TightropeSettings.cs`.

## 미정·주의
- 구조 거리 2.5m·높이 허용 0.75m는 이번 구현 초기값이며 GameSettings.Tightrope.Hang에서 조정한다. 최종 체감과 실제 2클라이언트 지연은 사람이 확인한다.
- 담당은 현재 Git 사용자와 기존 공용 시스템 Task에 따라 @MoHoDu로 기록했다.
- 기존 UI 고도화(Task-077)·외줄 고도화(Task-074)와 관련 코드가 겹치므로 구현 시 변경 파일·통합 주의점을 log에 기록한다.
- 시작 기준은 로컬 dev 및 origin/dev가 가리키던 `935f5bd60beb7729082d46e8e0a9525c00060145`이며 원격 fetch는 하지 않았다.
- doctor에서 private submodule 미초기화·기존 Task-077 중복을 확인했다. 이번 준비에는 영향 없지만 Unity 검증 전 환경 재확인이 필요하다. 기존 Task 번호·에셋은 수정하지 않는다.
- 사용자 후속 저장 요청으로 구조 변경·사용자 Lobby 씬까지 커밋한다. TMP 폴백 폰트의 별도 변경은 보존하고 stage하지 않는다. 공개 push는 private 커밋의 main 선반영 훅으로 차단된 상태이며 dev 반영은 별도 PR 검토다.
