# Handoff
## 현재
- Task-061을 사용자 지시로 UI 씬 통합으로 전환. 현재 환경/브랜치 유지, 제작 기록은 History/Before-scene-integration 보존.
- 공통 UI 씬에 C1/C2/C3, 외줄 공간에 P1. 두 공연 씬 UI 루트 정리, Assets 정본 프리팹만 재사용.
- C2는 불의 안전 경계까지 표시만 하며 기존 성공/실패·게임 규칙은 변경하지 않는다.
- 사용자 Start→Tightrope HUD 확인 완료. 시작/로비/대기실 UI는 아직 미적용.
- origin/dev3a2bc88 수신 merge a474bac 완료. 상세: [수신 검증](dev-sync-verification.md).
- 2026-10-20 사용자 commit/push 승인. 기존 로컬 정책·자동 에셋 변경·환경 메모는 별도 보존하고 UI 변경만 저장한다.
## Verification
- 구역 검사: PASS (수신 merge 이후 이번 변경만 검사, verify-scope -Base HEAD)
- 문서/규칙: PASS (외부 인계 원문을 Docs/References로 이동, 이름·내용 보존)
- 개별 Unity 컴파일: PASS(up_to_date, Unity6000.3.25f1)
- PlayMode: PASS9/9(병합 후 실행). .harness/local/dev-sync-hud-tests.json.
- 실제 외줄 호스트: 공통 C1 0/100·C2 8:19, 공간 P1 alpha1. 추락/관전 숨김 확인.
- 실제 오프라인 외줄→인간대포 Single 전환: 공간+공통2씬/binding1개, 공통 씬 P1 없음.
- 인간대포 호스트 C1/벽 타이머는 분리 전 검증. 분리 후/병합 후 인간대포 호스트 재실행은 미실행.
- 실제 다인·재접속·A/D 입력·불 접촉·다른 해상도·전체 회귀 미실행. 앞선 사용자 확인으로 이를 대신하지 않는다.
- 새 import가 재생성하는 private 서드파티 자료는 공개 커밋에 포함하지 않는다.
## 다음 작업
- [계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md): Start/Lobby 정본·API 대조 → 공통 알림/설정과 공간 화면 분리 → WaitingRoomBase → 전체 흐름/다인 회귀.
- 최신 가이드: Docs/Guides/Ui-scene-integration/. 원문: Docs/References/curtaincall_unity_handoff_v1/. Packages ZIP은 로컬 보관·Git 제외.
- C3 정본은2~4인 지원,1인 —. 사망 단원 마지막 관측 위치 유지, 늦은 참가의 추락 이력 복원 API 없음.
- 기본 save-work는 이미 수신한 dev까지 범위 위반으로 계산한다. 이번 저장은 수신 ancestry·실제 변경 기준 scope·fast·compile을 각각 확인한 후 경로를 명시해 stage한다. 검사나 hook을 비활성화하지 않는다.
