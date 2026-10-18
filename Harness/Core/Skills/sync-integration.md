---
name: sync-integration
description: PM 전용. 원격 child Base의 선언된 부모에서 직계 자식 통합으로 동기화 PR을 미리보고, 별도 승인 후 생성·재사용한다. 자동 병합하지 않는다.
---

# Sync Integration

권한: PM만. gh 로그인 계정이 PM인지 확인하고 `git-workflow.md`·필수 리뷰/보호 규칙을 따른다.

## 명령

```powershell
pwsh -File Scripts/sync-integration.ps1 -Child integration/<slug> [-Parent <branch>] [-DryRun]
```

- `-Child` 필수. 부모는 **원격 child 메타 Base**의 `dev` 또는 `integration/*`이며 직계 부모만 허용한다.
- `-Parent`는 선택 확인값이다. 생략하면 Base로 결정하고, 명시하면 선언된 Base와 일치해야 한다. 부모를 덮어쓰는 옵션이 아니다.
- 로컬 체크아웃·작업 문서와 독립적으로 필요한 원격 refs만 fetch한다. 브랜치 전환·작업 파일 변경·commit/push를 동반하지 않는다.
- 이 명령은 부모→자식 PR 전용이다. 자식→부모 반영, 준비 Task 배정 동기화, 통합 마감은 기존 별도 절차를 따른다.

## 절차

1. 대상 child와 목적을 확인한다. 명령에 `-DryRun`을 붙여 **미리보기부터** 실행한다. 원격 조회/fetch는 하지만 PR을 쓰지 않는다.
2. 선언된 부모→직계 child 방향, 양쪽 SHA, 변경 범위와 예상 결과를 PM에게 제시한다. 임의 통합 간 동기화나 dev 직행으로 대체하지 않는다.
3. **미리보기와 별도의 실행 승인**을 받는다. 일반 동기화 요청·미리보기 승인은 PR 생성 승인이 아니다. 이 단계에서 멈추라는 요청이면 실제 실행하지 않는다.
4. 승인된 동일 인자로 `-DryRun`만 빼고 실행한다. 실제 head=부모/base=child인 open PR을 생성하거나 재사용한다. 기존 PR/상태가 달라지면 임의 정리하지 않고 보고한다.
5. 결과·PR URL(있으면)·SHA를 보고한다. DryRun 결과는 예상일 뿐이며 실행 전 원격 변경으로 달라질 수 있다.

| 결과 | 의미 |
|---|---|
| `Created` / `WouldCreate` | 신규 생성 / DryRun에서 생성 예정(외부 쓰기 없음) |
| `Reused` | 동일 방향의 기존 open PR 재사용 |
| `UpToDate` | 부모 변경이 child에 이미 포함되어 새 PR 생성 없음 |

## 병합 전 안전 경계

- PR 생성·재사용은 병합 승인이 아니다. **리뷰·CI 완료를 기다린 뒤 별도 승인된 Merge commit**으로만 반영한다. Squash·자동 merge·보호 우회는 금지한다.
- `merge-integration.ps1 -Merge`는 일반 작업 head Squash 전용이므로 쓰지 않는다. 부모 head PR의 **Update branch도 금지**한다(자식 변경이 부모로 역유입됨).
- GitHub API로 부모·자식 SHA를 원자적으로 고정할 수 없다. 미리보기/생성 이후 SHA·방향·OPEN 상태가 변경되면 멈추고 최신 차이·리뷰·CI를 재검토한다. 기존 승인으로 밀어붙이지 않는다.
- 실패 시 현재 PR·브랜치를 보존하고 원인/현재 상태를 보고한다. 삭제·되돌리기·force 이동·자동 복구·다른 도구로 우회하지 않는다. 충돌은 PM/담당자가 별도 승인 범위에서 처리한다.
