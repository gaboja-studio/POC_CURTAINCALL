# UI·대포 시점 후속 확인 (2026-10-17)

## 요청·수정
- 사용자 요청: 줄타기 UI를 다시 보이게 하고 인간 대포 시점이 기존대로 유지되는지 확인한다.
- CampaignPerformanceEntry에서 TightropeDemoUI GameObject 비활성화와 TightropeRunDebug 비활성화를 제거했다. 기존 HUD·균형 게이지·표시 설정을 보존하며, TightropeRun의 외부 관리 재시작/재도전 거절은 유지한다.
- 원본 Tightrope UI·씬과 HumanCannon 코드·씬·카메라 설정은 수정하지 않는다. enhancement 브랜치 갱신·기능 커밋/push/PR은 요청 범위 밖이다.

## 읽기 전용 조사
- 현재 체크아웃의 HumanCannonPilot.Camera.cs는 일반 상태 FirstPerson(priority 50), 탑승 FreeThirdPerson(200), 비행 FirstPerson(200), 트램폴린 운반 FreeThirdPerson(100)을 요청한다.
- 현재 저장 HumanCannon 씬에는 초반 플랫폼 CameraViewZone/preset override가 없다. 로컬 플레이어가 없는 접속 전에는 카메라의 기본 ThirdPerson이 선택될 수 있으므로 접속 전후를 구분해 비교한다.
- 연결부는 카메라 preset을 직접 변경하지 않는다. 상태 정상화·출발 위치 배치의 간접 영향은 실제 실행에서 별도로 확인한다.
- PM 교훈 제안: 씬 연결 때 시연 UI라는 이름만 보고 전체 GameObject를 숨기면 진행 HUD·균형 표시까지 사라진다. 표시/입력 역할을 확인하고 연결·독립 화면을 각각 확인해야 한다. Harness 기록 승격은 PM 통합 시 수행한다.

## 자동 검사
- UI 표시 보존·HUD/게이지 활성 및 재시작 보호 회귀를 추가했다. 대포 단독 및 Single/Additive 연결 경로의 출발·탑승·발사 시점도 검사했다. 탑승/발사는 위치·입력 프레임 합성이며 실제 F 키 조작은 아니다.
- UI 복원 후 `check-work.ps1 -Mode PlayMode`: 구역·문서·컴파일 PASS, PlayMode 72/72 PASS(105.98초, 실패·생략 0). 이전 70/70 결과와 구분한다.

## 실제 화면·시점 비교
- 줄타기 단독: 저장 proto 씬을 직접 열고 Play → 공개 API로 1인 LAN/게임 시작. 접속 창과 진행 HUD·상단 도구·하단 조작 안내 표시를 화면으로 확인했다.
- 줄타기 연결: Start의 LAN/방 만들기를 포인터로 클릭하고 로비 명단·레버 앞 위치 합성 및 F 입력 주입으로 진입했다. 중간 진행은 공개 API를 썼다. 진행 HUD·상단 도구·하단 조작 안내 표시를 화면으로 확인했다.
- 줄 위 이동을 위한 W 입력 주입 시도는 추락·NoSurvivors 정산으로 끝났다. 사람 정상 조작/완주 성공이 아니며 균형 게이지의 줄 위 실제 표시도 미확인이다. 줄 밖에서 게이지가 숨는 것은 기존 BalanceGaugeView 정책이다.
- 대포 단독: 저장 씬 직접 실행의 접속 전 ThirdPerson, 공개 API로 LAN 접속 후 Normal/FirstPerson(DefaultFirstPerson(Clone))을 관측했다. 출발 위치 (-2.250, 4.050, 0.000), 카메라 회전 (0,0,0), 카메라 높이 y=5.400이었다.
- 대포 연결: 같은 출발 위치·카메라 회전·FirstPerson 프리셋을 관측하고 화면도 확인했다. 카메라 높이 y=4.860은 안정 후에도 유지됐다. 두 다리 손실·IsCrawling=true·BodyHeightOverride/BodyHeight=0.9를 관측했다. 기존 CameraViewDirector.ApplyPose의 몸높이×0.9 규칙으로 4.050+0.9×0.9=4.860이며, 같은 신체 상태의 프레이밍 비교는 아니다. 부상이나 카메라 규칙을 바꾸지 않았다.
- 현재 공용 저장본에서는 접속 후 일반 1인칭 → 탑승 3인칭 → 비행 1인칭 정책이 단독/연결 모두 보존됐다. 사용자가 보던 enhancement 최신본의 초반 플랫폼 3인칭과 같다고 단정하지 않는다. 해당 브랜치는 조회·갱신하지 않았다.
- 실행 종료 후 원본 씬 dirty=false 확인, 저장 없이 Start 씬으로 복귀했다. 최종 Console 오류/경고 0, compilationFailed=false. 읽기 eval의 잘못된 namespace 진단 2회는 수정 후 조회 성공했으며 프로젝트 컴파일 실패가 아니다.

## 남은 확인
- 실제 2인·사람 조작 완주·줄 위 균형 게이지 화면·단독 R 키 확인은 미실행/미확인이다. 자동 검사나 API·위치·입력 합성으로 대체하지 않는다.
- 기능 변경은 미커밋·미push이며 PR/병합·enhancement 전달은 하지 않았다.
