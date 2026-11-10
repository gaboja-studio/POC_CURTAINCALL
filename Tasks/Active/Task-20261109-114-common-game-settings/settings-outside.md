# GameSettings 밖에서 관리하는 항목

아래는 기능별 전체 분류다. 세부 필드·소스 위치·초기값은 [필드 색인](Inventory/index.md)에 있다. 위치·외형·기술 설정도 포함하되 일괄 이관 대상으로 보지 않는다.

| 번호 | 대상 | 밖에서 관리하는 내용 | 대표 소스 |
|---|---|---|---|
| 1 | 봉 성능 | 탑승 인원·간격, 매달림 길이, 잡기 거리, 유지 시간, 예고 시간, 가속·각도·각속도·감쇠, 도약 상승·수평 배율·상한, 공중 조절 | HumanCannonPole |
| 2 | 봉 이용 조건 | 시선 방향 사용, 지상 점프에서 잡기 허용, 독립 상호작용 | HumanCannonPoleSystem |
| 3 | 이동하는 봉 | 이동 속도, 경로 길이·반경·위상, 역방향, 벽 진입·하역 거리 | HumanCannonPoleLoop |
| 4 | 봉 연결 톱 | 몸 중심 판정, 맵 횡단 여부·기준 위치 | HumanCannonPoleSaw |
| 5 | 봉 관문 | 열림 폭·정지·열림 시간, 낙하 중력·시차, 접촉 여유, 회전·흔들림 | HumanCannonSawGate |
| 6 | 대포 발사 | 탑승 거리, 전방·상승·공중 조절 속도, 포신 연출 사용 | HumanCannonStation |
| 7 | 대포 시스템 | 비행 바닥 높이·좌표계, 전용 시점 사용·프리셋 | HumanCannonSystem |
| 8 | 대포 부상 | 한쪽 다리 지상·공중 배율, 양쪽 다리 공중 배율, 외팔 상호작용 지연 | HumanCannonInjurySettings / System.Injuries |
| 9 | 트램펄린 | 반동·운반 속도, 잡기 거리, 운반자 필요 여부, 반동면 형태·범위, 운반 높이, 재시작 위치 초기화 | HumanCannonTrampoline |
| 10 | 대포 이동 발판 | 이동 시간, 탑승 크기, 문 열림·빈 발판 초기화 시간, 과적 함정·차단 높이 | HumanCannonTransport |
| 11 | 대포 경로 | 탑승·복귀 지연, 낙사 높이, 피난처 대포 범위 | HumanCannonRouteSystem |
| 12 | 대포 일반 위험물 | 톱·불 종류, 이동 주기·위상·경로·회전, 접촉 크기·반경, 불 절단 여부, 작성된 개수 사용 | HumanCannonHazard / HazardCourse |
| 13 | 대포 마지막 톱 | 예고·이동 시간, 수평 속도·폭·반경, 세로 톱 반경·레인·왕복 횟수 | HumanCannonFinalSaws |
| 14 | 대포 추격 화염 | 출현·예고 시간, 속도·폭·높이, 접촉 즉사, 난이도별 속도·대사 | HumanCannonFireWall |
| 15 | 바람 | 영역·방향, 지상·공중 적용, 시각 효과. 바람 세기 공식은 코드에 있음 | HumanCannonWindZone / CannonDifficulty |
| 16 | 외줄 씬 판정 | sceneShape 전체, 직접 배치 줄, 독립 줄·양방향 이동 사용, 목마 하차 플랫폼 | TightropeCourse / SceneRopes |
| 17 | 외줄↔대포 전환 | 대포 봉으로 이탈 허용과 이탈 거리, 적용할 조작 규칙 연결 | CourseControlSwitcher |
| 18 | 일반 이동 초기 상태 | 시작 조작 규칙, 도착점·코스 방향, 다른 플레이어 위 옆줄 착지 차단 | PlayerController / PlayerMover / SceneControlScheme |
| 19 | 몸 상태·겉모습 | 시작 부상 정책·손실 부위·균형 활성화, 모델 크기 맞춤·앉기 축소, 래그돌 기본 질량, 봉 손 위치 보정 | BodyDamageEffects / PlayerCondition / PlayerBalance / PlayerModelSlot / PlayerRagdoll |
| 20 | 탈출 제한 시간 | 시작 구역, 제한 시간 480초 기본값, 표시 연결 | EscapeRouteTimer |
| 21 | 탈출 추락 보호 | 추락 결과 억제, 외줄 균형 추락 허용, 캠페인 보호 분기 | EscapeDevelopmentSession |
| 22 | 낙사 구역 | 구역 범위, 낙사 높이, 좌표계 | EscapeFallVoid |
| 23 | 탈출 협동 잠금 | 체크포인트, 조작 거리, 스위치 연결 | EscapeCooperativeLocks |
| 24 | 탈출 협동 외줄 | 예고·보임·숨김 시간, 버튼 거리, 작업자·낙하 구역·반환 위치, 트램펄린 연결 | EscapeCooperativeRopes |
| 25 | 탈출 협동 다리 | 전개 시간·접힘 각도·통과 판정 연결 | EscapeCooperativeSpan |
| 26 | 탈출 운반톱 | 잡기·운반 속도·높이, 절단 범위·축·시간, 인원 비례, 출구 문 상승·속도, 복구 위치 | EscapeExitSaw |
| 27 | 탈출 발사톱 | 발사 간격·속도·수명, 접촉 반경·두께, 반동 거리 | EscapeExitSaw.Projectiles |
| 28 | 절단 케이블 | 출구 필수 여부, 케이블 반경·경로, 끊어짐 시간·벌어짐·처짐, 효과 | EscapeCuttableCable |
| 29 | 탈출 추격 화염 | 시작 지연·단계 간격·예고·분출 시간, 안전 출구 좌표, 효과 배율 | EscapePursuitFire |
| 30 | 탈출 기계·회수 연출 | 컨베이어 속도·절단 위치, 호이스트 이동·주기, 축 기계 낙하·승강, 연출 재생 속도 | EscapeConveyorProcession / RecoveryHoist / ShaftMachine / FacilityMotion |
| 31 | 탈출 진입·도착·엔딩 | 집결·차단 구역, 전환 배치, 접근 시점, 공개 지연·페이드·엔딩 시간 | EscapeEntrance / SceneArrival / CampaignContent / EndingPresentation |
| 32 | 치료 서비스 | 치료 작업 시간·퇴장 시간, 도착·상호작용 거리, 커튼·빛 연출 | ExpertService / ExpertRoomView |
| 33 | 부수는 벽 | 필요한 도구·횟수, 도달 거리, 열린 뒤 공개 대상, 파편·외곽선 | BreakableWall |
| 34 | 탐색 문·퍼즐 | 필요 대상·도구·타격 횟수·협동 시간·거리, 암호 조건, 열쇠 자리, 보상·공개 대상 | ExplorationGate / StructureA*Route |
| 35 | 도구 대상·단서 | 상호작용 거리, 종류·문 열림·보상, 단서 확대 크기·거리·회전 속도 | ToolTarget / ClueInspectable |
| 36 | UV 탐색 | 빛 거리·각도·세기, 조준 제한, 판정·요청 간격, 표시 거리·유지 시간 | UvFlashlight / UvMark |
| 37 | 탐색 발견·공개 | 발견 조건·아이템·알림 시간, 소품 습득 거리, 문 슬라이드 시간 | FoundItemSpot / PerformancePropDiscovery / PerformancePropPickup / ExplorationSlide |
| 38 | 탐색 타이머 표시 | 타이머 진행 구역, 방송·문구 노출 시간, 종소리 음량·상태색 | ExplorationTimerZones / Announcer / View |
| 39 | 기물 보관·전달 | 바닥 수용량·내려놓는 거리, 상점 수령칸 수, 선반·바닥·전화기 거리, 배달 정렬·간격·낙하 시간 | PropCarry / WaitingRoomShop / ShopDeliverySlot / FloorPropsView / HandoverShelfView / ShopPhone |
| 40 | 밤 정비·일정 | 침대 거리, 문 이동·점멸, 스케줄 선택 목록, 게시판 접근·시점 우선순위 | NightBed / NightDoorLock / ScheduleBoard / ScheduleBoardView |
| 41 | 카메라 공통 에셋 | 기본 시점·1인칭·가림, 프리셋 거리·높이·시야각·감도·전환 시간 | CameraViewSettings / CameraViewPreset |
| 42 | 카메라 개별 적용 | 씬 카메라 설정 참조, 영역별 프리셋·우선순위·이탈 여유, 로비 추적·경계·완화, 관전 어둡기 | CameraViewDirector / CameraViewZone / LobbyCamera / CameraSpectator |
| 43 | 공통 조작 체계 | 기본·씬별 조작 전략과 플레이어 입력 에셋, UI 입력 차단 | ControlSettings / SceneControlScheme / PlayerInputReader / UIInputBlocker |
| 44 | 개인 옵션 | 음량·BGM·효과음, 해상도·전체화면·FPS, 근처 캐릭터 숨김·피 표시, 키 재지정 | ClientSettings / CommonSettingsUI 계열 |
| 45 | 상호작용 안내 | 일반·대기실별 강조 거리·외곽선 두께, 색상·표시 기준점 | CommonInteractionUI |
| 46 | 행동·인벤토리 피드백 | 내구도 색·소모 섬광·지속·확대, 사용·파손 연출, 균형 위험 점멸·색 | InventorySlotDurabilityBar / InventorySlotUseEffect / BalanceRiskBlink / BalanceZoneColors |
| 47 | HUD·상점·결과 UI | 알림·재화 강조 시간, 상점 응답 제한·스크롤 안내, 월드 라벨 진입·이탈 거리, 결과 표시 연결 | CommonWaitingRoomUI / WaitingRoomShopUI / ShopDescriptionScroll / WaitingRoomWorldLabels / ResultsPanelView |
| 48 | 텍스트·아이콘·외형 | 상점 문구 에셋, 대기실·튜토리얼 TXT, 공통 아이콘, 플레이어 외형, 외줄 아트 프로필 | ShopUiTexts / ItemTexts / CommonIconCatalog / PlayerAppearanceCatalog / TightropeArtProfile |
| 49 | 시각 효과·환경 | 착지 마커 범위, 외곽선 거리·두께, 화염 잔상, 거울 해상도·거리·갱신, 조명 거리 제한 | HumanCannonGroundMarkers / HumanCannonOutline / TightropeOutline / FireView / PlanarMirror / EscapeWorkLightDistance |
| 50 | 개발·미리보기 | 검토 카메라 속도·감도·키, 개발 라벨, 테스트 HUD·키·배율, UI 미리보기 | EscapeInspectionCamera / ReviewLabels / LevelPreviewCamera / *Debug / *Preview / *TestUI |

## 별도 에셋의 정확한 위치

- 전체 26개 자체 스크립트 에셋은 [에셋 CSV](Inventory/settings-assets.csv)에 있다. 폴더 밖 15개에는 카메라 프리셋, 조작 규칙, 아트·아이콘·문구, 에디터 정적 배칭 기록이 포함된다.
- `Assets/Resources/Input/PlayerControls.inputactions`의 13개 바인딩은 [입력 CSV](Inventory/input-bindings.csv)에 있다.
- 카메라·입력·개인 옵션·아트 자산이 밖에 있는 것 자체는 오류가 아니다. 같은 기능의 중복 값과 씬별 우회 적용을 우선 확인한다.
- 장면 참조만 있는 컴포넌트와 UI 미리보기의 개별 필드도 원본 필드 CSV에 보존했다.
