---
name: merge-integration
description: 선택 대상의 PR을 조회하고 지정 팀원이 검토한 변경을 요청 시 병합한다.
---

# merge-integration

1. `pwsh -File Scripts/merge-integration.ps1 -Base <dev 또는 대상>`로 PR을 확인한다. 옛 -Slug도 사용 가능하다.
2. 사용자가 병합을 요청했다면 실제 대상·head·검사·지정 팀원 검토를 확인한다. PM 전용은 아니다.
3. `-Merge <번호>`는 현재 방향에 맞는 Squash/Merge commit을 사용하며 head SHA가 바뀌면 중단한다.
4. 충돌은 담당자와 확인한다. 보호 우회·force·자동 전체 병합은 하지 않는다.
