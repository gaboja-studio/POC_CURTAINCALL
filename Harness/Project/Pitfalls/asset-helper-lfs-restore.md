---
summary: 에셋 stash 복구 때 LFS 포인터 텍스트 대신 실제 바이트를 복원한다
status: active
updated: 2026-10-27
source: Task-20261027-072 local Git/LFS fixture
---

# 에셋 보관 복구와 LFS

## 언제 읽나
에셋 도우미의 보관·복구나 Git 환경 변수를 바꿀 때.

## 함정
- 안전 다운로드용 GIT_LFS_SKIP_SMUDGE=1을 stash apply에도 적용하면 수정한 LFS 에셋이 실제 파일 대신 포인터 텍스트로 복구될 수 있다.
- stash의 Git blob은 LFS 포인터인 것이 정상이다. 보관 검사에서는 pointer의 oid를 확인하고, 복구 검사는 실제 파일 바이트를 확인한다.

## 대처
- stash apply 작업에만 GIT_LFS_SKIP_SMUDGE=0을 사용해 캐시의 실제 파일을 복구한다.
- 보관 branch·stash ref는 복구 뒤에도 지우지 않는다. untracked 파일과 인덱스 상태도 함께 확인한다.
- `Scripts/Tests/asset-helper.test.ps1`의 LFS 보관·복구·중단 재시도로 검사한다. 실제 private 저장소를 테스트 대상으로 쓰지 않는다.
