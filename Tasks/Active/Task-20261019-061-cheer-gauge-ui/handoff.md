# Handoff
## 현재
- Task-061은 사용자 지시로 UI 씬 통합으로 전환, 동일 checkout 유지. 이전 제작 기록은 History/Before-scene-integration.
- 공통 씬 C1/C2/C3, 외줄 공간 P1. 사용자 Start→Tightrope 확인 완료. C2는 표시만, 게임 판정 보존.
- origin/dev3a2bc88 merge a474bac, 이전 HUD 작업 push9db2069 완료. [수신 검증](dev-sync-verification.md).
- 다음 순서를 HumanCannon으로 변경: 공통 C7/C15 정본 인스턴스, 공간 어댑터/구간 표시 적용. 사용자 요청으로 이번 추가분을 현재 feature 브랜치에 commit/push한다.
- 씬2개 저장 완료, 현재 에디터 HumanCannon 열린 상태·Play 정지·dirty0·runInBackground 원래 false 복원.
## Verification
- 통합 check-work: FAIL(scope279, 이전 수신 dev까지 비교). 후속 단계는 스크립트 내 SKIPPED. 검사/hook 우회 없음.
- 저장 직전 별도 로컬 변경을 stash에 보존 후 개별 scope -Base HEAD: PASS18개. 기본 save-work 미리보기는 이전 dev까지 포함해 FAIL279이므로 실제 delta의 scope·fast·compile을 각각 검사하고 경로를 명시해 저장한다.
- 개별 문서/규칙 PASS(기존 경고9개), 컴파일 PASS(up_to_date), PlayMode PASS12/12. [상세](human-cannon-verification.md).
- 인간대포 호스트 합성 상태: C1 호응10, C2 2:25 감소, 공통 씬1개, P1없음. 대포 접근/탑승·운반·입력 차단/해제·Fallen 숨김 확인.
- 1920×1080 접근/운반 화면 확인, 구간/안내 글자 누락 없음. 실제 물리 키 입력·봉 플레이·전체 코스·불 접촉 원인·다인·재접속·다른 해상도는 미실행.
- C1 동적 폰트 캐시가 재생성돼 저장에서 제외한다. 기존 TightropeCourse/정책3개/ui-workspace와 함께 보관 후 복원, private219건도 보존. 원본 폰트/프리팹 변경은 이번 커밋에 포함하지 않는다.
## 다음 작업
- [인간대포 가이드](../../../Docs/Guides/Ui-scene-integration/human-cannon.md)의 사용자 조작 확인 후 Start/Lobby → WaitingRoomBase 적용.
- C3 정본2~4인/1인 — 유지. 인간대포 불 마커·구간별 그래픽 트랙, C11/C12/C13 새 정본 화면, 최초1회 튜토리얼 저장은 미적용.
- 최신 가이드 Docs/Guides/Ui-scene-integration/. 원문 Docs/References/curtaincall_unity_handoff_v1/. Packages ZIP 사용 금지.
## PM이 확인할 것
- 교훈 제안(record-knowledge): TMP_FontAsset 런타임 복사본은 OnDestroy가 공유 atlas/material도 지우려 한다. 빌린 참조를 해제 후 복사본만 파괴해야 한다. 회귀 테스트12개에 unload 검증 포함.
