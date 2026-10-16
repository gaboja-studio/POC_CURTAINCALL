# Handoff

## 지금 상태
- 단계: 구현 3항목(구매·수령·자금 / 운반 한도 / 소품실·물려줄 선반) 1인 확인 완료, 다중 접속·공동 테스트 전. 운반·보관분은 미저장(첫 항목만 커밋 200c779).
- 씬: `Assets/Scenes/Tests/WaitingRoom/ShopStorage/WaitingRoomShopStorage.unity` = 1단계 그레이박스 복사(GUID 유지) + `/ShopStorage`(WaitingRoomShop·PropCarry, DontDestroyOnLoad) + `/ShopStorageViews`(SharedFundsView·FloorPropsView) + ③ Phone·DeliverySlot, ④ HandoverShelf_4Tier에 표시 컴포넌트.
- 막힌 것: 없음. PR: 없음.
## 다음 할 일
1. "저장해줘"(GB_Dark.mat·_ThirdParty 제외) → "검사해줘" → "제출해줘"(integration/waiting-room-base) → 4인 공동 테스트 3개(plan).
## PM이 확인할 것
- 공용 `CameraViewDirector.PrepareView`(210줄) NullReferenceException이 호스트 시작 무렵 반복(상점과 무관, 공용 구역이라 미수정).
- 구역 밖 `Base/Materials/GB_Dark.mat` 색 소수점 churn·`_ThirdParty` 하위 변경: 이 작업과 무관, 커밋하지 않음.
- 기획서 4.3·게임 흐름 5-5의 소지 3/+3과 injury-rules #8(2/1/+2)이 다름 → 2026-10-17 담당자 결정: 부상 기획 기준 유지.
## Verification
- 구역 검사: OK(38 files, 2026-10-17). 1 문서/규칙: 미실행(저장 때 실행).
- 2 컴파일: PASS(verify-unity -Compile, 2026-10-17). 3 테스트: EditMode `CurtainCall.WaitingRoom.ShopStorage.EditModeTests` 27/27. PlayMode 없음.
- 4 실행 로그(AI 1인, LAN 호스트 1인·개발자 모드 임시 켬 → 끔, 공연 2회에 호응 넣어 자금 마련, F 실입력 대신 같은 진입점 Interact/Request* 호출):
  - 구매·수령: 자금 0 주문 거절 → 주문 시 잔액·장부(-10/-20) 감소 → 배송구 앞 바닥 도형 → 수령, 재수령 "이미 다른 사람이 받았습니다".
  - 운반: 2/2에서 수령 거절(배송구에 남음) → 임시 배낭 +2 → 4/4 → F 길게 내려놓기·줍기 → 외팔 시 한도 3, 넘친 1개 발밑 → 사망 중 유지.
  - 보관·선반: 선반 F = 아래 빈 칸부터 올림, 빈손이면 위 칸부터 꺼냄 → 소품실 바닥 보관 → 씬 사본 생성·표시 재활성화해도 상태 유지(사본 자동 제거) → 밤→2일차 아침→탈출 전원 리타이어로 2세대: 선반 #2만 남고 손·배낭·바닥·배송구 비움, 자금 0.
  - 콘솔: 상점·소지 코드 오류 없음.
- 결정 메모: 배낭=상점 임시 품목, 내려놓기=F 길게, 한도 감소=넘친 만큼 발밑, 선반=F 자동(올리기 우선), 바닥 상한 32·배송구 8은 메시지 보호용 임시값, 탈출로 리타이어 소실은 범위 밖(2026-10-17 담당자 확인).
- 5 공동 테스트: 미요청
## 공동 테스트 결과
- 일시·참여자·재현·실제: 미실행 / 예상: plan 참조
