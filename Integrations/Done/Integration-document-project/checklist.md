# Checklist

## 병합 전

- [ ] 두 Task의 setup.md 작업 공간 확정 및 인계 완료
- [ ] 모든 Task PR이 integration/document-project 대상으로 제출됨
- [ ] 구역·문서 검사 결과와 Unity 검사 생략 사유가 각 handoff.md에 기록됨
- [ ] 공용 파일 소유 표와 실제 변경 경로에 충돌 없음

## 병합 순서

1. 프로젝트 overview 및 내용 문서 Task.
2. Assets 폴더 규칙 Task. 선행 의존성은 없으며 PM이 순서를 바꿀 수 있다.

## PR 하나를 병합할 때마다

1. Task PR을 Squash로 병합한다.
2. 다음 PR의 기준을 갱신하고 충돌은 담당자와 PM이 해결한다.
3. 씬·프리팹 YAML 충돌은 AI가 해결하지 않는다.
4. 문서 변경만 있는 경우 컴파일 생략 사유를 기록한다. Assets 변경이 있다면 Unity 검사도 수행한다.

## 공동 테스트 항목

| Task | 항목 | 결과 |
|---|---|---|
| overview Task | 확정 정보·TBD 구분 및 개요 탐색 가능 | 미실시 |
| Assets 규칙 Task | 현재 구조 대조 및 재점검·갱신 절차 재현 | 미실시 |

## dev로 PR 전

- [ ] 각 Task plan.md의 공동 리뷰 항목 통과
- [ ] Task 폴더를 Tasks/Done/으로 이동
- [ ] 테스트 씬 해당 없음 기록 확인
- [ ] 작업 브랜치 및 Task PR 정리 여부 확인
- [ ] 관련 이슈 종료 참조를 dev PR에 기재
- [ ] integration/document-project → dev는 Merge commit으로 병합
