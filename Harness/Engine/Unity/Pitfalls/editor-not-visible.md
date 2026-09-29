---
summary: 에디터가 켜져 있는데 unity status가 인스턴스 0개를 반환함 (샌드박스·Pipeline 미설치·경로 불일치)
status: active
updated: 2026-09-29
source: Octoplug harness/mcp/README.md
---

# 에디터가 보이지 않음

## 언제 읽나

`unity status`가 `STATUS_NO_INSTANCES`를 반환하거나 `verify-unity.ps1`이 exit 2로 끝날 때.

## 증상

사람은 에디터를 열어 두었다고 하는데 CLI에서는 연결된 에디터가 없다고 나온다.

## 원인 후보

1. 에디터가 실제로 꺼져 있거나 아직 import 중이다.
2. AI 도구의 샌드박스가 로컬 포트/프로세스를 가린다.
3. 에디터가 다른 경로(다른 worktree)의 프로젝트를 열고 있다.
4. 프로젝트에 Pipeline 패키지가 없거나 초기화되지 않았다.

## 대처

- 에디터가 꺼졌다고 단정하지 않는다. 사람에게 열린 프로젝트 경로를 확인한다.
- `unity status --json`의 `instances[].projectPath`와 Task worktree를 비교한다.
- 연결이 안 되면 Unity 수정 작업은 하지 않고 `ENV_BLOCKED(editor not visible)`로 handoff에 적는다.

## 확인 방법

```
unity status --json --project-path <task-worktree>
```
