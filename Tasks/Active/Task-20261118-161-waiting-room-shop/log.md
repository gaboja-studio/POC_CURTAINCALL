# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-18
- 구현: 신체 복구 입장 전 추가 예약, 해금 전 공연 기물 목록 제외 + 해금 알림(HUD 프로필 `shopItemsAdded`, 기본 시스템 보상).
- 검사 `check-work.ps1`: 안전성 PASS, 문서/규칙 PASS, 컴파일 ENV_BLOCKED(이 worktree에 연결된 Unity 에디터 없음), 테스트 SKIPPED.
- 대체 컴파일 확인: 같은 커밋(dev 0b8f5e8f)인 원본 저장소의 생성 csproj를 임시 복사해 `dotnet build` — Assembly-CSharp·Editor·ExpertService EditModeTests 오류 0. 임시 파일은 삭제.
- 새 EditMode 테스트 2개(추가 예약 규칙)는 Unity에서 아직 실행하지 않음.
- 다음: 사람 플레이 확인(추가 예약 흐름, 작업실 개방 시 알림·목록 추가).
- 제출 전 origin/dev(e5f772c3) 반영. dev에서 159번을 먼저 써서 Task 번호를 160으로 변경.
- 작업 트리의 에디터 재저장 변경(폰트 SDF·머티리얼·_ThirdParty)은 이 작업과 무관해 커밋하지 않음.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/425 (→ dev, Squash, 지정 팀원 검토 대기)

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-18 (2차: 상점 해금 조건·슬롯 설명·연습실)
- 구현: 첫 습득 해금(shopUnlockedFromStart, 탐색 기물 전부 체크), 공연 기물 4종 설명 기획서 기준 갱신, 대기실 슬롯 설명 토글, 연습실 무소모·그 밖 대기실 사용 불가.
- 작업실·소품실 개방 조건(1차)은 첫 습득 규칙으로 대체.
- 검사 check-work: 안전성 PASS, 문서/규칙 PASS, 컴파일 ENV_BLOCKED, 테스트 SKIPPED.
- 대체 컴파일: 원본 저장소 csproj 임시 복사 + 새 파일 추가로 dotnet build — Assembly-CSharp·Editor·SceneIntegration PlayTests·ExpertService EditModeTests 오류 0. 임시 파일 삭제.
- 영향 테스트 확인: 공연 기물 PlayMode 테스트는 모두 공연 씬(대기실 규칙 영향 없음). 새 규칙용 자동 테스트는 추가하지 않음.
- 새 스크립트 InventorySlotTooltip.cs의 .meta는 에디터 없이 직접 생성.

## 2026-11-18 (3차: 사용키 배지)
- 칸 키는 늘 1·2. 공연 기물을 지금 쓸 수 있을 때만 칸 오른쪽 위 노란 배지로 사용키(Q·E·재지정 키) 표시, 그 밖에는 배지 제거.
- 배지 글자는 자동 크기(18→9) 후에도 넘치면 말줄임. 슬롯 템플릿에 없으면 실행 중 생성(InventorySlotUseKeyBadge).
- dotnet build 대체 컴파일 오류 0(Assembly-CSharp·Editor·SceneIntegration PlayTests). 변경 파일 신규 경고 없음.

## 2026-11-18 (4차: 구조 장갑 대상 인식)
- 원인: 대상이 공중·매달림·대포 비행일 때만 후보, 방향 판정에 높이 차이 포함(위쪽 동료가 각도 밖), 경로 검사가 발밑 줄·다른 플레이어·충돌 안 하는 레이어까지 장애물로 판정.
- 수정: CanBeRescuePulled(죽음·추격자 포획·잠·대포 탑승·그네·트램펄린 들기·상호작용 동작 중·구조 중 제외, 그 밖 모두 허용). 수평 범위(rescueRange)·높이 범위(rescueHeightRange, 신규 6m)를 설정에서 조절, 방향은 수평만.
- 대기실 연습실 밖 사용 시 본인에게만 HUD 안내(HUD 프로필 equipmentPlaceBlocked, 기본 즉시 피드백).
- 기존 PlayMode 단언(땅에 선 동료는 대상 아님)을 새 규칙에 맞게 변경.
- dotnet build 대체 컴파일 오류 0. origin/dev가 fa86538e(PR #426)로 앞서 있어 제출 전 반영 필요.

- 2026-11-18 origin/dev(fa86538e) 반영 중 dev에 160번(shared-apparatus-art)이 생겨 Task 번호를 161로 변경. _ThirdParty를 dev 기록 커밋 1290bdf로 이동(로컬 수정 3건 보존).

## 2026-11-18 (5차: Unity·MPPM 2인 검증, 사용키 표시 변경)
- Unity 6000.3.25f1 이 worktree 에디터(원본 저장소 에디터는 씬 clean 확인 후 종료). verify-unity Compile PASS·Common settings PASS.
- EditMode: ExpertServiceBookTests 27/27(신규 2 포함). 전체 3531 중 실패 4: WaitingRoomTextTests 3·AudienceReactionTests 1. 내 변경이 만든 키 문제(ledger.medical 삼항식, shop.craft-rooms-locked 미사용)는 수정해 해당 키 사라짐. 남은 항목은 변경과 무관한 키/기능.
- PlayMode: PerformanceEquipment 6/6, ShopItemTooltipPlayTests 2/2(설명 형식을 2026-11-02 상점 규칙에 맞춤, 에디터 eval로 PerformanceEquipmentAuthoring.ApplyDescriptions 반영). WaitingRoomHudPlayTests 2건은 테스트의 옛 Start 씬 경로로 SetUp 실패(무관).
- MPPM 실제 2인(임시 시나리오 Main+Player2, LAN 127.0.0.1:17941, 2일차 대기실):
  - 로비 구조 장갑: Blocked, 사용 거절·내구도 5 유지, 안내 HUD는 시도한 호스트에게만.
  - 연습실: 서 있는 동료 인식·당김, 두 화면 위치 일치(-37.5→-38.9), 내구도 5 유지. 6m 낙하 중(y 4.54) 인식·당김, Normal 착지.
  - 첫 습득 해금: 참가자가 주운 스프링 신발·끈끈이 장갑이 두 화면 모두 해금, 알림 두 화면 동일 문구. 상점 지급(Receive)은 해금 안 함.
  - 신체 복구: 예약 2개(100) 후 추가 +1(예약 때 단가 50), 팀 이용 1회 유지, 총 3개 두 화면 일치.
  - 공연 전환은 두 에디터 동시 로딩 중 메인 스레드 10~20초 정지로 실패(환경 부하). 공연장 내구도 소모는 단일 에디터 PlayMode로만 확인.
- 별도 발견(미해결, 변경 무관 추정): 대기실 바닥 기물 66개로 Carry.State가 16KB 상한 초과 오류 반복. Pretendard에 "…" 글자 없음(TMP 말줄임 불가).
- 정리: MPPM 정식 Stop, 임시 시나리오 삭제, Dev 모드 EditorPrefs 원래값(false) 복원. Play Mode 구성 선택값은 내부 API 오류로 확인 못함.
- 사용키 표시 변경(사용자 요청): 별도 배지 삭제 → 칸 키 글자가 1·2(흰색)↔Q·E(노랑)로 페이드·슬라이드 전환(InventorySlotKeyLabel). 긴 키는 크기 축소 후 "..." 생략. Unity 컴파일 PASS, 화면 확인은 사용자 플레이로(사용자 지시: 검증 최소화).
