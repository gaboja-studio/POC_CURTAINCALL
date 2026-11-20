# Log

- 2026-11-20 DEV 451fe76d에서 외줄 결승 목마 관문을 조사했다. 이전 승인은 아래 두 명 중 사망·이탈이 있을 때만 비워져, 둘 다 살아서 역할을 바꾸면 출발을 계속 거부할 수 있다.
- 기존 승인자의 진행을 유지하면서 출발선에서 바뀐 아래 두 사람의 목마 순서를 재승인한다. 결승 관문과 착지 발판, 필요한 인원 수, 승인의 공유 형식은 기존 값을 사용한다.
- EditMode에서 실제 외줄 충돌체와 목마 연결을 적용했다. 역할 교체는 수정 전 실패했고, 수정 후 재승인과 관문을 이미 건넌 단원의 승인 유지가 모두 통과했다.
- 2026-11-20 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS(배치 EditMode); 테스트=PASS(신규 회귀 2건). 실제 플레이·멀티플레이 미실행.

- 2026-11-20 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/461 (fix/tightrope-finale-stack-retry → dev)
- 2026-11-20 DEV 검토용 [PR #461](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/461)에 제출했다.
