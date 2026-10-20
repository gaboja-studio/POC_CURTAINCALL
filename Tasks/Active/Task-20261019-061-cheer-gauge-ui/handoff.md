# Handoff
## 현재
- 후속: [씬 뷰 미리보기·구형 UI 정리](../../../Docs/Guides/Ui-scene-integration/layout-editing.md). 사용자 게이지 Y80 보존. 새 편집 메뉴, 중복 UI 제거, 독립 연습 도구 분리.
- Task061, feat/ui-scene-integration, 동일 checkout. 사용자 지시대로 origin/dev864ed37을 먼저 pull/병합(f0ceb0b)하고 새 공연 UI를 연결했다.
- 사용자 저장 요청: 리소스 병합 e6cc578·dev 병합 f0ceb0b와 이번 UI 고도화/편집 미리보기/구형 UI 정리를 feat/ui-scene-integration에 커밋·push한다. ui-workspace.md는 기존 로컬 메모로 제외한다.
- 인간대포 충돌은 최신 dev 공간 씬을 채택한 뒤 Editor에서 기존 HUD/호응/안내를 복원했다. private62250960과 원본 프리팹·PNG·9-slice·폰트 유지.
- 공통 C11 관전, 인간대포 H1 표지42개, C8 동료 사망, C12 대성공/정산/잔액/자동전환 초를 연결했다. [수정 파일·화면·데이터·확인 방법](../../../Docs/Guides/Ui-scene-integration/performance-upgrade.md).
- C10/C14/C16은 테스트된 정본 프리팹 미수신. C13 메뉴와 결과의 추가 필드는 후속이다. 기존 검은 전환 유지.
- 사용자 기존 ui-workspace.md 보존. 복구용 public54a6c49/privateaa1c499/dev수신전b00d1b8 stash 보관.
## Verification
- 최종 컴파일 PASS, UI PlayMode21/21 PASS. [검증 근거](upgrade-verification.md).
- 두 공연 호스트 합성에서 관전 대상2→가상 D→대상3→대상 없음→부활 숨김 확인. 실제 원격 클라이언트 검증은 아니다.
- 인간대포 표지36개 활성/6개 비활성/중복0, 한글 fallback 확인. 호응80 성공 결과와 잔액0→80, 대기실 전환 후 입력 해제 확인.
- 후속 정리 후 UI21/21·외줄 탑승/경고 게이지1/1 PASS. 최종 Editor Tightrope/Play 정지/P1 편집 미리보기, Y80 유지. 상세 실패 이력은 검증 문서.
- 구역 검사: FAIL — 구역 검사 실패 (10-20)
- 저장 DryRun도 기존 병합 이력/구역 중첩으로 FAIL371. 사용자 예외 승인 및 이번 저장 지시에 따라 실제 UI 변경만 별도 검사·선택 저장한다. 실패를 PASS로 바꾸지 않는다.
- 1 문서/규칙: 종합 SKIPPED(구역 검사 실패), 개별 Fast OK(기존 경고9)
- 2 컴파일: 종합 SKIPPED(구역 검사 실패), 개별 Compile PASS
- 3 테스트: 종합 SKIPPED(구역 검사 실패), 개별 PlayMode21/21 PASS
- 실제2~4클라이언트, 실제 완주/불 접촉, 재접속, 다른 해상도/빌드/전체 스위트는 미실행.
## 다음
- 두 공연 실제 다인 관전·동료 사망·정산 회귀 확인. Task 구역 중첩 조정.
- C13 ESC 입력 흐름, 결과 추가 정보와 정본 수신 후 C10/C14/C16.
- 이후 Start→Lobby→WaitingRoomBase. [순서](../../../Docs/Guides/Ui-scene-integration/next-plan.md).
