# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 구현 1차 완료(코드·세팅·빌드 목록·자동 테스트). SpaceLink 시작 씬 배치 전이라 사람 Play 확인 전
- 동작하는 것:
  - 공통 세팅 "장소 전환"(`BaseGameSettings.SpaceTransition`): 통째 교체(기본)/추가 로드, 장소별 씬 이름(기본 더미 4개 + 시작 씬 SpaceLink)
  - `SpaceLinkDirector`: 판정 화면(호스트·오프라인 런)이 `CampaignRun.StepChanged`로 장소 결정 → 접속 중 NGO `NetworkSceneManager`, 오프라인 Unity `SceneManager`로 전환. 공연 → 공연장, 쉬는 시간·밤 → 대기실, 대기실에서 나가면(B안 타이머 시작 감지 또는 버튼) 백스테이지, 탈출 → 탈출로, 도입·엔딩 → 시작 씬
  - 추가 로드: 시작 씬 유지, 공간 씬만 붙이고 이전 공간 씬 닫음, 새 공간 씬을 활성 씬으로(각 화면). 시작 씬 카메라는 공간 씬이 붙어 있는 동안 끔(`SpaceLinkHub`)
  - `SpaceLinkTestUI`(상단 가운데, 접기 가능): 방식·지금 장소·런 상태·공동 자금·내 잃은 팔다리, 런 시작·도입 끝·백스테이지로 나가기 버튼, 전환 기록
  - 빌드 목록에 SpaceLink + 더미 4개 추가(기존 2개 순서·활성 유지)
- 막힌 것: `SpaceLink.unity`에 `SpaceLinkHub`(+ 접속용 `NetworkSessionTestUI`) 배치 필요. meta `AI Setup Allowed: None`이라 PM 승인 또는 사람 배치 대기
- PR: 없음

## 다음 할 일

1. SpaceLink.unity에 빈 오브젝트 "SpaceLink Hub" + `SpaceLinkHub` + `NetworkSessionTestUI` 배치·저장(사람 또는 PM 승인 후 AI)
2. 사람 Play: 오프라인 런으로 통째 교체·추가 로드 각각 한 바퀴(공동 테스트 1·2·4), 2명 접속(3)
3. "검사해줘" → "저장해줘" → "제출해줘"

## PM이 확인할 것

- 확인 요청: SpaceLink.unity 배치를 AI에 맡길지(AI Setup Allowed) 사람 배치할지
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음(배정된 BaseGameSettings.cs·.asset, EditorBuildSettings.asset만 수정)
- 알려 둘 한계: 통째 교체는 모두 함께 이동(사람마다 다른 공간 불가). 접속 중 "백스테이지로 나가기"는 호스트만. 플레이어 캐릭터를 새 공간 시작 위치로 옮기는 일은 하지 않음(더미 공간은 UI 위주, NetworkPlayer는 읽기 전용). 실제 공간 연결 때 필요

## Verification

- 구역 검사: PASS (10-16, 19 files)
- 1 문서/규칙: PASS (10-16, Context WARN 9 기존)
- 2 컴파일: PASS (10-16)
- 3 테스트: PASS — PlayMode 41/41(SpaceLink 3 포함: 통째 교체 한 바퀴·추가 로드 시작 씬 유지·장소 대응), EditMode 55/55 (10-16)
- 4 실행 로그: 미실행(SpaceLink 씬 배치 전)
- 5 공동 테스트: 미실행
