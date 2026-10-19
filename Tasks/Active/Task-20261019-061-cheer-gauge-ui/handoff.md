# Handoff
## 지금 상태
- UI 리소스 확보·조합·테스트 Task working. 전용 `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, `feat/ui-resources` → `integration/ui-resources`. Task/통합 폴더 ID 유지.
- 아이콘까지 공개8cda879 저장. PM 추가 저장 요청 후 공연·백스테이지/로비 키트 private main38442263 push 완료, 공개 결과·공유 폰트 현재 상태·하네스·gitlink는 이번 문서 포함 save-work 실행. 삭제·공개 PR/병합·실게임 연결 없음.
- 공연 HUD C2/C3/P1·기존 C1 동시 확인 씬 완성. P1은 위험/주황/안전/주황/위험5구간·경계4개,10인자 API/구8인자 호환, 외부 합산 위험 비율 표시만 제공한다.
- 공연 정본 `Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab`, 씬 `Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity`. 상세·과거 검증은 [performance-hud-verification.md](performance-hud-verification.md), 사용법 `Harness/Project/Facts/performance-hud.md`.
- 백스테이지 W1/C4/C5/W6·로비 L1 조립 완료. 정본 `Assets/Resources/Prefabs/UIs/BackstageHud/`의 BackstageHud/LobbyHud/InventorySlotTemplate3종, 씬 `Assets/Scenes/Tests/BackstageHud/BackstageHud.unity`.
- 백스테이지 사용법 `Harness/Project/Facts/backstage-hud.md`, 전수 [backstage-hud-inventory.csv](backstage-hud-inventory.csv), 검사·실패·미결 [backstage-hud-verification.md](backstage-hud-verification.md).
- 원본88실파일/PNG45 보존, 표시 PNG32=신규14+재사용18. 최신 SHA/GUID 갱신·재사용18 정본 PNG 일치. private 무관 tracked220파일/4886삽입/931삭제 유지, 미추적 원본 보존.
- baseline95항목 중93동일/공유 동적 TMP2개 추가 SHA 차이. CheerGauge Bold/CommonButtons SemiBold는 시작부터 dirty, 원인 미확정. baseline 해시만 있으므로 임의 git 복구·폰트 재작성 금지, PM 확인 필요.
- 신규 강화 PlayMode7/7 PASS. 확인11버튼 Mouse/Enter·긴 값7→10→7·로비 가림/공개 복사·Controls 재활성 단일 구독·글리프/텍스트 전체 숨김 회귀 통과. 최초 씬 로드 시점 실패는 테스트 yield 보정 후 재통과.
- 실제1920×1080/1600×1000 각12상태24캡처 목시 확인. PNG32 시트crop·알파 정상, Sprite32/플랫폼160조합 PASS(override0).
- 최종 의존성74/누락0/Missing Script0/글리프0/활성 전체숨김0, EventSystem1, raycaster는 LobbyHud/Controls만. 전용 Static Medium/Bold2종 사용.
- 정상 설정 Play 연속2회 초기화 PASS. reload enabled=False/options=None, both OFF는 ProjectSettings 변경 금지로 미실행.
- 기존 회귀 Performance9/9·Icons3/3·Panels3/3·Cheer3/3 PASS. Icons 최초 외부 Unity Connect 오류, Panels 최초 SVGImporter 정점 한도 예외, Cheer 최초 시간 초과는 상세 기록에 보존.
- Buttons 전체2건 FAIL(글리프 검사/후속 클릭), 빈 호스트 재현. 입력 단독1/1 PASS. 실패 테스트 정리 누락에 따른 후속 오염 가능성은 미확정이며 공유 폰트 원인/복구와 함께 미결이다.
- 기존 C1·버튼·패널·아이콘 정본은 재생성하지 않음. 이전 상세: [verification.md](verification.md), [buttons-verification.md](buttons-verification.md), [panels-verification.md](panels-verification.md), [icons-verification.md](icons-verification.md).
## 다음 할 일
1. 사용자 백스테이지/로비 최종 외형 확인 대기. 공연 HUD 신규20종·아이콘 신규22종 그림 확인도 대기. 합성 검사와 사람 승인은 구분한다.
2. PM 확인 후 공유 폰트2개 baseline 차이·Buttons 글리프 실패 별도 해결. 기존 dirty/무관 private 변경·미추적 unity_only 보존.
3. 삭제·Git 저장·실게임 연결·제출·병합은 별도 요청. 제공120/100ms 애니메이션 미구현, C1 구형 Builder 재실행 금지. 사람 공동/네트워크·테스트 씬 처리·개발 배지 처리 미결.
## Verification
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: PASS (EditMode, 798/798, 10-20)
- 위4항목은 백스테이지 최종 check-work 재실행(exit0). 신규 강화 PlayMode7/7 PASS와 별개로 Buttons 전체 회귀 실패는 미결이다.
- 종료 Play stopped/BackstageHud 씬 clean, 화면1920×1080. 최종 문서 정리 후 fast 재검사 결과는 백스테이지 검증 문서에 기록한다.
- doctor FAIL: 시작 시 보고한 보존할 private 변경. 무관 변경 미저장·미폐기. 전체 PlayMode·사람 공동·네트워크 검증 미실행.
