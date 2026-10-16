---
summary: MPPM 가상 플레이어 복제본(Library/VP/)이 새 폴더·asmdef를 복사받지 못해 "Could not read file …asmdef"로 바로 꺼짐
status: active
updated: 2026-10-16
source: Task-20261013-032
---

# MPPM 복제본이 새 파일을 놓침

## 언제 읽나

Multiplayer Play Mode로 2번 이상 플레이어 창을 켰는데 `Compilation Pipeline: Could not read file Assets/.../<이름>.asmdef` 오류 창이 뜨고 Quit만 눌러 꺼질 때. 또는 메인 에디터에서는 되는데 가상 플레이어만 새 씬·스크립트를 못 찾을 때.

## 증상

- 가상 플레이어가 시작 직후 asmdef 읽기 오류로 종료된다.
- `Library/VP/mppm<id>/Assets/` 아래에 최근 새로 만든 폴더의 파일이 없다(빈 폴더나 `.gitkeep`만). 기존 파일 수정분은 반영돼 있다.
- `ProjectSettings/EditorBuildSettings.asset` 같은 설정도 옛것일 수 있다(빌드 목록이 옛것이면 NGO 씬 전환에서 클라이언트가 씬을 못 연다).

## 원인

MPPM은 메인 프로젝트를 `Library/VP/` 아래 복제본으로 동기화한다. 가상 플레이어를 켜 둔 채 새 폴더·asmdef가 생기면 동기화가 일부 파일을 놓칠 수 있다(정확한 조건은 미확정).

## 대처

1. MPPM 창에서 해당 가상 플레이어를 끈다(프로세스가 남았는지 확인).
2. `Library/VP/mppm<id>/` 폴더를 지운다. Library 아래 캐시라 git과 무관하고, MPPM이 다음에 켤 때 새로 만든다.
3. 가상 플레이어를 다시 켠다. 첫 실행은 복제·임포트로 오래 걸린다.

## 확인 방법

`diff <(cd Assets && find . -type f | sort) <(cd Library/VP/mppm<id>/Assets && find . -type f | sort)`가 비어 있는지 본다. 가상 플레이어가 오류 없이 뜨는지 확인한다.
