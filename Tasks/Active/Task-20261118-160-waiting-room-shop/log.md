# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-18
- 구현: 신체 복구 입장 전 추가 예약, 해금 전 공연 기물 목록 제외 + 해금 알림(HUD 프로필 `shopItemsAdded`, 기본 시스템 보상).
- 검사 `check-work.ps1`: 안전성 PASS, 문서/규칙 PASS, 컴파일 ENV_BLOCKED(이 worktree에 연결된 Unity 에디터 없음), 테스트 SKIPPED.
- 대체 컴파일 확인: 같은 커밋(dev 0b8f5e8f)인 원본 저장소의 생성 csproj를 임시 복사해 `dotnet build` — Assembly-CSharp·Editor·ExpertService EditModeTests 오류 0. 임시 파일은 삭제.
- 새 EditMode 테스트 2개(추가 예약 규칙)는 Unity에서 아직 실행하지 않음.
- 다음: 사람 플레이 확인(추가 예약 흐름, 작업실 개방 시 알림·목록 추가).
- 제출 전 origin/dev(e5f772c3) 반영. dev에서 159번을 먼저 써서 Task 번호를 160으로 변경.
- 작업 트리의 에디터 재저장 변경(폰트 SDF·머티리얼·_ThirdParty)은 이 작업과 무관해 커밋하지 않음.
