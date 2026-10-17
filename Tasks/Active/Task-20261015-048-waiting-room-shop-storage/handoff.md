# Handoff

## 지금 상태
- 단계: 구현 3항목 + 시각화(배송 낙하·이름표·든 기물/배낭 표시)·장부 분리·공연 중 차단 — AI·담당자 1인 확인 완료, 다중 접속·4인 공동 테스트 전.
- 씬: `WaitingRoomShopStorage.unity` = 1단계 그레이박스(`WaitingRoomGreybox`가 실제 대기실, WaitingRoom·대기실은 참고용) + `/ShopStorage`(상태, DontDestroyOnLoad) + `/ShopStorageViews`(잔액·바닥·든 기물 표시) + `/ShopStorageTestPanels`(접속·런 진행·상점 테스트) + ③ Phone·DeliverySlot, ④ HandoverShelf_4Tier.
- 막힌 것: 없음. 사람 확인 절차: `log.md` 2026-10-17 "절차 갱신".
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/151 (→ integration/waiting-room-base, 2026-10-17 제출)
## 다음 할 일
1. PM: PR #151 검토·Squash 병합, 아래 "PM이 확인할 것" 처리.
2. 병합 뒤 4인 공동 테스트 3개(plan) → 결과를 아래 "공동 테스트 결과"에 기록.
## PM이 확인할 것
- 공용 `CameraViewDirector.PrepareView`(210줄) NullReferenceException이 호스트 시작 무렵 반복(상점과 무관, 공용 구역이라 미수정).
- 공용 `SpaceLinkPlayTests` 2개 실패: 대기실 장소 WaitingRoomBase(#123)와 테스트 기대값(DummyWaitingRoom) 불일치 — 테스트 기대값 갱신 또는 설정 확인 필요(공용 구역).
- 구역 밖 `Base/Materials/GB_Dark.mat` 색 소수점 churn·`_ThirdParty` 하위 변경: 이 작업과 무관, 커밋하지 않음.
- 기획서 4.3·게임 흐름 5-5의 소지 3/+3과 injury-rules #8(2/1/+2)이 다름 → 2026-10-17 담당자 결정: 부상 기획 기준 유지.
## Verification
- 구역 검사: PASS (10-17)
- 1 문서/규칙: PASS (10-17)
- 2 컴파일: PASS (10-17)
- 3 테스트: FAIL — 테스트 오류 (10-17). PlayMode 46개 중 44 통과·2 실패 = 공용 `SpaceLinkPlayTests`(Single_FollowsPhasesAndKeepsRunState, Additive_KeepsHubAndSwapsSpaceScenes) — 1단계 #123의 대기실 장소(WaitingRoomBase)와 테스트 기대값(DummyWaitingRoom) 불일치, **이 Task와 무관한 기존 실패**(2026-10-17 담당자 결정: PR 본문에 적고 PM에게 알림). 이 Task EditMode 27/27.
- 4 실행 로그(AI 1인, LAN 호스트 1인·개발자 모드 임시 켬 → 끔, 공연 2회에 호응 넣어 자금 마련, F 실입력 대신 같은 진입점 Interact/Request* 호출):
  - 구매·수령: 자금 0 주문 거절 → 주문 시 잔액·장부(-10/-20) 감소 → 배송구 앞 바닥 도형 → 수령, 재수령 "이미 다른 사람이 받았습니다".
  - 운반: 2/2에서 수령 거절(배송구에 남음) → 임시 배낭 +2 → 4/4 → F 길게 내려놓기·줍기 → 외팔 시 한도 3, 넘친 1개 발밑 → 사망 중 유지.
  - 보관·선반: 선반 F = 아래 빈 칸부터 올림, 빈손이면 위 칸부터 꺼냄 → 소품실 바닥 보관 → 씬 사본 생성·표시 재활성화해도 상태 유지(사본 자동 제거) → 밤→2일차 아침→탈출 전원 리타이어로 2세대: 선반 #2만 남고 손·배낭·바닥·배송구 비움, 자금 0.
  - 시각화(10-17, 실제 키보드 장치 이벤트): [1인 테스트 시작]으로 캐릭터·자금 100 → W 이동 → F 수화기 창 → 주문 시 배송구에서 낙하·이름표 → F 수령 시 캐릭터 앞 상자, 걸으면 따라옴 → F 길게 내려놓기 → 장부는 수화기 창에서만 → 공연 중 주문·선반 거절. 개발자 모드 Play 종료 시 복원.
  - 사람 확인(10-17, 담당자 @gyumin9048-ctrl 1인): log.md 갱신 절차로 동작 확인 — "동작 확인했어"(세부 항목별 기록 없음).
  - 콘솔: 상점·소지 코드 오류 없음.
- 결정 메모: 배낭=상점 임시 품목, 내려놓기=F 길게, 한도 감소=넘친 만큼 발밑, 선반=F 자동(올리기 우선), 바닥 상한 32·배송구 8은 메시지 보호용 임시값, 탈출로 리타이어 소실은 범위 밖(2026-10-17 담당자 확인).
- 5 공동 테스트: 미요청
## 공동 테스트 결과
- 일시·참여자·재현·실제: 미실행 / 예상: plan 참조
