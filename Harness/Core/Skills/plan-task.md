---
name: plan-task
description: Task 계획과 공동 테스트 항목을 구현 전에 작성한다. PM과 담당자가 합의하며 구현하지 않는다.
---

# Plan Task

1. Task의 `meta.md`, `setup.md`와 관련 Domain Map 1개를 읽는다.
2. 관련 지식 폴더의 `index.md`를 보고 해당되는 함정·결정 파일만 읽는다.
3. 새 구조를 제안하기 전에 이름이 정확한 기존 파일을 먼저 확인한다.
4. 확인된 근거 / 가정 / 사람이 정해야 할 것(Open Decision)을 구분한다.
5. `plan.md`를 작성한다: 배경, 접근, 수정할 파일, 위험, 자동 검증, **공동 테스트 항목(최대 3개)**.
   - 모든 수정 파일은 `setup.md` 작업 구역 안이어야 한다. 밖이 필요하면 PM에게 구역 추가나 공용 파일 배정을 요청한다.
   - 다른 기능과의 연결이 필요하면 연결 Task로 분리하자고 제안한다(`Harness/Engine/Unity/Policies/code-folders.md`).
6. `todo.md`의 작업자 칸에 세부 항목을 추가한다.
7. 게임 규칙·UX·아트 등 사람이 정할 것은 `request-human-decision`으로 묻고 멈춘다.
8. 계획이 합의되기 전에는 구현하지 않는다.
