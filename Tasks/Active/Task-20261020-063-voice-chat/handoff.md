# Handoff

## 지금 상태
- 단계: 제출 완료(PR #186 → integration/voice-chat). worktree `D:\github\POC_CURTAINCALL-voice`(에디터는 닫음, 서드파티 원본 복구).
- 동작하는 것: 컴파일 PASS, 음성 규칙 EditMode 16개 PASS. Start 흐름 Services 호스트 1인: Vivox 로그인 → 로비 생존자 채널 입장 → 추락 시 사망자 채널 전환 → 복귀 시 생존자 채널 재입장, 음소거 유지(2026-10-20).
- 막힌 것: 없음. Vivox 활성화 완료(PM, 2026-10-20), `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`에 server·domain·issuer 저장, `tokenKey` 빈 값·Test Mode 꺼짐 확인.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/186

## 다음 할 일
1. (PM) PR #186 병합 → Integration meta 공용 파일 표에 Vivox `Settings.json` 추가·목표 문구 정정.
2. 통합 후 공동 테스트 3항목(2인, PC 2대 권장, Start부터). 결과를 아래 "공동 테스트 결과"에 기록.
3. 별도 fix: 외줄 `TightropeSurfaceControlTests` 기존 실패 3개(dev #184).

## PM이 확인할 것
- 확인 요청: 다른 팀원 에디터도 Unity 계정 로그인·프로젝트 연결 필요 없음(서버 정보는 Settings.json으로 공유). 단 에디터가 anonymous면 Vivox 설정 화면을 열 수 없다(이 PC는 에디터 왼쪽 위 계정 버튼으로 다시 로그인해 해결).
- 대기 중인 결정: 음소거 키 기본값(임시 `M`, control-keys 목록 밖 — 디스코드 `소집` 합의).
- 공용 파일 요청: `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`은 PM이 이 Task 배정 공용 파일로 추가해 커밋함(9e0474e) → 병합 때 Integration meta 공용 파일 소유 표에 추가, 목표 문구를 "Start 흐름 확정 씬"으로 정정.
- 교훈 제안: 새 worktree 첫 import 때 `_ThirdParty`의 AbandonedFactory 씬이 빈 씬으로 덮이고 HDRP 머티리얼 ~220개가 변환됨 → 에디터를 닫은 뒤 `git -C Assets/_ThirdParty checkout -- .`로 복구(함정).

## Verification
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: FAIL — 테스트 오류 (10-20). EditMode 802 중 799 통과, 음성 `VoiceRoomRulesTests` 16개 전부 통과. 실패 3 = `TightropeSurfaceControlTests`(`CourseControlSwitcher`→`NetworkPlayer.CurrentJump` NRE). 해당 코드는 dev #184(a05f07f)에서 들어왔고 이 브랜치는 MainStage·Network·Player 코드를 바꾸지 않음 → 기존 실패, 음성 무관. 후속 fix 필요.
- 4 실행 로그: PASS — Start 흐름 Services 호스트 1인, `[Voice] 음성 로그인 완료` → `생존자(거리 감쇠) 채널 입장` → (Fallen) `사망자(거리 무관) 채널 입장` → (Normal) `생존자 채널 입장`, 음소거 상태 유지(`IsInputDeviceMuted=True`). 첫 시도는 Vivox 미설정으로 초기화 실패 → PM 활성화 후 통과.
- 5 공동 테스트: 미요청

## 공동 테스트 결과
- 일시:
- 참여자:
- 재현:
- 예상:
- 실제:
