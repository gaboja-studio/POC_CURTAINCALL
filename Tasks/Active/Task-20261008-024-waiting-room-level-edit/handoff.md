# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다. 3차 배치 상세·검증 원문: `History/001-handoff-3rd-pass.md`.

## 지금 상태

- 단계: 3차 배치 완료(조감도 ①~⑩, 서커스 톤 드레싱, 무대 진입 터널, Cloth 커튼 9장, 깜박이는 전구 27개, 먼지). 저장·push 완료, 제출 중.
- 동작하는 것: `waiting_room.unity` 루트 `WaitingRoom`. 배치 스크립트 `build-layout.cs`(`unity command eval_file --file`로 실행).
- 막힌 것: 커튼 자동 열림(근접 트리거)은 코드가 필요해 미구현 — 지금은 플레이어가 밀어 젖히는 Cloth 반응만. Cloth는 플레이어 캡슐하고만 충돌.
- PR: 없음

## 다음 할 일

1. 사람이 Play로 확인: 커튼 흔들림, 깜박임 빈도, 안개 농도, 터널 길이감.
2. 자동 열림 커튼이 필요하면 PM에게 기능 Task 요청.

## PM이 확인할 것

- 확인 요청: 커튼 자동 열림은 이 Task 범위(코드 금지) 밖 — 기능 Task로 분리할지.
- 확인 요청: `PlaySetup/Player`에 자식 `ClothCollider`(트리거 캡슐) 추가(씬 오버라이드, 프리팹 원본 미수정).
- 확인 요청: 서커스 전용 에셋 부족(빅탑·간판 등), 조감도 ⑤/⑥ 번호·백스테이지 슬롯 수 불일치.
- 교훈 제안: 투명 재질을 에디터 스크립트로 렌더에 쓰면 `.mat`에 `_SrcBlendAlpha`/`_DstBlendAlpha`가 저절로 추가됨(서드파티 2개 발생, 되돌림). 시험 렌더엔 복제본 사용 (함정, Engine)
- 교훈 제안: Windows에서 `unity command eval`에 32KB 넘는 C#을 넘기면 "Argument list too long" → `eval_file --file` (함정, Engine)
- 확인 요청: PM 승인(2026-10-16)으로 구역 밖 변경 포함(postprocessing 패키지·ProjectSettings·TutorialInfo·SampleScene). `_ThirdParty`에 외부 에셋 5종과 자작 WaitingArea 리소스 추가(.unitypackage 제외).
- 대기 중인 결정: 없음 / 공용 파일 요청: 없음

## Verification

- 구역 검사: FAIL 10건(구역 밖 파일) — PM 승인으로 포함(2026-10-16)
- 1 문서/규칙: verify-fast OK (WARN 6건: 문서 길이)
- 2 컴파일: PASS (verify-unity -Compile, 2026-10-16)
- 3 테스트: NO_PROJECT_TESTS (배치 작업)
- 4 실행 로그: Play 모드 + `EditorApplication.Step()`으로 Cloth 흔들림·복귀, 깜박임 확인. Console 오류 0. 모든 방 도달, 큰 문·슬롯 문만 막힘.
- 5 공동 테스트: 미요청
