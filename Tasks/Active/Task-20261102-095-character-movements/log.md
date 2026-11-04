# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-02 PR #263 제출(dev): https://github.com/gaboja-studio/POC_CURTAINCALL/pull/263 — new-pr.ps1이 2단계 브랜치명을 거부해 gh로 직접 등록.
- 2026-11-02 하네스: Get-PrFlow(Scripts/Lib/common.ps1)가 feat/area/name 같은 다단계 브랜치를 받도록 수정. harness-flow 테스트 PASS(16), 현재 브랜치 new-pr -DryRun 통과(기존 PR #263만 경고).

- 2026-11-02 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/266 (fix/character/movements → dev)
- 2026-11-02 PR #263은 병합 완료. 하네스 수정은 PR #266(dev)으로 별도 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/266
- 2026-11-04 반대 방향 키 수정: 기존엔 먼저 누른 키를 '뗀 것'으로 보고 다시 누를 때까지 무시해, 나중 키를 떼면 정지했다. `OppositeKeyAxis`를 마지막에 누른 방향 기억 방식으로 바꿔 둘 다 누르면 나중 키, 하나만 남으면 그 키 방향. 같은 프레임 동시 입력은 0 유지.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
