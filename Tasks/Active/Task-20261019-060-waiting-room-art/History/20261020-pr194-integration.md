# PR #194 대기실 통합

- 일자: 2026-10-20
- 승인: PM이 dev에서 직접 충돌 해결·PR 병합과 이번 작업의 하네스 예외를 명시 승인했다.
- 추가 요청: 옛 `대기실`·`WaitingRoom` 루트를 제거하고 `WaitingRoomGreybox`를 실제 플레이 공간으로 사용한다.
- 최종 지시: 추가 검증을 중단하고 남은 실패를 공개한 상태로 병합한다.

## 적용

- 씬 충돌을 오브젝트 단위로 결합했다. PR의 아트·집결 범위를 보존하고 dev의 선반 4칸 참조 및 탐색 기능을 보존했다.
- Unity Editor API로 `WaitingRoomGameplay`·`NightSchedule`·`ExplorationTimer`를 그레이박스 아래로 이전했다. 로비 스폰은 그레이박스 위치 z=70 기준이다.
- 옛 루트 삭제 전 외부 참조를 검사했다. 조명 3개의 옛 전구 참조를 같은 조명 기구의 복제 전구로 이전했다.
- 옛 두 루트를 삭제하고 그레이박스를 활성화했다. ThirdParty 파일·포인터는 변경하지 않았다.
- 회귀 테스트의 공간 활성화 기대값·물리 좌표를 새 공간에 맞췄다. 배치 모드 로딩 대기는 실제 시간 60초, 입력은 키 해제 대기와 0.05초 누름·뗌을 사용한다. 실패 assertion을 제거하거나 생략하지 않았다.

## 검증

- Unity 컴파일·Editor API 씬 저장: PASS.
- 구조: 활성 기능 9종 각 1개, 선반 참조 4개, 끊어진 스크립트·중복 fileID·충돌 표식·옛 루트 이름 모두 0.
- verify-fast: PASS — HARD 0, 문서 길이 경고 9개.
- 마지막 대기실 PlayMode: 6건 중 5통과·1실패. `PhysicalRoom_NightLocksAndReadyStartsMorningWithoutReload`의 F 입력 후 스케줄표 열기 assertion 실패. 미해결이며 PASS로 취급하지 않는다.
- 확대 장소·공연 전환 PlayMode: 25건 중 20통과·5실패. 스케줄표 외 `Managed_CampaignChangesInsideResultEvent_DiscardsOldSettlement`, `Managed_LatePerformanceStart_PreservesInjuryAndSettlesExactlyOnce`, `Standalone_SyntheticStartAndFinish_KeepSessionEndAndGuardedRetry` 시간 초과 및 `SavedSettings_SelectEnabledActualScenesWithoutOverrides` 씬 매핑 기대값 불일치. 원인 확정·수정하지 않았다.
- 실제 2/4인 공동 플레이·정성 아트 검수: 미실행.
- 사용자 지시에 따라 추가 테스트 중단. GitHub 필수 승인·검사 조건은 우회하지 않고 병합 절차를 진행한다.
