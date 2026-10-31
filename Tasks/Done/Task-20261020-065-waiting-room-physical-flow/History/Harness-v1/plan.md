# Plan

## Context

공연 후 실제 대기실 대신 예전 UI 선택용 그레이박스가 나타난다. 첫 개인 퇴장으로 팀 탐색 타이머가 시작되어야 하지만 SpaceLinkDirector는 대기실 전체를 DummyExploration으로 교체한다.

## Approach

1. 기존 미커밋 Task-064 보존·분리를 먼저 해결하고 setup.md 작업 구역을 확정한다. 사용자 요청 없는 커밋·push·PR·병합은 하지 않는다.
2. BaseGameSettings.SpaceTransitionSettings에 같은 씬 유지/별도 탐색 씬 로드 정책을 명시한다. 실제 설정은 유지, 기존 더미 테스트는 별도 로드를 명시한다.
3. SpaceLinkDirector.HandleChanged와 LeaveWaitingRoom 양쪽에 정책을 적용한다. 실제 첫 퇴장은 타이머만 시작하며 나머지 인원·대기실 씬을 유지한다.
4. Unity 에디터에서 WaitingRoomBase의 실제 WaitingRoom을 활성화한다. 기존 로비를 복귀 스폰, 로비→무대 통로 문을 퇴장/야간 잠금 경계, R10_StageEntrance를 집결 구역으로 연결한다.
5. 그레이박스에서 기능 참조·스폰·스케줄표 F 상호작용·폰 등을 실제 배치로 함께 옮긴다. 활성 스폰 묶음 하나만 유지하고 더미 이동 선택 UI를 끈다.
6. WaitingRoomExitWatch의 개인 퇴장 기록을 재사용해 나간 인원의 역방향 재진입만 막는다. 미퇴장자 퇴장은 허용하며 해당 쉬는 시간 종료 시 초기화한다. 로컬 이동 권한·호스트 판정을 일치시킨다.
7. 기존 StageEntranceGather→CampaignRun의 생존자 전원 집결 및 밤 스케줄 확정·전원 준비 흐름을 재사용한다.

## Files

- 구현: Systems/SpaceLink/SpaceLinkDirector.cs, Systems/Settings/BaseGameSettings.cs, 게임 설정 에셋, WaitingRoomBase 씬, WaitingRoom/ExplorationTimer 출구 코드, SpaceLink PlayMode 테스트.
- 기존 Task-064 세 구현/테스트 파일은 추가 수정하지 않고 회귀만 재실행한다.

## Risks

- 실제 문/통로가 통행 불가능하면 임의로 벽을 열거나 새 동선을 만들지 않고 사람 결정을 요청한다.
- 밤은 타이머 없음·출구 잠금이다. 아침 첫 퇴장부터 기본 B안 300초가 흐른다.
- 같은 씬 이름을 설정하는 우회, 전체 셔터로 개인 재입장 금지를 대체하는 구현은 제외한다.
- 미완성 backstage 씬을 연결하거나 미병합 공간 브랜치를 가져오지 않는다.
- 씬·설정 에셋은 Unity 에디터에서만 수정하며 raw YAML을 편집하지 않는다.

## Auto Verification

- 최초 구역 검사 FAIL은 사용자 승인 Task-064 로컬 커밋 6bd83ca 분리로 해소. 원격 없는 로컬 integration 기준을 명시하며 선행 실패 시 후속 검증을 진행하지 않는다.
- 구역→문서→컴파일→관련 EditMode/PlayMode→실제 입력/로그 순서.
- 실제 맵 활성·그레이박스 비활성, 실제 로비 스폰, 출구/집결/셔터 참조, 더미 버튼 미노출 확인.
- 출구 전 타이머 미시작, 첫 퇴장 후 감소, DummyExploration 미로드·씬 handle 유지·남은 인원 잔류, 후속 퇴장으로 초기화 없음.
- 개인 재입장 금지·미퇴장자 퇴장 허용·생존자 전원 집결로 다음 공연 확인.
- 인간대포→밤 잠금/타이머 없음→스케줄/준비→아침 첫 퇴장 확인. Task-064 재사망 회귀도 유지.
- 합성 상태·위치 주입은 로직 테스트로만 보고하며 실제 입력·2인 공동 검증과 구분한다.

## 공동 테스트 항목

1. 공연 종료 후 실제 로비 복귀·스폰·외팔 부활·관전 해제 확인.
2. 아침/낮 한 명만 퇴장 시 즉시 타이머·나머지 대기실 잔류·퇴장자 재입장 금지, 생존자 전원 무대 입구 집결 시 다음 공연 확인.
3. 인간대포 후 밤 출구 잠금·탐색 타이머 없음, 기존 스케줄표 F 상호작용·전원 준비로 아침 전환 확인.
