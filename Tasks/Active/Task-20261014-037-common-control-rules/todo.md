# Todo

## PM 준비

- [x] Task 폴더 생성, `setup.md`·`plan.md` 초안 (2026-10-14)
- [x] Unity에서 테스트 씬·폴더 생성, JIRA 발급 후 `setup.md` 확정 (2026-10-14)
- [x] `plan.md` 공동 테스트 항목 합의 (2026-10-14)
- [ ] 인계 전 체크 완료, 담당자에게 전달

## 작업자

- [x] "시작해줘"로 작업 시작 (2026-10-14)
- [x] 구현 (세부 항목은 아래에 추가)
  - [x] 플랫폼 이동 → 공용 기본 이동 규칙(`Systems/Control/`), 외줄 규칙 → `MainStage/Tightrope/Control/` (GUID 유지)
  - [x] 조작 설정 에셋·인스펙터 목록·메뉴(`Tools/CurtainCall/조작/기본 이동 규칙 선택`), 씬 규칙 `SceneControlScheme`
  - [x] 공용 입력 막기 `InputGate`·`UIInputBlocker`, `TightropeUIFocus`는 요청만, `TightropeInputGate`는 호환 창구
  - [x] EditMode 테스트 5개, 지도 갱신(common-game-systems·tightrope-course)
  - [x] 테스트 씬 `ControlRules.unity` 배치(2026-10-14 PM 예외 허용, AI)
  - [x] 공동 테스트 항목 1~3 AI 플레이 확인(Step 진행, 키 입력은 값 주입으로 대체)
  - [x] 사람 손 키 입력 확인 (10-14, ControlRules·proto_0.0.2 모두 정상)
- [x] "검사해줘" 통과
- [ ] "제출해줘"로 PR 제출

## PM 통합

- [ ] PR 병합
- [ ] 공동 테스트 통과
- [ ] 테스트 씬 처리 결정 기록 (`setup.md`)
