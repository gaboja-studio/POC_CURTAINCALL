# 042 제출 검증 — 2026-10-16

- 사용자: "전부 확인 완료 했고 제출해줘". 분리 출발/첫 받기/쉼터/불꽃·리프트·톱/봉·손잡이 표시의 직접 확인 완료로 기록한다. 인원·역할·원격 환경·영상은 추가로 제공되지 않아 실제4인 공동 테스트 증거를 임의로 만들지 않는다.
- 대상: feat/human-cannon-route-shelter → integration/main-stage-human-cannon, Squash. git fetch 후 부모 최신292667c 확인, merge는 Already up to date. 043/044 이관·PR 병합은 이번 제출에 포함하지 않는다.
- 제출: [PR #111](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/111), 구현87f383f push 완료. 요청된 검증 결과와 기존 scope FAIL8/위임 근거를 본문에 기록했다. 병합은 미실행이다.
- 작성자: gh 계정 RagSpirin / 37629846+RagSpirin@users.noreply.github.com 일치, setup-gh PASS, 기존 커밋 검사 훅 유지.
- check-work -Mode PlayMode 및 save-work -DryRun은 전체 브랜치 scope FAIL8로 중단한다. 실패 결과를 PASS로 바꾸지 않는다.
- FAIL8은 a18d6aa에 저장된 통합 문서3개, 041 인계 문서4개, 042 setup1개다. [PM 순차 인계 위임](../../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)에 승인된 단계 전환이며 이번 세션에서 해당8개는 수정하지 않았다.
- 증분 scope(-Base HEAD)는 코드/씬/Task 문서 전부 PASS. 이미 승인된 전환 문서를 포함하는 전체 자동 판정과 구분한다. 기존 위임·현재 제출 요청에 따라 직접 Git 저장과 PR 스크립트로 제출하며 훅/서명/검사 스크립트는 우회·변경하지 않는다.
- 문서: verify-fast PASS(범위 밖 기존 WARN5/hard0), diff --check PASS.
- 컴파일: 명시적 프로젝트를 지정한 Pipeline recompile/recompile_status up_to_date·failed=false·errors=[] PASS. status 검색 호환성 문제로 verify-unity 성공으로 표기하지 않는다.
- 최종 전체 EditMode277/277 PASS(실패/건너뜀0), PlayMode0개 NO_PROJECT_TESTS. 기존 합성 톱/봉 및 손잡이 표시 검사는 별도 이력 참조.
- 에디터: 해당 HumanCannon 씬 저장/Stop/dirty0 확인. 마지막 콘솔 오류2건은 UnityEditor.SceneViewMotion 내부 NullReferenceException(게임 코드 프레임 없음)으로 이전 손잡이 이력에 기록돼 있다. 전체 Console 오류0으로 표기하지 않는다.
- 로컬 증거: .harness/local/task042-submit-{compile,editmode,playmode}.json. 직접 플레이는 사용자 보고, 자동 검사와 구분한다.
