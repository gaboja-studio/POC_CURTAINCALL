---
name: update-domain-map
description: 기능 영역의 담당 파일·진입점·의존성이 바뀌었을 때 Domain Map을 짧고 정확하게 갱신한다.
---

# Update Domain Map

권한: 해당 기능 Task 담당자(작업 중) 또는 PM. 동시에 고친 경우 **실제 코드와 맞는 쪽**이 우선이며, 합치기 단계에서 PM이 최종 정리한다(`team-roles.md`).

1. 오래 유지될 담당 구조, 진입점, 의존성, 수정 주의점이 바뀌었을 때만 갱신한다.
2. "존재함"으로 적은 경로는 실제로 확인한다. 아직 없는 위치는 `planned`로 표시한다.
3. 현재 구현 사실 / 기획 방향 / 미결정 사항을 구분한다.
4. 크기 한도(`context-budget.md`의 `domain`)를 넘지 않는다. 넘으면 도메인을 나눈다.
5. 다른 도메인 내용은 복제하지 말고 링크한다.
6. 함정·결정은 Domain Map에 쓰지 않고 지식 폴더로 보낸다(`record-knowledge`).
7. 도메인을 추가·이름 변경·분리·폐기하면 `Docs/Domains/index.md`를 갱신한다.
