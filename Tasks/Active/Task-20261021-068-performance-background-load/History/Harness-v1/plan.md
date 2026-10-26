# Plan

## Context

#196은 integration/main-stage에만 반영되어 있다. dev 반영 PR #197 및 역방향 동기화 PR #198에 gitlink·통합 문서 충돌이 있어, dev 기반 전용 fix PR로 포인터 가져오기와 자동 로드를 함께 준비한다. dev나 다른 통합 브랜치에 직접 push하지 않는다.

## Approach

1. private b6cc29c7은 현재 dev의 be547df7을 포함함을 계보로 확인. 기존 로컬 Vivox 변경은 보존한다.
2. 전용 로더를 AfterSceneLoad로 부트스트랩하고 최초 씬 스캔 및 sceneLoaded/sceneUnloaded를 처리한다.
3. 실제 Tightrope/HumanCannon 및 레거시 줄타기 proto_0.0.2를 대상으로 시각 전용 배경을 로컬 additive 로드한다. 더미·대기실·로비는 제외한다.
4. 배경 원본의 모든 root를 묶어 공통 1/3 배율과 이전 적용 중심 Z81.125/82.5를 반영한다. 원본 private 씬은 수정하지 않는다.
5. 로드/언로드를 직렬화하고 종료·연속 전환·이미 열린 배경을 구분한다. active scene과 게임 오브젝트 소유 씬은 건드리지 않는다.
6. NGO의 로컬 씬/늦은 참가 동기화 정책을 소스로 확인하고 중복 로드를 방지한다.
7. 코스·카메라·게임 설정은 보존한다. 조명·안개를 임의 재디자인하지 않는다.

## Files

- 전용 코드·테스트: Systems/PerformanceBackground.
- 공용 변경: 빌드 씬 목록과 private gitlink만.

## Risks

- 44개 배경 광원·약 417만 삼각형으로 실제 PC 성능 검토가 필요하다.
- 로컬 배경을 NGO가 중복 동기화하거나 Single 전환 중 로드가 끝나는 경합을 검증한다.
- 이미지의 정확한 수치는 읽을 수 없으므로 기존 승인된 적용 기록으로 초기 배치를 맞추고 실제 화면을 제시한다.

## Auto Verification

- 구역 → 문서 → 컴파일 → 전용 EditMode/PlayMode 테스트 → 실제 묘기 화면.
- 최초 진입, 중복 없는 배경 하나, 배치/배율, active scene 유지, 이탈 시 해제, 연속 전환을 검사한다.
- 원본의 Camera/Collider/NetworkObject/게임 스크립트 부재와 누락 참조를 확인한다.

## 공동 테스트 항목

1. 호스트·참가자가 실제 외줄/인간대포 시작 시 동일한 위치의 공연장을 보고 코스·카메라·판정이 유지된다.
2. 묘기 전환·대기실 복귀 및 늦은 입장에서 배경 중복·잔류·씬 전환 오류가 없다.
3. 이미지와의 배치·분위기 일치 및 실제 PC 프레임 시간을 사람 플레이로 확인한다.
