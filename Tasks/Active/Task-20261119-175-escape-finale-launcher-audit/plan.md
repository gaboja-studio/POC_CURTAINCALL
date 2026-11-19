# Task-20261119-175

- **Title:** 최종 추격자와 톱날 발사기 사후 검사
- **Type:** fix
- **Assignee:** @RagSpirin
- **Branch:** fix/escape-finale-launcher-audit
- **Base:** dev

## Context

최신 dev의 최종 추격자와 휴대 톱날 발사기를 재검사한다. 이미 통과한 시나리오는 제외한다.

## 계획

- 경기장 바깥으로 계속 날아가는 톱날이 발사 가능 슬롯을 채우는 경우를 재현한다.
- 전투 구역에서 멀어지는 톱날을 정리하고, 돌아오는 방향의 톱날과 다른 씬에 있는 발사기 판정을 검증한다.
