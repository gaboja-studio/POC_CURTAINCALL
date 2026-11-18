# Log

- 2026-11-18: dev clean, fetch/ff-only 최신 56c05863. 다른 작업 fix-main-stage-pieces 에디터들은 유지. 이 프로젝트 에디터를 새로 열어 검증.
- doctor 1회: 기존 Task ID 중복 FAIL, Unity 설치 탐지 FAIL(CLI 실제 6000.3.25f1 열기 성공), CLI 문서 버전 WARN. 서브모듈/작성자 정상. 이번 작업에 필요한 실행 가능.
- investigate-bug 및 기존 unity-pipeline 절차 적용. 초기 가설: 로비를 건너뛴 참가자에게 SpaceLinkDirector가 없어 sceneLoaded → CampaignPerformanceEntry.Prepare가 호출되지 않음.
- 실제 MPPM 수정 전: Host PID35824 / Player2 PID46740, LAN17934. 대기실 P2 정상 배치이나 Director 없음. 외줄 공연에서 양쪽 Performance/Tightrope/2인 수신, 호스트 Entry/LocalPlaced=true, 참가자 Director/Entry/LocalPlaced=false 및 출발점 오차75.42m. 참가자에게 기존 Prepare만 1회 호출하자 Entry/LocalPlaced=true, 오차0.08m로 정상 복구. 초기화 누락을 확정했다.
- 수정: CampaignPerformanceEntry가 BeforeSceneLoad에서 자체 sceneLoaded 훅 등록. 씬 전환 권한 관리자 없이 각 화면의 기존 배치 경로를 호출하고 Prepare의 중복 보호/게임 상태·오프라인 조건은 유지한다.
- 첫 회귀 묶음 4/6 PASS(신규 관리자 없는 NGO 씬 로드 포함), 기존 2건은 ServerSetState/ServerKill에 추가된 선택 인자를 리플렉션 테스트가 생략해 실패. 테스트 호출에 Type.Missing을 명시해 현재 API에 맞춤(게임 판정 변경 없음).
- 선택 인자 호출 보정 뒤 배치 관련 PlayMode 6/6 PASS(27.09초). 독립 공연 테스트도 오래된 StartSession 선택 인자 호출 보정 뒤 PASS(11.72초). 단독 씬은 기존 Bridge로 실행/정산하며 불필요한 Entry를 생성하지 않음을 확인.
- 추가 MPPM 새 세션 Start→공연 직행에서 첫 스냅샷 수신 전 Prepare가 조기 반환하는 경로 발견. 네트워크 공연 씬에 진입 컴포넌트를 만들고 InitializeIfReady가 캠페인 수신을 기다리도록 보완. 늦은 스냅샷 합성 회귀 추가 후 배치 7/7 PASS(31.75초).
- 최종 코드 독립 공연 회귀 PASS(11초). 실제 MPPM 최종 Host35824/Client27496 새 세션 직행에서 client sceneLoaded 순간 Playing/캠페인 None/Entry=true/initialized=false를 읽기 전용 기록. 상태 수신 뒤 자동 LocalPlaced=true, 양쪽 Normal/오차0.08m. 15초 관찰 호스트738프레임/참가자795프레임, 정상·배치·2인 유지, 오류0. 외줄 동일 씬 재이동도 Entry1개/조작 가능/Running/오차0.08m 확인.
- 인간대포 전환도 양쪽 Normal/조작 가능/오차0m, 15초 호스트584프레임·참가자635프레임 모두 배치/생존/2인 유지·오류0. 대기실 복귀 오차0.02m, 원래 대기실→외줄 실패 경로 재검사도 양쪽 Normal/조작 가능/Running/오차0.08m. 참가자 Director는 끝까지 생성하지 않았고 수동 위치 보정 없이 통과. 자세한 결과는 verification.md.
- check-work 1회에서 새 원인 기록 문서가 62줄로 제한60줄을 초과해 실패. 내용을 보존하며 60줄로 압축한 뒤 verify-fast OK(hard0), verify-unity Compile PASS/up_to_date, Common settings PASS. 자동 테스트는 별도 실행한 최종 코드 8/8 PASS 결과를 사용했다.
- MPPM 종료 및 기존 실행 설정 복원, 임시 시나리오 삭제. 대상 에디터만 정상 종료 후 검증으로 생성된 폰트 캐시4개·재질 반올림8개 원복. 사용자 씬/게임 설정은 변경하지 않았다.
- 제출 전 origin/dev fetch: 26c4cdb9까지 fast-forward 반영. 새 탈출로 에셋 변경은 이번 공연 배치 코드/검증 대상과 겹치지 않는다. 변경된 외부 에셋 gitlink도 깨끗한 서브모듈에 반영한다. 사용자 요청대로 dev에서 직접 commit/push하며 PR은 생성하지 않는다.
