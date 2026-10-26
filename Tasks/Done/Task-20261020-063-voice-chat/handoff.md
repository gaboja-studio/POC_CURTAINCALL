# Handoff

## 지금 상태
- 단계: done. #186 Squash `f6c5499e` → #187 dev `3a36545d` 병합 완료. 후속 #190(`7241c1f2`)으로 LAN에서도 음성 기본 켜짐 반영.
- 2026-10-22 PM 요청으로 voicefix worktree 제거 및 완료 기록 보강. 로컬 백업: `D:/github/Worktree-backups/curtaincall-20261022-174457`. 공동 테스트는 미실시 상태 유지.
- 이번 검사: 구역 SKIPPED(PM), 문서 PASS(verify-fast, WARN18/HARD0), 컴파일 ENV_BLOCKED(연결된 에디터 없음), 테스트 SKIPPED(문서 마감). 아래 기존 PASS/FAIL은 그대로 보존한다.
- 동작하는 것: 컴파일 PASS, 음성 규칙 EditMode 16개 PASS. Start 흐름 Services 호스트 1인: Vivox 로그인 → 로비 생존자 채널 입장 → 추락 시 사망자 채널 전환 → 복귀 시 생존자 채널 재입장, 음소거 유지(2026-10-20).
- 막힌 것: 없음. Vivox 활성화 완료(PM, 2026-10-20), `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`에 server·domain·issuer 저장, `tokenKey` 빈 값·Test Mode 꺼짐.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/186

## 다음 할 일
1. dev에서 사후 공동 테스트 3항목(2인, PC 2대 권장, Start부터, 개발자 모드면 오른쪽 위 음성 확인 화면). 실패하면 fix Task로.
2. 별도 fix: 외줄 `TightropeSurfaceControlTests` 기존 실패 3개(dev #184).
3. 음소거 키 `M` 조작키 합의.

## PM이 확인할 것
- 확인 요청: 팀원 에디터는 Unity 계정 로그인 없이도 실행 중 음성 동작(서버 정보는 Settings.json으로 공유). Vivox 설정 화면을 열려면 에디터 안 계정 버튼으로 로그인해야 한다(Hub·`unity auth` 로그인과 별개).
- 대기 중인 결정: 음소거 키 기본값(임시 `M`, control-keys 목록 밖 — 디스코드 `소집` 합의).
- 공용 파일 요청: 없음(Vivox `Settings.json`은 Integration meta 공용 파일 표에 기록 완료).
- 교훈 기록 완료: `Harness/Project/Pitfalls/third-party-first-import-rewrite.md`.

## Verification
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20, 병합 후 통합 브랜치에서도 PASS)
- 3 테스트: FAIL — 테스트 오류 (10-20). EditMode 802 중 799 통과, 음성 `VoiceRoomRulesTests` 16개 전부 통과. 실패 3 = `TightropeSurfaceControlTests`(`CourseControlSwitcher`→`NetworkPlayer.CurrentJump` NRE). 해당 코드는 dev #184(a05f07f)에서 들어왔고 이 Task는 MainStage·Network·Player 코드를 바꾸지 않음 → 기존 실패, 음성 무관. 후속 fix 필요.
- 4 실행 로그: PASS — Start 흐름 Services 호스트 1인, `[Voice] 음성 로그인 완료` → `생존자(거리 감쇠) 채널 입장` → (Fallen) `사망자(거리 무관) 채널 입장` → (Normal) `생존자 채널 입장`, 음소거 상태 유지(`IsInputDeviceMuted=True`).
- 5 공동 테스트: 미실시 — PM 결정(2026-10-20, PC 1대라 불가). dev 병합 후 다음 날 팀 사후 검증 예정.

## 공동 테스트 결과
- 일시: 미실시
- 참여자:
- 재현:
- 예상:
- 실제:
