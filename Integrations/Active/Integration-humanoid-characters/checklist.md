# Checklist

## 로컬 준비

- [x] 최신 origin/dev 기준 별도 worktree·작업 브랜치 생성
- [x] Task-20261019-062 발급·PM 직접 작업 권한 기록
- [x] private 외부 에셋 submodule 독립 초기화
- [x] 전용 빈 테스트 씬 준비
- [x] 전용 에디터 컴파일 검증
- [ ] 저장 요청 후 통합 준비/Task 문서를 커밋하고 원격 통합·작업 브랜치 등록

## 병합 순서

1. Task-20261019-062를 integration/humanoid-characters로 Squash PR.
2. 기능 완료 후 plan.md의 최대 3개 공동 테스트를 한 번에 수행.
3. 테스트 씬 처리·Task 마감 후 integration/humanoid-characters를 dev로 Merge commit PR.

## 병합 전

- [ ] 구역·문서·컴파일·자동 테스트 결과 기록
- [ ] 다른 진행 중 Task와 파일 충돌 조율
- [ ] 외부 에셋 private 저장 및 공개 포인터 정책 확인
- [ ] 공동 테스트 결과와 남은 이슈 기록
- [ ] PM의 별도 병합 승인
