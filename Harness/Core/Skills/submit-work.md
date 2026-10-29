---
name: submit-work
description: 선택한 공유 대상으로 PR을 제출한다. integration 필수 경유는 없다.
---

# submit-work

1. plan·todo·최근 log와 실제 변경을 확인한다. 작업 목적과 남은 확인을 짧게 정리한다.
2. 필요한 검사가 현재 변경과 일치하는지 확인한다. 같은 검사를 반복하지 않는다.
3. `pwsh -File Scripts/new-pr.ps1 -Summary "<문제와 변경 결과>" [-Base <대상>] [-Title "<제목>"] -DryRun`.
4. 제출 요청은 관련 변경 저장·push와 PR 생성을 포함한다. dev 또는 필요한 작업·통합 브랜치를 고른다.
5. 실제 등록 후 PR 링크를 log에 기록하고 채팅에 첨부한다. 중간 Status·인계 문서는 만들지 않는다.
6. dev 반영은 지정 팀원 검토를 거친다. 병합은 사용자가 요청한 경우에만 한다.
