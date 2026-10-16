# Handoff
## 지금 상태
- 단계: PR #136 Squash 병합 완료(`d23d57e`), 통합 브랜치에서 검사·도구 기반 2인 LAN 확인. 사람 공동 테스트 대기, Task submitted 유지.
- 동작하는 것: Canvas 시작 메뉴·고정 3D 로비·명단 F 서명·방장 레버·엔딩 후 같은 방 재준비/재시작·나가기.
- 막힌 것: 사람 조작감·온라인 방 코드/잠금 해제 미검증. 첫 공연은 기존 DummyPerformance로 연결되며 바닥 충돌체가 없어 캐릭터 낙하(실제 공간 연결 별도 Task 필요).
- 저장 상태: 구현 `afae2ca` → 병합 `d23d57e`. 씬·설정·코드 추가 수정 없음. 통합 검증 기록은 현재 브랜치 Git 참조.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/136

## 다음 할 일
1. Start 씬에서 plan의 공동 테스트 3개를 사람이 한 번에 확인. 온라인 코드·대기 재참가/진행 중 거부·설정 저장/빌드 화면 모드 포함.
2. 실제 공연 연결/스폰·바닥 문제를 별도 Task로 배정하고, MPPM 공식 Scenario 또는 별도 PC에서 공동 검증. 병합된 feat·integration에 구현 추가 금지.
3. 2026-10-16 PM "현재 상태로 먼저 병합" 예외 승인으로 미검증·낙하 문제를 명시해 dev 선반영. Task submitted / Integration testing·Active 유지. 공동 결과·후속 이슈 처리 전 integrated/done·브랜치 삭제 금지.

## PM이 확인할 것
- 확인 요청: 시작/로비 화면·조작감·카메라, 온라인·사람 공동 검증. 참가자에게 호스트 종료 이유가 영문으로 표시됨.
- 교훈: Project/Pitfalls의 hub-spawn-before-environment-enable, Engine/Unity/Pitfalls의 mppm-virtual-player-status. 상세 증거는 Integration result.

## Verification
- 구역 검사: 작업 PR PASS / 통합 SKIP(PM integration, 10-16).
- 1 문서/규칙: PASS (10-16), 기존 다른 문서 길이 WARN 5, hard 0.
- 2 컴파일: PASS (병합본 d23d57e, 10-16).
- 3 테스트: PASS (기존 PlayMode 46/46, skipped 0). StartLobby 전용 자동 테스트 없음.
- 4 실행 로그: 부분 확인 — 1인 스모크 + 실제 2개 로컬 런타임 LAN. 인원/서명 동기화·시작 차단·퇴장/재입장·엔딩 로비 복귀·재시작·호스트 종료 확인. 위치 이동/API 진행 단축·합성 F 사용. DummyPerformance 낙하 및 MPPM 내부 오류가 있어 전체 PASS 아님.
- 5 공동 테스트: SKIPPED — 사람 직접 테스트·온라인 미실행. 자동 2인 검증으로 대체하지 않음.
- 종료 상태: Play OFF, Start dirty=False, 보조 에디터 종료, 기존 DevMode ON 복원. 설정 저장·빌드 화면 모드·4인/별도 PC 미검증.

## 공동 테스트 결과
- 일시·참여자: 미실행(사람 참여 없음).
- 재현·예상: plan의 3개 항목. 사람 실제 결과 미측정. 도구 기반 2인 재현/예상/실제는 `Integrations/Active/Integration-start-lobby/result.md`에 별도 기록.
