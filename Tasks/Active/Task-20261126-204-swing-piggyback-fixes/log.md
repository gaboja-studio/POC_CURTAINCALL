# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-26 조사
- doctor: third-party dirty·task-id 중복은 기존 상태. `unity-editor 미설치`는 CLI 1.0.1 출력 차이 오탐(6000.3.25f1 설치 확인).
- 봉 배치 측정(바닥 기준 몸 중심 도달): 탈출로 36개는 모두 서거나 점프해 잡는 배치, 인간대포 불 스위치 봉은 바닥 7.3m(대포 전용), 연습실 봉은 발판 기준 일반 봉. 목마 전용 배치는 외줄 공중그네 높은 첫 봉(공연 5m, 리허설 4.5m)뿐.
- 높은 첫 봉 재현: 공통 반경 3m라 리허설(4.5m)은 혼자 점프(몸 중심 1.55m → 거리 2.95m), 공연은 2단 점프(2.35m → 2.65m)로 잡혔다. 기존 PlayMode 검사 2개가 "첫 봉도 혼자 잡는다"를 단언하고 있었다 → 새 규칙으로 갱신.
- 그네 프레임 드랍(Tightrope 씬 Play, 에디터 프로파일러): 잡는 프레임 64ms+55ms, 7MB+2MB 할당. 흔드는 동안 카메라 가림 메시(봉 파이프 2,272삼각형·조명 빔) 90프레임 중 85~87회 재생성, 카메라 151ms/28MB 스파이크 1회.
- 침대 해제 원인: 연습실 자유 연습 외줄이 켜져 `InWaitingRoom` 거짓 → 해제 검사 건너뜀.
- 4인 맨 위 부상 원인: 목마 위에서 낙하 추적이 매 프레임 초기화되고 해제 순간(발 4.5m)+점프 1.2m부터 낙하 계산 → 5m 부상 기준 초과.
- 로비: 보이지 않는 경계 높이 3.5m라 상자 위 목마+점프로 넘을 수 있었다.

## 수정 결과
- 개선 후 같은 측정: 흔드는 동안 카메라 할당 1KB/프레임, 잡는 프레임 66ms·4.9MB(일회성: 첫날 안내 카드 8ms·Mono JIT 6ms·에디터 전용 머티리얼 훅). 공통 잔여 비용(그네와 무관): 카메라 갱신 약 11ms/프레임, UI 매 프레임 할당(상호작용 UI 약 1.1MB, PerformanceSceneBinding·CommonInventoryUI 각 368KB) → 별도 최적화 후보.
- 실제 Play에서 높은 첫 봉 아래 혼자 있을 때 안내판 문구 표시 확인.

## 검사
- 새 EditMode: HighPoleAndSwingDirectionTests 8/8, PiggybackDismountAndBedTests 10/10, CameraMeshCollisionCacheTests 2/2 PASS.
- 공통 회귀(CommonProgressionRegression) 1166/1178, CommonRulesRegression 13/13. 실패 12건은 변경 stash 기준선에서도 동일 실패(탈출로 아바타 인계 5, 늦은 봉 이벤트 Release 인자 수 3, 리프트 재부착 1, 탑승 감사 lift 1, 트램펄린 중복 응답 1, 리허설 대포 아트 1).
- CooperativeUpgradeTests 45/47: 실패 2건은 `flying.Begin` 인자 수(기존).
- PlayMode: CommonPoles_ 3/6 PASS(실패 3건 기준선 동일), W6_Rehearsal_TrapezeUsesPerformanceDevicesAndRules PASS, W6_Learning_LowPole 2건 실패는 기준선 동일.
- 처음 `IsSeatInReach`에 인자를 추가해 반사 호출 검사 3곳이 깨졌다 → 원래 4인자로 되돌려 해결.
- 다른 PC 2~4인 실제 플레이는 미실행(사람 확인 필요).

- 2026-11-26 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 증빙 공유(2026-11-26)
- Confluence 50659329 "M3 테스트 결과 및 피드백" 1·2번 '수정 결과'에 영상 7·캡처 6 첨부(v7).
- 촬영: 에디터 Play + 합성 동료(SpawnWithOwnership 740~742), 외줄 위는 균형 값만 고정. 동료 침대 눕기는 restingBed 값을 호스트에서 주입.
- 재확인: 뒤를 보고 잡은 채 W로 놓으면 뒤쪽으로 도약(마지막 입력 방향 규칙). 문서에 '확인 필요'로 적음.
- 로비를 직접 열면 SceneCurtainTransition 입력 잠금·이동 정지로 점프 재현 불가 → 벽 범위 표시 이미지로 대체, 실제 플레이 확인 남음.
- 공중그네 첫 봉 '매달린 동료 잡기' 표시는 보조 설명 줄 없이 나옴(왼쪽 첫날 안내 카드가 설명).
- 사용자 요청: 혼자일 때 높은 그네 안내를 제목 '높은 그네' / 내용 '높은 그네는 목마를 통해 잡을 수 있어요'로 줄임(PoleSettings 기본값·에셋). Confluence v8에서 1-0a 캡처·1-1 영상만 교체.
- 제출 전 origin/dev 확인: 새 커밋 없음(합칠 것 없음). Play 부산물 에셋 3개·_ThirdParty는 커밋 제외.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/488 (dev, Squash). new-pr.ps1은 제외한 부산물 에셋 때문에 멈춰 같은 제목·본문 형식으로 gh 직접 등록.

- 2026-11-26 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
