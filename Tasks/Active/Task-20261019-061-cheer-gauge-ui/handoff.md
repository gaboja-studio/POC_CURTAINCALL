# Handoff
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/200
## 현재
- 2026-10-21 후속 PR: 외줄 추락게이지/기물 슬롯 간격 및 대기실·외줄 안내·공연 사망 배너 Y-260 배치. dev #201(33bfe0e) fast-forward 후 UI 복원, 충돌 없음. 컴파일 PASS, 두 공연 씬 누락스크립트0, 추락 fill 높이15/P1 Y104/외줄 배너 Y-260 확인. Play·자동 테스트 생략(사용자 위임). 안전용 stash 유지.
- 2026-10-21 사용자 후속: Tightrope.unity의 P1 씬 인스턴스만 수정. 추락 트랙 Y-56→13/높이12→23, 빨간 fill Y-60→9/높이4→15(후속 요청으로10에서50% 추가 확대), P1 Y80→104. 원본 프리팹·게임 로직 유지. 에디터 값 확인/씬 저장, Play·컴파일·자동 테스트는 실행하지 않음(사용자 검증 위임).
- 이전 Start·팝업·Canvas는2be6f00 commit/push 완료.
- 로비 정본 HUD/순간이동 수정41bce04 commit/push 완료. 실제UI 누락 점검·대기실 임시UI 정리 완료, 이번 정리 미커밋. 상세 ui-audit.md.
- 게임 시작 순간이동: LobbyRoom이 단순 전환 플래그 종료를 복귀로 판단하던 문제 수정. 실제 공간 복귀에만 재배치.
- 파일·연결·사람 확인: [로비](../../../Docs/Guides/Ui-scene-integration/lobby-ui.md), [Start](../../../Docs/Guides/Ui-scene-integration/start-title.md).
- 기존 폰트2/재질3·ui-workspace.md는 별도 보존. 이번 작업 산출물 아님. 기존 stash 유지.
## Verification
- 이번: 실제10개 씬 누락스크립트0, 컴파일 PASS, 대기실 알림·재등록 중복HUD 회귀1/1 PASS. 전체 실플레이는 미실행.
- 컴파일 PASS, 실제 LAN 로비 PlayMode1/1 PASS(9.49초). 코드 보기/복사·서명·레버 시작·위치 유지 확인.
- 미실행: 원격2~4인, 실제 F/포인터, 다중 화면비, 전체 완주 복귀, Mac/빌드.
- 이전 검증은 각 적용 가이드 유지. 과거 임시 씬 제외, 실제 진행5개 씬만 검증.
- 기존 확장 SpaceLink6개/카메라2개 실패는 이전 기록 유지, 이번 재실행 없음.
## 다음/PM 확인
- 로비 실제 F/눈/복사/ESC·공연 전환 시 HUD 숨김 확인 후 전체 UI 누락 점검.
- 다인 음성/원격 차폐ACK·재접속/지연, 상점 다인·의료 복구·3일밤→4일아침 실제 동선 확인 남음.
- [남은 계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md)과 단계별 가이드의 미실행 항목 유지.
- 후속 hover/외줄 조작 안내 교체 완료. 배치용 컴파일 PASS, 실행 검증은 사용자 요청으로 미실행. Task Integration=dev로 PR 준비, 생성/병합 안 함. 상세 ui-audit.md.

## 최신 사용자 수정: 상황 안내와 C5
- 하단 외줄 안내 삭제 → 상단 상황별 Assets 배너. 공통 메뉴 씬에 Assets C5 슬롯 추가, 실제 대기실·두 공연에 숫자키 선택/선택 UID 동기화/손 표시/선택 기물 내려놓기·선반 연결.
- 기존 소지 한도 유지: 정상2+배낭2,5번째는 잠금. 기물별 미제공 아이콘은delivery, 손3D는 기존 임시 큐브1개.
- Unity 컴파일 PASS, 수정3개 씬 참조/누락스크립트0, diff 공백 PASS. Play·다인·시각 검증 미실행(사용자 위임). ui-audit.md 마지막 절에 확인 항목.
- 이번 변경 미커밋. 기존 폰트/재질/ProjectSettings.asset/ui-workspace.md 변경은 보존했으며 이번 작업 산출물 아님.
- 추가 사용자 수정: 외줄 배너 한글 폰트 교체(문구 누락 문자0), 같은 번호/빈 슬롯으로 집어넣기 및 재선택 꺼내기, 호스트 보관 상태 동기화. 빈손에서 자동 내려놓기/선반 올리기 방지. 실행 검증 미실행.
- 2026-10-21 사용자: 최신 수정 정상 동작 확인, commit/push 및 dev PR 준비 요청. Scope/Fast/Compile PASS(문서 길이 WARN), dirty 씬0, 테스트 반복 생략. PR 초안: Docs/Guides/Ui-scene-integration/dev-pr.md. 저장 스크립트의 add -A 대신 명시 파일 staging으로 별도 변경 보존.

- 2026-10-21 사용자 승인으로 잔여7개 정리. 원본 TTF 참조가 유효한 동적 폰트2개 캐시/재질3개 반올림/runInBackground 테스트 설정은 HEAD 복원. 오래된 미추적 ui-workspace.md 삭제. dev PR 준비 문서 반영, 전체 Play 검증 반복 없음.
