# Handoff
## 지금 상태
- working / submit-work — 2026-10-15 사용자 기존 Task/PM 관리 문서도 함께 포함해 041 제출 승인. MPPM 설정3파일 제외, 042 구현/소유권 전환·PR 병합 없음. 봉 조작·Space·톱 변화 확인 완료, 관문·사망 인계 실제 확인은 이후 4인 플레이로 연기.
- 현재 PC 재개: fc6b306 수신 상태에서 CLI beta.6→beta.13 갱신, 사용자 Pipeline Stop/Start 후 C:\Unity\POC_CURTAINCALL ready 연결 복구. 이후 봉 입력·HUD 코드만 변경, 씬·공용 입력 변경 없음.
- 2026-10-15 PM 정정: Task 안에서도 한 기능 구현→사용자 확인→다음 기능 구현. 5Task 인계 완료는 내부 기능 일괄 구현 승인이 아니다. 담당 @RagSpirin.
- 040 수신 기준은 #93/8293bb9 → da4747c. 현재 작업 브랜치는 feat/human-cannon-pole-saw-gate, HumanCannon 코드·테스트 씬은 041 단독 소유. 2026-10-15 PM 요청으로 다른 PC 재개용 중간 저장; 041 PR·통합은 미실행.
- 구현 초안: 일반/연결 봉 조작·톱 수납→예고→재동작·호스트 점유/비행 식별자 동기화·사망/연결 해제/R 복구. 운반자 관문 통과 후 비행자 위/옆 통과 허용, 040 중앙 재반동·외곽 F 인계 재사용 및 봉 점유 중 중복 운반/반동/착지 차단.
- 승인한 기본 도형: 일반 봉·연결 봉·z32 톱·측벽·x±6/z35~40 받기 표시. 유지 8초·예고 2초 등 Inspector 조절값이며 최종 밸런스 아님. 받기 표시는 안전 쿠션이 아니다.
## 다음 할 일
1. 필수 관문 열기→운반자 통과→너머 다시 받기·우회 차단은 사용자 지시로 이후 실제 4인 플레이에 남긴다. 현재 PASS로 처리하거나 즉시 재확인을 요구하지 않는다. 재현·예상은 plan의 공동 테스트 2번 참조.
2. 사망 인계도 사용자 지시로 이후 실제 4인 플레이에 남긴다. 기존 소유권 해제·제자리 반동·공중 F 손잡이 인계 경로만 점검 완료. plan 공동 테스트 3번 유지, 실제 플레이 PASS 아님.
3. 최신 사용자 지시(2026-10-15)는 기존 관리 문서를 함께 포함한 041 제출이다. 이전 PM 선반영 대기를 대체한다. Assets38+관리/작업 문서35=73파일을 integration/main-stage-human-cannon에 제출하며 MPPM 설정3파일은 로컬 보존·제외한다. 042는 새 채팅에서 진행한다.
## PM이 확인할 것
- 교훈 제안(환경): 샌드박스 doctor는 Unity 설치/프로세스를 가려 미설치 FAIL을 낼 수 있다. 실제 설치는 D:\unity editor\6000.3.25f1이며, 권한 있는 조회로 확인했다. Pipeline 연결 정보 파일이 없던 상태는 CLI beta.13 갱신과 사용자 서버 재시작 후 복구됐다(파일 소실 원인은 미확정). 전역 CLI Facts 갱신은 PM 검토 대상.
- 현재 사용자 PM 지시로 쓴 5Task 인계·소유 전환 문서가 같은 작업 트리에 있다. 실제 승인 상태와 작업자용 구역 검사 결과를 구분한다.
- 이번 보관 대상은 현재 041 작업 브랜치다. 다른 준비 브랜치·integration으로의 문서 sync/병합은 별도이며 이 중간 저장으로 완료 처리하지 않는다. 로컬 전용 검사 파일은 다른 PC에 없으므로 필요한 검사는 다시 실행한다.
- 외팔 ReachMultiplier를 재사용(현재 기본 반경 1.5m), 받기 공간·조정값 제공. 실제 외팔 시간 여유·조작성·네트워크 플레이는 미검증.
- 공용 Systems/Resources/main_stage/Packages/ProjectSettings 수정 없음. Unity 자동 폰트 변경은 PM의 명시적 복원 승인으로 해당 파일만 복원했다.
## Verification
- 현재 PC 재개(2026-10-15): 최초 doctor FAIL(샌드박스 설치 미검출) 기록 유지, 재실행하지 않음. 설치/프로세스/CLI 조회로 6000.3.25f1 설치·프로젝트 일치·ready 확인. 사용자 gh 로그인 후 setup-gh Ready, 작성자 RagSpirin/37629846+noreply·커밋 훅 설정 완료. skill-links 경고 미해결.
- 현재 PC 전환 준비 검증(2026-10-15): 컴파일 PASS(up_to_date), EditMode118/118 PASS(이전113과 별도 실행), verify-fast PASS(hard0/기존 WARN5), diff --check PASS. Scene root11/dirty0. 최초 Space 변경6파일 scope PASS였으나 현재 구역 검사는 사용자 PlayMode 설정3파일과 setup 요약 변경으로 FAIL4; 에이전트의 setup 요약 변경은 제거했고 사용자 설정은 보존한다. 실제4인·관문·사망 인계는 미실행.
- Console 조회: 오류0/경고137/compilationFailed=false. Synty FBX 자기 교차 면 제외136건, PolygonFarm 텍스처 2K/2k 이름 차이1건. 외부 원본/패키지 수정 없음. 아래 저장·113개 테스트는 이전 PC 이력이다.
- 제출 검사(2026-10-15): check-work -Mode PlayMode는 관리 문서30+로컬 MPPM3으로 scope FAIL33·후속 SKIPPED. 최신 사용자 문서 포함 승인으로 제출73파일을 별도 대조하며 자동 PASS로 바꾸지 않는다. 개별 재검증 컴파일 PASS(up_to_date)·PlayMode NO_PROJECT_TESTS, 앞선 동일 코드 EditMode118/118 PASS. MPPM은 PR에서 제외되므로 CI 구역 검사에는 관리 문서30개가 남는다.
- 2026-10-15 사망 인계 소스 점검: 호스트가 운반자 사망/클라이언트 이탈 시 소유권 해제·마지막 장치 위치 전파, 무주인 중앙 반동과 생존자 F 손잡이 인계 경로 확인. 명확한 오류 미발견, 게임 코드 수정·실제 플레이·테스트 재실행 없음. 호스트 접속 종료로 방이 Offline이면 초기 위치 복귀 경로다.
- 이전 검증: verify-fast PASS(hard0/범위 밖 WARN5), diff --check PASS, 컴파일 PASS(completed→저장 직전 up_to_date), EditMode113/113 PASS(기존64+신규49), PlayMode NO_PROJECT_TESTS. 사람 Play·실행 로그·4인 SKIPPED. Scene root11/dirty0/Missing Script0·봉2·8초/2초 확인. 합성 검사는 실제 입력 PASS가 아니다.
- 이전 근거: .harness/local의 test-results-editmode.json·test-results-playmode.json·task041-scope-review.json·task041-scene.png(로컬 전용). 기존742객체 보존·새 관문 루트만 추가, 재질4개·스크립트 .meta 짝 확인. 초기화 교훈: Harness/Engine/Unity/Pitfalls/Initialization/unity-api-in-constructor.md. 문서 검사 샌드박스 실패는 권한 있는 재실행으로 통과; eval 5초 timeout은 20초 재조회로 성공.
## 공동 테스트 결과
- 일시·참여자·실제 결과: 미실행.
- 재현: 봉 F→W/S→F hold/시간 만료/사망; 관문 열기→운반자 통과→트램폴린 받기; 담당 사망→중앙 재반동→외곽 F 인계. R로 회차 초기화.
- 예상: 수납/예고/재동작·필수 순서·인계가 4화면에서 일치. 상세 조건·영상/로그 기록 방법은 plan.md.
