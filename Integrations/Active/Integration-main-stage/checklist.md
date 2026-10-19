# Checklist

## 병합 전

- [ ] 모든 Task의 PR이 제출됨 (meta.md Tasks 표)
- [ ] 각 PR의 검사 결과(구역·컴파일·테스트)가 채워져 있음
- [ ] 공용 파일 소유 표와 실제 PR 변경 파일이 겹치지 않음

## 병합 순서

공용 파일을 소유한 Task → 다른 Task가 의존하는 Task → 나머지 순서로 정한다.

1.

## PR 하나를 병합할 때마다

1. Squash로 병합한다 (Task 하나 = 기록 한 줄).
2. 다음 PR이 뒤처졌으면 GitHub **Update branch**로 최신 내용을 받게 한다.
3. 충돌이 나면 해당 Task 담당자와 함께 해결한다. 씬·프리팹 충돌은 AI에게 맡기지 않는다.
4. 컴파일 확인 후 다음 PR로 넘어간다.

## 공동 테스트 항목

각 Task `plan.md`의 공동 테스트 항목을 모은다.

| Task | 항목 | 결과 |
|---|---|---|

## dev로 PR 전

- [ ] 공동 테스트 전부 통과, 실패 항목은 후속 Task로 분리
- [ ] Task 폴더를 모두 `Tasks/Done/`으로 이동
- [ ] 각 Task의 테스트 씬 처리 결정이 기록됨
- [ ] 병합된 작업 브랜치와 PR을 닫음
- [ ] 각 Task PR의 `closed #번호`를 모두 모아 dev PR 본문에 적음 (GitHub는 dev에 병합될 때만 이슈를 자동으로 닫는다)
- [ ] `integration/*` → `dev` PR은 Merge commit으로 병합
