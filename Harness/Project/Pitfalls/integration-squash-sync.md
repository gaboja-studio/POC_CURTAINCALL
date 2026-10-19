---
summary: 통합 동기화를 Squash하면 부모 계보가 끊겨 이미 받은 변경도 재충돌한다. 인간 대포 상향 병합과 생존 규칙 테스트를 확인할 때
status: active
updated: 2026-10-16
source: human (2026-10-16 PM 상향 병합·고도화 3브랜치 동기화 요청)
---

# 통합 Squash 동기화의 재충돌

## 언제 읽나

공용 변경을 이미 받았는데 통합 상향 병합에서 같은 파일이 충돌하거나, 생존 규칙 문서와 실제 동작이 다를 때.

## 증상·원인

- 인간 대포 #134(`cbc37ad`), 공연장 #135(`f6b7bb9`)는 sync PR이지만 실제 커밋 부모가 1개다. Merge commit이 아니어서 원본의 조상 관계가 보존되지 않았다.
- 부모/자식 병합 시 `DummyPerformancePlayTests.cs`가 재충돌했다. `distance-label.mat`은 별도 GUID인데 삭제된 `dummy-platform.mat`의 rename으로 판정되었다.
- 공연장/당시 dev의 `PerformanceRewards`에는 `DevMode.MinSurvivors(2)`가 남아 있었다. 인간 대포의 정식 생존 1명 테스트만 가져오면 7개 중 5개가 실패한다.

## 대처

- 통합 상향/하향은 기존 정책대로 `--merge`를 명시하고 병합 결과 커밋의 부모 2개와 원본 조상 관계를 확인한다.
- 충돌 정리 시 인간 대포 원본 머티리얼과 `.meta`를 함께 보존한다. 씬·프리팹 YAML 충돌은 AI가 해결하지 않는다.
- 정식 생존 1명 규칙은 테스트뿐 아니라 `PerformanceRewards.OfficialMinSurvivors`, 개발자 모드 설명/UI까지 같은 원본으로 맞춘다. 시작 인원 규칙은 바꾸지 않는다.
- 작업 브랜치 배포는 [space-branch-update-without-pr.md](space-branch-update-without-pr.md)를 따른다. force/reset으로 계보를 덮지 않는다.

## 확인 방법

- `git merge-tree --write-tree --name-only <부모> <자식>`으로 사전 충돌 검사.
- `git merge-base --is-ancestor <원본 SHA> <수신 브랜치>` 성공, 인간 대포 코드·씬 tree 동일 여부 확인.
- 컴파일 및 `DummyPerformancePlayTests` PlayMode 회귀를 실행한다. 실제 4인 검증과 자동 테스트 통과는 구분한다.
