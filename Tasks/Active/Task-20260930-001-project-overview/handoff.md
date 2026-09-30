# Handoff

## 지금 상태

- 단계: 문서 변경을 `feat/project-overview`에 제출했다. [PR #2](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/2)는 `integration/document-project` 대상으로 열려 있으며 병합 전이다. 공식 인계는 진행되지 않아 Task 메타는 `scaffolded`로 유지한다.
- 원문 `Docs/References/game-flow.md`를 보존하고 `Harness/Project/Facts/overview.md`·`game-flow.md`·`index.md`에 기획 기준, 저장소에서 확인한 구현 상태, 미정 항목을 구분해 반영했다. `setup.md`·`plan.md`·`todo.md`도 실제 작업 범위에 맞춰 갱신했다.
- 브랜치: `feat/project-overview`에 문서 8개 파일만 커밋·push했다. 전환 중 LFS 필터로 `Assets/TutorialInfo/Icons/URP.png`가 수정으로 표시됐으나 작업 파일과 HEAD의 SHA-256은 같다. PR 변경 파일에는 포함되지 않았다.

## 다음 할 일

1. PM이 [PR #2](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/2)의 실패한 `verify-scope`를 확인한다. PR의 8개 변경 파일에는 없지만 CI 작업 트리에서 LFS 이미지 `Assets/TutorialInfo/Icons/URP.png`가 수정으로 감지됐다. 검사 환경·필터를 점검한 뒤 재실행한다.
2. PM이 공용 원본 파일의 Integration 소유 표, `AGENTS.md`의 오래된 TBD 안내 및 공식 인계 상태를 확인한다.
3. `plan.md`의 공동 리뷰를 진행하고 PowerShell 환경 점검·문서 검사를 로컬에서도 재실행한다.

## PM이 확인할 것

- `Docs/References/game-flow.md`의 Integration 소유 표 반영과 `setup.md` 인계 상태가 남아 있다. `meta.md`의 `scaffolded`는 임의 변경하지 않았다.
- `AGENTS.md`의 “게임 개요는 아직 미정(TBD)” 문구는 제공된 기획 자료를 반영한 Facts와 다르지만 이번 작업 구역 밖이므로 수정하지 않았다.
- CI의 `verify-scope` 실패는 PR 변경 파일이 아닌 LFS 이미지의 작업 트리 수정 감지 때문이다. `Get-SetupAreas`의 `Harness/Project/` 파싱 제약도 있어 PM의 검사 경로 확인이 필요하다.
- 제공 원문의 `CONTEXT.md` 참조는 저장소에서 확인되지 않는다. ※미정 사항·임시 튜닝값의 확정은 사람이 결정한다.

## Verification

- 0 구역: `FAIL(CI)` — `verify-scope`가 CI 작업 트리의 `Assets/TutorialInfo/Icons/URP.png`를 수정 금지로 판정했다. 이미지 파일은 PR 변경 목록에 없고, 로컬에서도 HEAD와 작업 파일 SHA-256이 같다. 변경 문서 8개 경로는 `setup.md`와 수동 대조했다. 검사 환경·LFS 필터 확인 필요.
- 1 문서/규칙: `PASS(CI verify-fast)` — GitHub Actions의 `verify-fast`와 `verify-commits`가 통과했다. 로컬 `verify-knowledge.ps1`, `verify-context.ps1`, `verify-fast.ps1`은 워크트리 격리 장치로 실행이 차단됐다. 수동 점검에서 Facts 33·30줄, frontmatter 4항목, 상대 `.md` 파일 존재, 원본 SHA-256 `97887676df1dbe6774c5a5c4434a64b3a573f72fcdd6e4a6d9a15f40a658fe97` 일치를 확인했다. 링크 앵커의 실제 렌더링은 미검증이며 `git diff --check` 오류 없음.
- 환경 점검: `ENV_BLOCKED` — `doctor.ps1`도 같은 제약으로 실행되지 않았다. 이전 준비 단계의 통과 기록을 이번 검사로 재사용하지 않는다.
- 2 컴파일: `SKIPPED(문서만 변경)` — Unity 코드·에셋 변경 없음.
- 3 테스트: `NO_PROJECT_TESTS` — 테스트 어셈블리가 없고 Unity 테스트를 실행하지 않았다.
- 4 실행 로그: `SKIPPED(문서 작업)` — 플레이 모드 실행 없음.
- 5 공동 테스트: `SKIPPED(공식 인계·공동 리뷰 전)` — `plan.md` 항목은 준비됨.
