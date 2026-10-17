# Log

## 2026-10-17

- PR #147 MERGED·Ubuntu CI 실패를 읽기 전용으로 확인한 뒤 사용자 승인 계획에 따라 후속 fix 준비. 전체 ref 번호 검사·새 발급 잠금으로 055 생성, PM 본인 직접 배정·착수 단회 예외. 최신 dev c55da6b 안전 전환, private pin 불변. 커밋·push·PR 없음.
- 기존 실제 선택식 두 곳에 후보 두 개를 반환하면 배열이 되는 문제를 재현했고 수정 후 단일 경로·실행 성공 확인. 구역·문서·컴파일 및 EditMode 406/406 PASS. 함정은 별도 지식 파일에 기록.
- 격리 PowerShell 146/146 PASS(동기화66·workspace31·안전전환24·에셋구역25). 실제 HEAD/index/private 보존 PASS. 단일 선택 제거 변이는 새 회귀가 2/2 차단. 로컬 Ubuntu는 pwsh 없음으로 ENV_BLOCKED, 실제 GitHub CI·공동 검증은 미실행. 저장·제출 승인 전 미커밋 유지.
- 사용자 "진행해"로 저장·제출 진행 승인. 최신 원격 dev와 기준 c55da6b 일치, 중복 PR 없음, Unity 씬 저장 상태 확인. 병합은 제외하며 PR 미리보기 확인 후 등록한다.
- 사용자 "등록하고 CI까지 확인해" 승인 후 PR #148 등록. 구현 커밋 852fce9의 Ubuntu run 38996128445는 문서·에셋구역25·동기화66 PASS, workspace-flow FAIL. 기타 4개 검사 PASS. 기존 Git 경로 오류 해결과 전체 CI 실패를 구분함.
- workspace-flow.test.ps1:421의 Update.*금지가 실제 오류 출력의 Update/금지 사이 줄바꿈과 일치하지 않음. 금지 처리는 실행됐으나 회귀의 문구 검증 실패. setup 수정 금지 구역이므로 임의 수정하지 않고 PM 후속 승인 필요로 인계. 제출 상태·실패 결과만 저장하며 병합하지 않음.
