---
summary: 새 worktree를 Unity로 처음 열면 _ThirdParty의 AbandonedFactory 씬이 빈 씬으로 덮이고 HDRP 머티리얼 ~220개가 변환된다. 에디터를 닫은 뒤 서브모듈을 원본으로 복구
status: active
updated: 2026-10-20
source: Task-20261020-063 (2026-10-19 Task-059에서도 재현)
---

# 새 worktree 첫 import 때 외부 에셋이 바뀜

## 언제 읽나

새 worktree를 만들어 Unity로 처음 열었거나, 에디터를 연 뒤 `Assets/_ThirdParty`가 `modified content`로 보이고 저장·PR 스크립트가 "저장하지 않은 변경"으로 막힐 때.

## 증상

- `git -C Assets/_ThirdParty status`에 `.mat` 약 220개와 `LeartesStudios/AbandonedFactory/HDRP/Scene/AbandonedFactory.unity`가 수정됨.
- 씬 파일이 원본(24,771,936바이트) 대신 약 3.5KB 빈 YAML 씬이 된다(로딩 실패 "Unknown error occurred while loading" 뒤 덮어씀).
- `new-pr.ps1`이 "저장(커밋)하지 않은 변경이 있음"으로 등록을 멈춘다(`save-work`는 주의만 띄우고 통과).

## 원인

URP 프로젝트가 HDRP 머티리얼을 import하면서 다시 직렬화하고, 바이너리 씬을 읽지 못해 빈 씬으로 저장한다. 에디터가 열려 있는 동안 되돌려도 다시 import해 또 바뀐다.

## 대처

1. 바뀐 파일이 이번 Task에서 고친 것이 아닌지 확인한다(worktree를 열기 전 서브모듈 status가 깨끗했는지).
2. 해당 worktree의 Unity 에디터를 닫는다(저장 안 된 씬이 없는지 먼저 확인).
3. `git -C Assets/_ThirdParty checkout -- .`로 원본 복구. 부모 저장소의 서브모듈 포인터는 바뀌지 않으므로 커밋할 것은 없다.
4. 새 worktree의 서브모듈 git dir는 따로라 바이너리 씬 attributes도 따로 복사해야 한다([third-party-branch-sync.md](third-party-branch-sync.md)).

## 확인 방법

`git -C Assets/_ThirdParty status --short`가 비어 있고, 씬 파일 크기가 24,771,936바이트.
