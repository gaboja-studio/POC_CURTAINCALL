# Handoff

## 지금 상태
- 단계: 구현 완료·미저장(커밋 전). worktree `D:\github\POC_CURTAINCALL-voice`, 전용 Unity 포트 7802.
- 동작하는 것: 컴파일 PASS, 음성 규칙 EditMode 16개 PASS. Start 흐름 Services 호스트 1인: Vivox 로그인 → 로비 생존자 채널 입장 → 추락 시 사망자 채널 전환 → 복귀 시 생존자 채널 재입장, 음소거 유지(2026-10-20).
- 막힌 것: 없음. Vivox 활성화 완료(PM, 2026-10-20), `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`에 server·domain·issuer 저장, `tokenKey` 빈 값·Test Mode 꺼짐 확인.
- PR: 없음

## 다음 할 일
1. (PM) `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`을 통합 브랜치에 커밋(Task 구역 밖 — 지금 verify-scope가 이 파일로 FAIL). 커밋 전 `tokenKey`가 비어 있는지 다시 확인.
2. "저장해줘" → "검사해줘" → "제출해줘".
3. 공동 테스트 3항목(2인, PC 2대 권장).

## PM이 확인할 것
- 확인 요청: 다른 팀원 에디터도 Unity 계정 로그인·프로젝트 연결 필요 없음(서버 정보는 Settings.json으로 공유). 단 에디터가 anonymous면 Vivox 설정 화면을 열 수 없다(이 PC는 에디터 왼쪽 위 계정 버튼으로 다시 로그인해 해결).
- 대기 중인 결정: 음소거 키 기본값(임시 `M`, control-keys 목록 밖 — 디스코드 `소집` 합의).
- 공용 파일 요청: `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`(Vivox 서버 정보) — PM 통합 준비로 커밋. Integration meta 목표 문구를 "Start 흐름 확정 씬"으로 정정(병합 때).
- 교훈 제안: 새 worktree 첫 import 때 `_ThirdParty`의 AbandonedFactory 씬이 빈 씬으로 덮이고 HDRP 머티리얼 ~220개가 변환됨 → 에디터를 닫은 뒤 `git -C Assets/_ThirdParty checkout -- .`로 복구(함정).

## Verification
- 구역 검사: OK (2026-10-20, PM 작업)
- 1 문서/규칙: OK (verify-fast)
- 2 컴파일: PASS
- 3 테스트: EditMode 802 중 799 PASS, 실패 3 = `TightropeSurfaceControlTests` 3개(`CourseControlSwitcher`→`NetworkPlayer.CurrentJump` NRE, dev #184 이후 기존 실패, 음성 무관). PlayMode 170 중 160 PASS·7 FAIL·3 SKIP — SpaceLink·PerformanceLink·PerformanceRoute·CameraViews Proto 계열 기존 실패(Integration-player-locomotion 기록과 같은 묶음, 음성 무관). PlayMode는 범위 변경 전 실행.
- 4 실행 로그: PASS — Start 흐름 Services 호스트 1인, `[Voice] 음성 로그인 완료` → `생존자(거리 감쇠) 채널 입장` → (Fallen) `사망자(거리 무관) 채널 입장` → (Normal) `생존자 채널 입장`, 음소거 상태 유지(`IsInputDeviceMuted=True`). 첫 시도는 Vivox 미설정으로 초기화 실패 → PM 활성화 후 통과.
- 5 공동 테스트: 미요청

## 공동 테스트 결과
- 일시:
- 참여자:
- 재현:
- 예상:
- 실제:
