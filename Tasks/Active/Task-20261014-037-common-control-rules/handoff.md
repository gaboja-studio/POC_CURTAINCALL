# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.
## 지금 상태

- 단계: 제출 완료(submitted), PM 병합·공동 테스트 대기
- 동작하는 것: 공용 `PlayerController`가 외줄을 모름. 규칙 순서 = 끼운 규칙 → 시작 규칙 → 씬 규칙(`SceneControlScheme`) → 기본 이동 규칙(`ControlSettings.asset`, 지금 플랫폼 이동), 플레이 중 변경 즉시 반영. 공용 `InputGate`·`UIInputBlocker`. 외줄은 `TightropeUIFocus`가 요청만, `TightropeInputGate`는 호환 창구(공연장 다른 파일 수정 없음)
- 테스트 씬 `ControlRules.unity`(PM 예외 허용 배치): Floor·Player·DebugHud·TestUI/BlockingPanel(꺼 둠, `UIInputBlocker`)·SceneControlScheme(비움)
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/61

## 다음 할 일

1. PR 병합 후 PM 공동 테스트

## PM이 확인할 것

- 확인 요청: 기본 이동 규칙 위치 = 새 `Settings/Control/ControlSettings.asset`(meta Open 제안대로), `SetScheme(null)` 의미 "규칙 없음" → "기본 쪽 복귀"(호출처 없음)
- 공용 파일 요청: `Docs/Domains/player-control.md`(작업 구역 밖) 조작 규칙 경로·`SetScheme` 설명 갱신. `player-sync.md`는 바뀐 동작 없음
- 교훈 제안(함정, `playmode-frame-freeze.md` 갱신): 일시정지 후 `EditorApplication.Step()`을 eval 안에서 부르면 프레임이 동기로 진행됨. 단 큐에 넣은 키보드 이벤트는 Game 창 포커스·입력 설정을 바꿔도 플레이어 입력에 닿지 않음

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14, Console 오류 0)
- 3 테스트: PASS (EditMode, 5/5, 10-14)
- 4 실행 로그(AI 플레이, Step 진행): ControlRules — 기본 규칙 Platform·대각 이동 OK, 기본 규칙 변경/복귀 즉시 반영, 씬 규칙 적용/해제, 패널 켜면 막힘·끄고 키 뗀 다음 프레임 풀림, `SetScheme(null)` 복귀. proto_0.0.2(미저장) — 플랫폼 Platform→줄 위 Tightrope+균형 켬, 로비·외줄 모달 동안 막힘·닫으면 다음 프레임 풀림, 오류 0
- 5 공동 테스트: 1~3 AI 확인 + 작업자 키보드 확인 PASS(ControlRules·proto_0.0.2, 10-14), PM 공동 테스트 대기
