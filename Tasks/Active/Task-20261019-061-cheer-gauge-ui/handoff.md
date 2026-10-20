# Handoff
## 현재
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
