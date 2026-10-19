# Plan

## Context

2026-10-19 PM 지시로 이 Task를 UI 리소스 확보·조합·테스트로 확장한다. C1 디자인/기능을 보존하며 제공 버튼 미리보기의 확인·취소·설정·일반 프리팹과 독립 확인 씬을 추가한다. 폴더/Task ID는 이력 보존을 위해 유지한다. 기존 보상/네트워크·게임 메뉴는 변경하지 않는다.

## 공용 버튼 추가

- private CurtainCallButton과 CommonButtonBuilder를 재사용한다. builder의 실제 입력/출력 경로·실행 진입점·연결 저장만 수정하고 제공 코드는 공개 복제하지 않는다.
- CommonButtons 전용 Resources 프리팹4개/SemiBold TMP, Tests 확인 씬 생성. Canvas1920×1080/Match0.5, 192×56, 취소X/설정톱니, 9-slice/PPU200/Body alpha0.92를 준수한다.
- 프리팹 인스턴스를 씬에 가로 배치하고 4상태/Focus/클릭/Submit/비활성 무반응을 확인한다. 전체 키트 삭제는 이번 제작에서 하지 않는다.
- 구역·문서·컴파일 및 실제 Overlay/입력 경로 검증. 합성 입력과 사람 확인을 구분한다. 기존 C1 해시를 보존한다.

## Approach

1. 전용 worktree, 로컬 Task/브랜치에서만 작업. 커밋·push·PR 없음.
2. private PNG 8장 참조, 정확한 레이어/색/크기/상단 중앙 배치, Pretendard TMP.
3. 프로젝트 View와 읽기 전용 Binder. 실제 long 합계와 Cap100/150/200, 1~4인. 사망/이탈 시 명부·색 유지.
4. 순수 표시 데이터 변환과 자동 테스트, 프리팹/전용 테스트씬 생성. 기존 공연씬은 수정하지 않는다.

## Files

- 수정 예정: setup.md 작업 구역의 신규 코드/프리팹/폰트/테스트씬, Task 문서.
- 읽기 전용 참고: private C1 키트, PerformanceRewards.State/Changed, RewardSnapshot.Segments/Rules.

## Risks

- 원본 키트는 미추적이므로 다른 PC 재현에는 private 저장이 필요하다. 현재 작업은 원본을 보존한 별도 로컬 복사본을 참조한다.
- 시작 doctor FAIL: private C1 미추적 폴더. 사전 보고 완료, PM이 로컬 복사본의 import/.meta 생성을 승인했다. 원격 저장 없음.
- Unity CLI beta.6에는 문서의 --until-ready가 없다. 실제 help로 명령 확인.
- 추락 판정은 제공되지 않으므로 점수 감소를 추락으로 간주하지 않는다.

## Auto Verification

- 구역 검사: verify-scope
- 1 문서/규칙: verify-fast
- 2 컴파일: verify-unity -Compile
- 3 테스트: EditMode 변환/예외, PlayMode 표시/바인딩
- 4 실행 로그: 전용 에디터 Game View/Console/스크린샷

## 공동 테스트 항목

1. 1920×1080에서 승인 미리보기와 같은 외형·상단 중앙 배치·합계155 확인.
2. 실제 환호/야유와 100 초과 점수도 중앙 합계/원호 비율이 일치.
3. 사망 시 해당 단원 구간만 제거, 다른 단원색 유지. 가능하면 호스트/참가자 동일 표시 확인.
