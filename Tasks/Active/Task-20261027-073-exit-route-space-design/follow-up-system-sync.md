# 탈출로 시스템 동기화 목록

2026-11-02 / Task-073 / feat/exit-route/design-map. 사용자 후속 승인으로 P0 전체와 P1-2/3을 대조·보수했다.
기준 경로: Start→대기실→탈출로. 원본 변경 반영과 호스트/클라이언트 상태 일치를 함께 확인한다.

## 확인 수준

- 최신 UI 정정: 원본 공통에 없던 EscapeBodyStatusHud는 제거했다. 아래의 신체 HUD 보존/11건 회귀는 이전 단계 기록이다. 절단 상태·모델·부상 판정은 유지하고 공용8분 타이머는 독립 Canvas에 보존한다.

- 최신 후속 승인으로 원본 묘기↔탈출로 공통 동작까지 반영했다. 맵별 설정과 공통 규칙의 경계·수정/검사 결과는 [공통 동작 동기화](common-mechanics-sync.md)를 따른다. 아래 표는 직전 UI/시스템 동기화 묶음의 기록이다.

- HumanCannon/Tightrope, PropCarry/ItemCatalog, NetworkPlayer, CampaignRun 연결 코드는 이미 존재한다. 전부 새로 만드는 목록이 아니다.
- 신체 HUD는 NetworkPlayer.LostParts, 탈출 타이머는 공용 타이머 뷰와 호스트 시각을 사용한다. 연결과 실제 전체 동작 검증을 구분한다.
- 아래는 대조·검증 대상이며 전부 불일치/버그로 확정한 것이 아니다. 실제 차이부터 확인한다.

## 이번 승인 범위의 결과

| 대상 | 변경 또는 확인 | 근거·남은 한계 |
|---|---|---|
| P0-1 전환 | 완료→대기 상태 역행과 동일 출발 내 명단 변경 거부. 기존 플레이어·소지품·부상 서비스를 재생성하지 않는 인계 유지 | 명단1~4명 오프라인 회귀, 대기실 표시/인계 코드 대조. 실제 1~4인 이동은 미실행 |
| P0-2 장비 | 쿨타임을 공유 서버 종료 시각으로 전달. 재수신으로 대기시간이 늘지 않도록 보수. 선택 장비의 재사용 대기 초 표시 | PropCarry 복원/ItemCarryBook 회귀. 신발·구조/끈끈이 장갑·벌룬 바지의 원본 효과 진입점 유지. 효과 전체의 실제 다인 조작은 미실행 |
| P0-3 신체 | LostParts→PlayerCondition→기존 패널티/표현과 탈출로 상시 HUD 연결 유지. 별도 부상 상태를 추가하지 않음 | HUD11건/CannonInjury10건과 원격 부상 정책 연결 대조 |
| P0-4 상호작용 | F 안내와 실제 재지정 입력의 차이 보수. 버튼/그네 안내는 기존 호스트 판정과 같은 CanUse/InRange 사용 | SwitchInteraction13건/PoleFeedback2건, 원본 독립 봉 입력 우선순위 유지 |
| P0-5 입력·설정 | 기존 Reader의 바인딩 변경 이벤트/공용 메뉴 InputGate·커서/CameraViewDirector를 재사용. 길게 누르기·점프 안내도 실제 키로 치환 | InputBindingPreferences19건, F↔Space 교환 안내 회귀. 감도는 기존 공용 카메라 프리셋 값을 유지하며 새 설정 항목은 만들지 않음 |
| P1-2 UI·타이머 | 기존 호스트 시작 시각과 480초 제한 유지, 휴식 타이머는 Escape에서 비활성. 최신 정정에 따라 대포/그네 C6만 글리프·전체 문장/긴 키 칸 보수; 장비/일반 알림 폰트·레이아웃 유지 | 탈출 타이머12건/공용 RestTimer14건/HudLayout3건. A 타이머 참조3개 유효, 메뉴 인벤토리/설정 각1개 |
| P1-3 다인 기믹 | 다리 전개량+서버 스냅샷 시각 복제로 늦은 수신·접힘 복원. 문/하부 작업자 동일 revision 충돌 거부, 다리 시각 역행·비정상 양 거부 후에만 재시작 응답 수락 | CooperativeUpgrade29건/Ropes17건/톱31건/추격14건/배선9건. 소실 랜덤·그네/위험 코스의 기존 공유 시각 유지 |

선별 EditMode는 20개 필터의 고유 회귀260/260 통과(동기화255 + 대포/그네 UI 및 기존 알림 보존5). 테스트 증거는 `.harness/local/escape-system-sync/`에 보관했다. 실제 씬 프리뷰 참조 감사에서 누락 스크립트0, 다리3개 모두 체크포인트 연결, Play 중지/dirty scene0. 대포/그네 C6 편집 렌더에서 키·제목·본문9개 잘림0/한글 정상 표시 확인. 오프라인 상태·편집 검증은 실제 네트워크 지연이나 4인 연속 완주 검증이 아니다. 기존 자동 Play/MPPM 중지 지시는 유지한다.

P1-1 공통 동작은 후속 승인에 따라 common-mechanics-sync.md에서 반영했다. 맵별 수치 재튜닝·P1-4 사망/재접속 전반·P1-5 결과/재시작 전반의 실제 다인 검증·P2 아트/소리는 별도 범위로 유지한다. 필요한 공통 신체 상태/점유 해제/재시작 응답 방어·상태 복원은 기존 연결을 보존한다.

## 우선순위별 목록

| 순서 | 대상 | 확인할 내용 | 주 진입점 |
|---|---|---|---|
| P0-1 | 공간 전환·플레이어 인계 | 소유자·절단·소지품·점유 유지, 공용 카메라/입력 중복 연결 방지, 숨긴 대기실 서비스 유지 | EscapeSceneTransfer, EscapeCampaignContent, EscapeWaitingPresentation |
| P0-2 | 소지품·장비 효과 | 대기실 슬롯/장비 유지, 사용 횟수·쿨타임·파괴 알림, 실효과 발생 시1회 소모. 스프링 신발·구조 장갑·끈끈이 장갑·벌룬 바지 개별 확인 | PropCarry.Equipment, ItemCatalog |
| P0-3 | 신체 손상·회복·HUD | 절단 부위/모델·속도/점프/상호작용 패널티·목마/운반 제한·신체 HUD 일치, 전환/재접속/재시작 유지 범위 | NetworkPlayer, PlayerCondition, EscapeBodyStatusHud |
| P0-4 | F 상호작용·안내 | 거리·시선·가림·공중·점유 조건의 UI/강조와 호스트 승인 일치, 버튼/봉/기물/목마의 같은 F 중복 소비 방지 | PlayerInteraction, HumanCannonPoleSystem, EscapeIndependentInteractions, EscapeSwitchInteraction |
| P0-5 | 입력·카메라·설정 | 키 재할당·감도·반전·UI 입력 차단·커서, 지상/외줄/탑승/비행/운반/구조/관전에서 공용 설정 적용 | PlayerInputReader, InputBindingPreferences, CameraViewDirector, CommonSettingsUI |
| P1-1 | 원본 묘기 동작 | 대포 발사/조절·그네 반동/놓기·트램폴린 접촉/운반·외줄 균형/매달림/구조·목마 분리의 최신 코드/세팅/프리팹 반영 | HumanCannonSystem/Pilot/Pole/Trampoline, TightropeCourse/Hang, PiggybackSystem |
| P1-2 | UI·타이머 | 탈출8분/대기실 휴식 타이머 중복 방지, 기물 사용·절단·운반/점유 안내, 안전 착지 표시와 실제 판정의 의미 대조 | EscapeRouteTimer, RestTimerView, EscapeBodyStatusHud, HumanCannonGroundMarkers |
| P1-3 | 탈출 기믹 다인 상태 | 소실 줄·하부1인 예약·컨베이어 그네·스위치/문/다리·전원/배선·전기톱·추격 화염의 공유 시각/상태 일치 | EscapeCooperativeRopes/Locks/Span, EscapeExitSaw, EscapePursuitFire |
| P1-4 | 사망·관전·재접속 | 사망자 점유 해제, 관전 시점/HUD/조작, 접속 종료 시 예약 해제, late join·재접속의 기믹/장비/타이머 복원 | NetworkPlayer, CameraSpectator, NetworkSessionManager, 각 기믹 Network 부분 |
| P1-5 | 성공·실패·재시작 | 탈출문/도착자·제한시간·전원 사망 판정 인계, 소실 줄/점유/스위치/화염/장비/절단의 유지·초기화 규칙 | EscapeCampaignContent, EscapeRouteTimer, CampaignRun |
| P2-1 | 외형·이펙트·소리 | 최신 원본 기물/묘기 프리팹·재질·피격/절단/반동·사용/파괴 알림 반영, 낡은 복제/중복 연출 제거, 음량/음성 공간 전환 확인 | 원본 HumanCannon/Tightrope/WaitingRoom 프리팹, 공용 사운드/음성 설정 |

## 진행 순서·완료 기준

1. P0의 실제 참조/설정/화면을 비교해 확정 차이부터 목록화한다. 이미 같은 부분은 검증 완료로 구분한다.
2. 원본 재사용/탈출로 연결 코드로 해결하고 전용 동작·8분 타이머·동선/기믹을 원본 묘기 값으로 일괄 덮어쓰지 않는다.
3. 묶음별 호스트/클라이언트 상태·횟수·시각과 UI/실제 판정 일치를 확인한다. 실제 다인 검증은 별도 기록한다.
4. 공연 점수·관객 호응·묘기 보상까지 탈출로에 적용할지는 기획 확인 대상으로 남긴다. 자동 편입하지 않는다.
