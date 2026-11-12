# Task-20261112-121

- **Title:** 로비 방 코드의 nobr 태그 표시 제거
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/lobby-room-code-nobr
- **Base:** dev

## Context
로비 방 코드에 줄바꿈 방지용 `<nobr>`가 글자로 표시되는 문제를 핫픽스로 수정한다.

## 계획
방 정보 카드의 자체 일반 텍스트 표시를 유지하고 공용 HUD 포매터 적용을 제거한다. 가림·표시·복사를 검증한 뒤 이 변경만 dev 대상 PR로 제출한다.

## 미정·주의
엔딩 작업과 원래 작업 브랜치의 미저장 변경을 보존하기 위해 origin/dev 기반 별도 작업 폴더에서 진행한다.
