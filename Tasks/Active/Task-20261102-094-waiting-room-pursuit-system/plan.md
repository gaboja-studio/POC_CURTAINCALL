# Task-20261102-094

- **Title:** 대기실 추격 시스템 M2 데모
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/waiting-room/pursuit-system
- **Base:** dev

## Context
M2 데모용 대기실 추격 시스템. 공용 탐색 제한시간(현재 5분) 만료 후 전시 마네킹이 각성하여 플레이어를 추격하고, 포획된 플레이어를 외팔 상태로 만들어 관전시킨다.
원문: https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/32473089 (2026-11-02 조회, live doc v1).

## 확정 요구사항
- WaitingRoomBase 전체를 별도 Pursuit 테스트 씬으로 복제하여 임시 런 진입점을 연결한다.
- 지정된 남성 4종·여성 6종 마네킹 모델을 사용한다. 제작/파생 모델·애니메이션·머티리얼·프리팹 등 에셋은 Assets/_ThirdParty 안에 보관한다. C#은 Assets/Scripts, 씬은 Assets/Scenes.
- 방별 배치 인원·위치·순찰 경로·담당 범위를 Inspector/Scene 뷰에서 편집 가능하게 한다.
- 전시 움찔 → 각성/대기 → 부자연스러운 걷기 → 기괴한 달리기 → 붙잡아 넘어뜨리기 → 절단. 피해자는 넘어짐·바둥거림 연출.
- 하체는 이동 방향, 상체는 주시 방향을 따르며 상체 좌우 회전은 전방 기준 ±90도.
- 추격 시작 +1분 시야 확대, +2분 벽 투시, +3분 최인접 플레이어 상시 추격. 각 단계 시스템 알림.
- 포획 방에만 조명 점멸. 절단·포획·조명은 다른 플레이어에게도 동일하게 보인다.
- 2026-11-02 사용자 답변: 추격 활성화 후 안전 구역도 추격 가능. 생존자 전원이 공연장/탈출로 집결 구역에 모여 실제 장소 전환할 때만 추격 종료. 잡힌 인원은 집결 인원에서 제외하고 선택된 목적지로 외팔 상태로 함께 이동.
- 1~3일차 전원 포획: 포획 연출 완료 후 다음 공연에 전원 외팔 상태로 이동.
- 4일차: 기존 0.1.0 데모 종료 예외를 바꾸어 아침 탐색·만료 후 추격·탈출을 허용한다. 전원 포획되면 즉시 런 실패(다음 공연으로 이동하지 않음).
- M2 포획 후 구출 없음. 탐색 방별 기본 1체·방 간 이동 허용, 시야 10→20m, 정상 최대 달리기 속도×1.2. 각 단계 시작 시간·탐지·속도를 WaitingRoomSettings/Pursuit에서 개별 조절한다.

## 계획
1. 원문·기존 CampaignRun/RunState, ExplorationTimerZones, RestTimeoutCatch, NetworkPlayer, 집결/장소 전환/관전 API 조사.
2. Task·결정 기록, Unity에서 원본 전체 복제, 테스트 씬만 런 진입점/Build Settings에 연결. 원본 씬 GUID와 내용 보존.
3. 만료 시 즉시 공연 전환/일괄 포획하는 기존 동작을 추격 시작으로 대체. 호스트 판정과 늦은 접속자 상태 복구까지 연결.
4. 방/스폰/순찰 저작 컴포넌트, 경로 탐색·시야·대상 선택·추격 단계 및 알림 구현.
5. 모델 리그 확인 후 필요한 애니메이션 제작. 상하체 방향·포획 동기화·방 조명·절단·관전·다음 장소 부활 연결.
6. 복제 씬에 초기 배치 및 빠른 재현용 개발 도구. 규칙 테스트·Unity 컴파일·오프라인 플레이·가능하면 다인 검증 후 check-work 1회.

## 착수 결과
- 테스트 씬: Assets/Scenes/Tests/WaitingRoom/Pursuit/WaitingRoomPursuit.unity. Unity AssetDatabase로 전체 복제하고 열었으며 원본 SHA256 일치·새 GUID 확인.
- BaseGameSettings의 waitingRoomScene/explorationScene을 WaitingRoomPursuit로 임시 연결. 음성 씬 목록과 Build Settings에도 추가·저장.
- 지정 마네킹 10종 모두 스킨/뼈대 없는 단일 Transform 정적 메시. 원본 FBX를 보존하고 _ThirdParty 안에 리깅 파생 모델/모션을 제작해야 한다.

## 미정·주의
- 절단은 기존 왼다리→오른다리→왼팔 순서·오른팔만 남는 공통 정의를 재사용한다. 이미 외팔이면 추가 절단 없이 포획 연출 후 관전/목적지 외팔 복귀.
- 다인 환경의 수신 화면·늦은 접속·동시 포획은 별도 실기 확인 대상으로 기록한다.
- 공통 Campaign/Survival/장소 전환/설정 연결 수정이 예상된다. 다른 작업자의 원본 대기실 씬과 기존 에셋은 보존한다.
- 담당은 로컬 Git 계정 @MoHoDu 기준. commit/push는 별도 요청 때만.

- [완료한 후속 구현 계획](History/002-completed-followup-plan.md)

## 플레이 중 배치 창 경량화·플레이어 추적
- Play에서는 3D Camera/RenderTexture/팔레트/SerializedObject 편집 UI를 해제하고 2D 방·경로·마커 및 추격 연결선으로 관찰한다. 10Hz 갱신, 층 기준 캐시, 숨긴 탭은 갱신 중단.
- 플레이어 선택/추적과 자유 모드를 제공. 추적은 위치·층·중심 방을 따라가고 드래그/방·층 선택 시 자유 모드로 전환. 관찰 대상 생성/제거·씬 이동·Play 종료 때 자원 정리.
- 실제 호스트 창의 자원/전환/이동 회귀, 기존 편집 층 검사, UI 확인 및 check-work.
