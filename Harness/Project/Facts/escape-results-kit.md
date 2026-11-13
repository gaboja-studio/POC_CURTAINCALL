---
summary: 성공/실패 결과창·실제 플레이어 Cinemachine·성공 로비/실패 새 런 계약
status: active
updated: 2026-11-14
source: human 성공 아트/실패 재시작 요청, Task-20261112-120, Task-20261114-131, Unity 6000.3.25f1 LAN 1/2인 검사
---

# 탈출 결과창 키트

## 언제 읽나
탈출 엔딩 UI 키트를 설치하거나 기존 EscapeEndingPresentation에 결과 데이터를 연결하기 전.

## 산출물과 계약
- Task-120 `kit-guide.md`가 설치·조립·API 안내다. private 로컬 키트 `Assets/_ThirdParty/curtaincall_escape_results_v1/`에 ZIP/UnityPackage와 무시 폴더 `Unity~/` 원본을 보관한다. 아직 private 저장소 커밋/원격 배포는 하지 않았다.
- 설치 결과 `Assets/Resources/Prefabs/UIs/EscapeResultsKit/`의 Canvas/PlayerRow/StatisticRow 3개와 `Assets/Scenes/Tests/EscapeResultsKit/EscapeResultsPreview.unity`.
- PNG 33장→Sprite 36개. 기존 UI 20장+실제 기물 8장 원본 바이트 재사용, 신규 기하4장+초상아틀라스1장(4 Sprite). 원본 importer/GUID는 수정하지 않는다.
- Canvas1920×1080/Match0.5, 패널 (40,60)/800×960, SpritePPU200. PNG별 border/출처는 manifest, pivot은 Unity importer/meta에 기록한다. 탭 selected PNG는 바탕 교체용이 아니라 독립 밑줄이다.
- TMP 문구/수치, 플레이어 1~4명, 기물 2칸, 통계 6행. SetPlayers/SetStatistics/SetReturnAllowed/SetVisible로 게임이 자료·권한을 공급한다.
- ReturnRequested는 의도 이벤트만 발생한다. 자동 숨김·로비 이동 없음. 전역 커서/입력·성공 집계·보상·씬 전환은 게임이 소유한다.

## 실게임 연결
- 2026-11-14 변경: 성공은 기존 로비 복귀, 실패 A(Deadline/EscapeAllRetired)는 `RestartAfterFailure`로 세대+1/1일차 아침 쉬는 시간. 공연 해금·클리어 단계는 초기화하며 본 가이드/이수 리허설은 유지한다. 미열람/미이수는 계속 제공한다.
- 실패 확인은 `다음 런 시작`, 목적지 WaitingRoom의 전환 차폐가 끝나면 기존 Warning 시스템 UI로 빨간 `탈출 실패` 안내를 표시한다. 실패 시 세션 Playing을 유지하며 전원 신체와 조작을 복구한다.
- 성공 배경은 `Resources/Prefabs/Endings/SuccessEndingArt`를 기존 달리기 경로에 추가한다. GameSettings의 낮 하늘/거리 안개 색을 분리하고 서커스·갈대·지면 및 원본 4모델 파생 재질을 적용한다. 원본 팔레트/변형/혈흔 표시 옵션을 보존하며 복귀 때 원래 재질을 복구한다.
- `escape_route.unity`의 EscapeEndingPresentation에 EscapeEndingResults/Cinematic을 연결했다. Canvas 루트를 인스턴스화하고 자식 EscapeResultsPanel의 View를 찾는다. 결과 sorting600, 공유 전환 커튼2000.
- 호스트가 도착 명부/마지막 관측 기물 최대2칸을 동결해 기존 ShopChannel로 전달한다. 실제 모델 순서 rabbit/bear/pierrot/girl, 슬롯 색/ItemVisualAssets 아이콘을 사용한다. 같은 세대의 재동기화는 탭을 초기화하지 않는다.
- PerformanceRewards의 EscapeRunStatistics는 현재 세대의 모든 확정 공연을 누적한다. 최근10개 History와 독립이며 미시도는 —, 동률 개인 최고는 첫 기록 유지. 세대/새 런 초기화, 기존 RewardState 메시지 공유.
- 실제 소유자 PlayerMover에 연출 입력을 공급한다. 일반 입력은 InputGate로 차단하며 기존 부상 속도·애니메이션·NetworkTransform은 유지한다. 느린 생존자 속도에 맞춰 대형을 유지한다.
- EscapeCampaignFrame 아래 연출 공간은 원래 경로에서 local z500 떨어져 있다. 120m 바닥3개 재배치, CinemachineFollow/RotationComposer, FOV48·정면 낮은 카메라·밤 안개·달빛·서커스 원경. 원본 플레이어를 사용하고 복제 모델/영상은 없다.
- 6초 후 결과창, 버튼 전에는 계속 달린다. 호스트 버튼이 기존 SpaceLink 전환을 해제한다. 로비 환경 활성화 뒤 ServerRestoreEveryone으로 Arrived/Retired를 복구하고 배치한다. 세션은 Ended를 유지하며 레버 F 이후 Waiting.

## 검사와 한계
- 개인 최고 호응 기록이 없으면 원형 초상화 전체를 숨기고 —를 가운데 표시한다. 원형 마스크는 테두리 안쪽, 토큰 행은 구분선 사이 중앙. 실제 2인 LAN 확인에서 부상자는 탭에 기본 초상화, 갈대밭에 부상 외형이 나타났다. 선두 입력을 감속해 추적 프레임 안의 대형을 유지한다.
- Dev F9 후반 이동 지점은 상단 `탈출로 체크포인트`에 표시한다. F8 `공연 클리어 이력 유지`는 공간 이동 전 외줄/인간 대포 최고 클리어 단계만 다음 테스트 런에 전달한다. 선택하지 않으면 초기화한다.
- F9 `마지막 문 열기 (호스트)`는 Dev 모드/활성 탈출로/준비된 호스트에서만 실행한다. 출구 필수 배선을 완료하고 기존 ShareCut/Changed로 장치·클라이언트에 공유한다. 재시작은 기존 초기화를 사용한다. `갈대밭 · 엔딩 도착`으로 이동하면 결과창까지 확인 가능하다.
- Dev F9에서 후반 이동6지점과 출구 개방 후 갈대밭 도착 지점을 제공한다. F8 직접 탈출 시작도 같은 출구/도착 영역으로 엔딩을 판정하되 전원 실패 시 기존 출발점 재시도를 유지한다. 문 상태는 여러 장치 중 마지막 생성된 `EscapeExitSaw.Current`가 아니라 `EscapeCampaignContent.ExitOpen`을 참조한다.
- F9 외줄/대포1~3단계 바로가기와 F8 공연 난이도는 이번 테스트 공연에만 적용한다. 클리어·해금 기록을 만들거나 GameSettings를 바꾸지 않는다. 시작 지점28개 검사와 실제 호스트6조합·후반6지점·갈대밭 Ending 확인은 Task-120 `cheats-review.md`.
- 독립 Unity에서 컴파일/프리팹 생성/61개 TMP 필드·Sprite 참조 확인, 두 탭1920×1080 렌더 확인. Play Mode raycast+합성 pointer click19개 검사 통과. 사람 입력/NGO 연결 검사는 아님.
- 검증 에디터 SearchDatabase 초기 인덱싱에 엔진 내부 예외가 있어 전체 Console 오류0으로 기록하지 않는다.
- 실제 일반 에디터 LAN 호스트 Start→Lobby→Escape→성공 경계→결과 버튼→Lobby 확인. W 0.5초에1.55m, F 레버로 Waiting 복구. 통계3/엔딩4 테스트 통과, 씬 missing script0. 상세는 Task-120 integration-review.
- Task-131에서 실제 LAN 1인/2인 성공·실패 전환부터 결과 탭과 목적지까지 MP4 네 편을 저장했다. 2인 성공은 토끼/부상 곰, 실패 후 양쪽 Day1/Morning/정상 신체/조작 복구를 확인했다. 영상은 개발 시작점으로 공연을 건너뛰었고 통계는 실제 미시도 값이다.
- 전 구간 수동 완주/3~4인/모든 부상 조합은 미검증. 기존 Task-120의 전체 보상 기대값 불일치와 문서 검사 실패는 과거 기록을 참조한다. 배치 모드에서는 C16 커튼이 전환을 거부하므로 복귀 검사는 일반 에디터를 쓴다.
