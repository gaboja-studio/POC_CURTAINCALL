# Plan

## Context

브랜치 전환 뒤 외부 에셋을 받으면 다운로드 실패가 작업 폴더의 부분 전환으로 이어진다. 자동 잔여물 삭제와 meta 복구도 사용자 변경을 지울 수 있다.

## Approach

1. 대상 SHA 고정, 양쪽 상태·잠금 보호, public/private Git·LFS 사전 수신과 캐시 검증.
2. 명령별 재귀·자동 수신 억제 후 전환, 캐시 반영과 사후 검증. 자동 삭제·discard·rollback 금지.
3. 호출부 연결과 상세 진단. 미초기화·미지원 구조는 전환 전 차단.
4. 작은 격리 fixture 회귀, 기존 회귀, 단계별 검증. private 실적용과 B/C는 별도.

## Files

- 수정 예정: setup.md의 명시 구역.
- 읽기 전용 참고: 기존 third-party-scope 및 workspace-flow fixture.

## Risks

- 협조적 잠금은 외부 직접 Git을 막지 못하므로 HEAD 재검사와 작업 폴더 단독 사용 필요.
- 새 worktree/미초기화 안전 초기화는 이번 범위 밖, 명확히 차단한다.
- Unity Pipeline 연결 불가 시 컴파일 성공으로 보고하지 않는다.

## Auto Verification

- 구역 검사: verify-scope
- 1 문서/규칙: verify-fast
- 2 컴파일: check-work, 환경 차단 그대로 기록
- 3 테스트: 신규 안전 전환 fixture, 기존 scope 25 cases, workspace-flow
- 4 실행 로그: fixture의 성공·차단 결과, 실제 에디터 조작 없음

## 공동 테스트 항목

1. 정상: 작은 public/private LFS fixture 두 명목 브랜치 전환, gitlink·실파일 hash 일치.
2. 실패: dirty/잠금/수신 실패/캐시 손상 시 출발 HEAD·index·사용자 파일 불변.
3. 보존: legacy/meta·설정 보존, 부분 상태 판정, 실패 후 후속 작업 차단.

PM 본인 하네스 작업으로 자동 회귀 근거를 기록한다. 실제 팀 공동 테스트를 수행했다고 표시하지 않는다.
