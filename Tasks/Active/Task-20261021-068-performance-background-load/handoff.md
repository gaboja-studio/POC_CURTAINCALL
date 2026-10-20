# Handoff

## 지금 상태

- fix/performance-background-load: dev 기반 같은 프로젝트 폴더에서 구현. 별도 worktree/에디터 없음.
- #196 private b6cc29c7 포인터와 배경 빌드 등록, 실제 묘기 자동 additive 로드·종료 해제 완료.
- 대상: HumanCannon/Tightrope/proto_0.0.2. 원본 1/3, root Z54.4583/55.8333, 무대 Y0. 기존 코스·씬 저장본 수정 없음.
- 원본 조명 범위/세기 배율과 안개·ambient·카메라 배경색을 런타임 적용, 이탈 시 복원. 수동으로 연 배경은 소유·변형·해제하지 않음.
- NGO 공개 검증 콜백으로 배경을 네트워크 동기화에서 제외. 게임의 동기화 모드·공용 전환 코드는 유지.
- #197·#198은 충돌로 미병합. 사용자 승인으로 본 Task PR 제출·CI 후 병합 및 두 PR 닫기 예정.
- 기존 Vivox 변경 제외. 재질 3개 소수점 자동 변경은 승인받아 원복. 외줄 Play가 만든 TightropeCourse.prefab 4줄 변경은 로컬 보존·제출 제외. 임시 캡처는 scratchpad로 이동.
- 교훈: [NGO 로컬 배경 동기화](History/Networking/local-additive-scene-synchronization.md). 공용 Pitfalls는 Task-062 소유 구역이라 직접 제출하지 않고 제안으로 보관.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/199 — 9978056 구현 제출. 2026-10-21 사용자 재승인으로 PR 생성 완료. 추가 Unity 검사 없이 GitHub 필수 검사 후 Squash 병합하고 #197·#198을 닫는다.

## Verification

- 구역 검사: PASS (origin/dev 기준, PM). 제출 파일은 전용 코드·테스트·빌드 목록·포인터·Task/지식 문서로 제한.
- 1 문서/규칙: PASS, 기존 WARN 9/hard 0.
- 2 컴파일: PASS (up_to_date).
- 3 테스트: PASS — 배경 PlayMode 11/11, 전체 EditMode 858/858 (2026-10-21).
- 4 실행 로그: PASS — 인간대포·외줄 직접 Play에서 배경 1개/배치/active scene 유지와 Scene/Game 렌더 확인. 인간대포→Start Single 전환 후 배경 0개. 실제 입력·완주 검증은 아님.
- 5 공동 테스트: SKIPPED — 실제 2인·늦은 참가·PC 성능 측정 미실행.
- 중간 실패: SceneHandle/int NUnit 비교를 Scene 비교로 수정. 호스트 테스트 NetworkConfig 초기화 및 Transport 어셈블리 참조 수정. 한 실행의 외부 Multiplayer Editor API InvalidOperationException은 최종 재실행에서 재현되지 않음.

## 다음 할 일

1. 승인된 제출·CI·병합 및 대체 PR 정리.
2. 실제 두 화면/늦은 참가/프레임 시간은 plan.md 3개 공동 확인 항목으로 검증.
