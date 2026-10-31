# M2 후속 개발 계획 (2026-10-30 초안)

사용자 작업 목록 4건을 현재 코드와 교차 확인한 결과와 단계별 순서. Confluence 원문(M2 변경점 27721807, 기물 27525121)과 대조해 아래 "원문 대조" 절에 반영했다(2026-10-30).

## 교차 확인 결과

### 1. 상점 기물
- 카탈로그 7종의 가격·새제품 내구도는 로컬 결정과 같다(`Assets/Resources/Settings/GameSettings/Items/ItemCatalog.asset`, `ItemCatalog.cs:87-111`). UV등은 장비·무제한(10-30 변경) 반영.
- **실제 판매는 탐색용 3종(머리핀·말뚝뽑개·UV등)뿐.** `WaitingRoomShop.cs:104`가 `ItemUse.Exploration`만 올린다(10-29 사용자 지시 주석: 공연용은 효과 연결 뒤 연다). 공연용 4종(구조장갑·끈끈이장갑·벌룬바지·스프링신발)은 빠져 있다. — 원문 확인
- 구매는 밤 정비만(`WaitingRoomShop.cs:178`), 공동 자금 차감, 배송구 새제품 지급.
- 정리 대상: 에셋 없을 때의 임시 품목 3개(`WaitingRoomShop.cs:32-37`), 발견 머리핀 내구도 0 → 새제품 값 5로 대체(`ItemCatalog.cs:70`). — 원문 확인

### 2. 의료(신체 복구) 서비스
- 상점 서비스 탭에 "신체 복구 1개/2개/3개"가 **각각 별도 항목**으로 나온다(`WaitingRoomShopUI.cs:126-141`, 항목당 버튼2 템플릿 제약). 수량 선택 UI가 아니다.
- 상한 min(잃은 부위, 3), 비용 = 개수 × (100 + 25 × 팀 이용 횟수), 쉬는 시간·밤 정비 이용(`ExpertService.cs:75-85`, `ExpertServiceBook.cs:75-107`).
- 구매 → 예약(Booked) → S08 승강기 앞 F → 입장·입력 잠금 → 절단 역순 복구 → 퇴장(`ExpertService.cs:194-224`, `ExpertRoomView.cs:162-167`).
- 어긋남: 복구 부위 직접 선택 없음(역순 고정), 거절 문구 일부가 옛 "커튼/전문가 대기실"(`ExpertServiceBook.cs:180`, `ExpertRoomView.cs:8-13`), 환불 없음(임시 규칙), 옛 IMGUI(`ExpertServicePhonePanel`, `ExpertServiceTestUI`) 잔존.

### 3. 인간대포·트램펄린 발견 해금
- 공연 ID는 문자열 `"Tightrope"`, `"HumanCannon"` 두 개뿐. **"Trampoline" 공연은 없다.** 트램펄린은 인간대포 코스 안 소품(`HumanCannonTrampoline.cs`). — 원문 확인
- 공연 종류 해금 개념 없음(단계 해금만 `RunState.cs:194-221`). 스케줄표 선택지는 고정 `ScheduleBoard.cs:19`, `ScheduleBook.cs:43,52`(readonly), UI는 순환만 하고 잠금 건너뛰기 없음(`WaitingRoomScheduleUI.cs:229-241`). 단계 버튼의 비활성·툴팁 패턴은 재사용 가능(`:41-47, :206-224`).
- 1일차 "줄타기→인간대포 고정"(`schedule-board.md`)이 발견 해금과 충돌. `DefaultTrick` 대체값도 잠긴 공연이 될 수 있다.
- 발견 판정 재사용: `CommonInteractionUI.InViewAndRange`·`IsVisible`, `ExplorationReach.From`, `ExplorationState.RequestUse`(ToolId 비우면 도구 없이, 팀 공유). 단 `ExplorationState`는 세대 교체 때 지워진다.
- 연습실에 `PracticeLanes/HumanCannon`, `PracticeTrampoline`이 이미 아트로 놓여 있다(생성기 `WaitingRoomLayoutA.PracticeRing40.cs:144-166`). 스크립트·NetworkObject 없음, 연습 기능 미연결.
- 해금의 세대 계승 개념 없음. — 원문 확인

### 4. UV 손전등 동기화
- `UvFlashlight`는 씬에 하나(`StructureAExploration`)이고 켜짐·조준을 로컬 입력과 `Camera.main`으로만 판단(`UvFlashlight.cs:34-74`). 켜짐·방향은 네트워크로 가지 않는다 → 다른 화면에 빛이 없다.
- 전달되는 것: 미발견 낙서의 `RequestUse`, 발견 결과(`UvMark.Found`·전기음), UV 소지(`PropCarry.Held`).
- 관전자 화면의 낙서 드러남도 로컬 전용(`UvMark.cs:56-94`).
- 재사용 틀: `NetworkPlayer` Owner 쓰기 NetworkVariable(`restingBed` 등, `NetworkPlayer.cs:54-69, 443-461`) + 표시 전용 `NetworkBedPose`(`NetworkPlayer.cs:359`).

## 원문 대조 (2026-10-30)
- 상점: 원문은 새 제품 7종 모두 밤 정비 판매(구조장갑100/5·끈끈이100/3·벌룬바지125/4·스프링신발175/3·머리핀50/5·말뚝뽑개75/4·UV등75/3). 현재 탐색 3종만 판매 → 어긋남. UV등은 원문 3회, 로컬은 10-30 결정으로 장비·무제한 → 원문 갱신 필요.
- 신체 복구: 1~3개, 절단 역순, 부위당 100 + 팀 이용마다 +25(여러 개여도 1번만 오름), 런 초기화 시 100, 쉬는 시간·밤 정비 언제든. 코드 계산과 일치. 수량 선택 UI·흐름만 남음.
- 트램펄린과 대포: **한 쌍의 배치 기물이 '인간 대포' 공연 하나를 해금**한다(트램펄린 별도 공연 아님). 플레이어 한 명 이상 시야에 들어오면 즉시 해금, 런마다 1회, 런 초기화 시 다시 필요(세대 교체=런 초기화로 사라짐). 발견 즉시 정해진 공간으로 이동(원문 "로비 특정 지역", 사용자 지시 "연습실") → 연습실로 구현.
- 1일차: 원문은 오전 줄타기부터. 로컬 schedule-board.md는 오후 인간대포 고정, clue-overview.md는 전부 줄타기 → 발견 해금과의 관계 결정 필요.

## 사용자 결정 필요 (4건)
1. 의료 흐름: (A) 구매 즉시 치료 연출 / (B) 기존대로 승강기 이용 / (C) B + 승강기 안내(가이드).
2. 공연용 4종: 원문대로 지금 판매에 열지(효과 미연결이면 사도 효과 없음), 효과 연결 후 열지.
3. 1일차 오후 공연: 미발견이면 줄타기로 대체 / 1일차만 예외로 인간대포 허용.
4. 다른 사람 UV 빛에 숨은 낙서도 드러나게 할지(발견 판정·내구도는 비춘 본인 기준).

## 단계별 개발 순서
1. **UV 빛 동기화** — `NetworkPlayer`에 UV 켜짐·조준 방향(Owner 쓰기, 변화·임계각 때만), 원격 표시 컴포넌트가 다른 플레이어 Spot을 그림. 원격은 `RequestUse` 금지. 결정 4 반영. 검사: UV PlayMode + 2인.
2. **상점 기물 정리** — 결정 2 반영, `ItemUse.Exploration` 필터·임시 품목 정리, 원문 7종 가격·내구도 대조 테스트. 검사: 상점 PlayMode.
3. **의료 서비스** — 서비스 탭 한 항목에서 1~min(잃은 부위,3) 수량 선택(합계 비용 표시), 결정 1 흐름, 문구 "승강기" 통일, 옛 IMGUI 정리. 검사: `WaitingRoomMedicalPlayTests`·상점.
4. **공연 해금 상태** — 호스트 권한 팀 공유 해금 목록(NetworkVariable, 늦은 접속 수신), 런 초기화 시 리셋. `HumanCannon` 하나만.
5. **발견 오브제 배치·판정** — 구조 A 맵에 대포+트램펄린 오브제 배치, 각 클라가 화면 안·거리·가림(`InViewAndRange`·`IsVisible`)으로 보면 호스트에 발견 요청 → 해금. 둘 중 하나만 봐도 해금.
6. **스케줄표 잠금** — 선택지를 해금 목록으로 거름(잠긴 공연 건너뜀·잠금 표시·`ScheduleBook` 거절 사유), `DefaultTrick` 대체, 결정 3으로 1일차 처리.
7. **연습실 이동 표시** — 발견 시 맵 원위치 오브제 숨김 + 연습실 대포/트램펄린 표시, 모든 화면·늦은 접속 동일.
8. **전체 검사** — check-work, 관련 PlayMode, 2인 확인 기록. 사람 플레이 확인은 별도.
