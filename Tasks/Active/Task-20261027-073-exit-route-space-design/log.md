# Log

- 이전 공간 제작·수직 공동·브루탈리즘·사용감 보완 기록: [History/001-space-design-history.md](History/001-space-design-history.md).

## 2026-10-28 작업 재개 환경 최신화
- 사용자: 여기서 다시 작업할 수 있도록 최신 환경 요청. 실제 Unity 프로젝트 C:/Unity/POC_CURTAINCALL, feat/exit-route/design-map, 저장된 A 씬과 Editor 연결 확인.
- 원격 fetch 후 작업 브랜치는380fc5e로 origin과0/0. origin/dev에는 추가10개 커밋(c203582)이 있으며 공용 게임 규칙/구매·소지/개발씬 정리 등이 포함되어 있어 통합 범위를 사용자에게 질문했다. 답변 전 dev 병합/커밋/푸시하지 않음. merge-tree 사전 확인은 충돌 없이 완료.
- 재개 시 doctor: third-party HeadMismatch FAIL, CLI 문서 버전 차이/선택적 Claude 스킬 링크 WARN. 실제 외부 에셋 내부 수정은 없었고 로컬1a619e83에서 지정86fc532c로의 차이는 Test 머티리얼3파일이었다. 원본 작업 보존 확인 후 submodule update로 지정 버전을 맞춰 HeadMismatch 해소.
- start-work.ps1이 기존 중첩 브랜치명에 잘못 거부하던 순서를 수정: 같은 Task/현재 브랜치 재개 판단을 새 브랜치 이름 검증보다 먼저 한다. 새 브랜치 생성 이름 정책은 유지. 직접 실행으로 재개 성공 확인.
- 기존 공용 TMP 폰트·Multiplayer/Vivox 설정 변경3건은 보존. Unity6000.3.25f1 연결 ready, A dirty0. 별도 Unity 컴파일 PASS(up_to_date), 자동 플레이/이동 시험 없음.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-28 작업 브랜치 재최신화
- 사용자 정지 후 새 요청에 따라 현재 작업 브랜치만 원격5ecd7cc(탈출로 A 사용감과 조명 가독성 개선)로 fast-forward. origin과0/0 확인. dev 통합/마지막 질주 제작은 재개하지 않았다.
- 로컬 로그만 원격 변경과 겹쳐 경로 한정 stash로 보관 후 적용. 문서의 append 충돌은 양쪽 기록을 모두 보존해 해결했고 미해결 index0, 기존 미커밋 스크립트/폰트/설정 변경 보존. 원래 stash도 복구용으로 유지(.harness/local/exit-route-update-stash.txt에 SHA).
- Unity에서 미저장 변경 없는 A 씬을 재개방. recompile_status completed/failed=false/errors=[], 추가18 그룹 로드와 참조 상태 확인. 자동 Play·이동 입력 없음. 새 커밋/푸시 없음.

## 2026-10-28 — A 마지막 질주 설비동
- 요청: 긴급하게 달리며 회피·그네 횡단을 하는 마지막 맵, 더 긴 달리기 구간, 보이는 출구, 전시 공간처럼 깔끔한 인상 개선.
- 변경: `19_RunnerEscape`에 X140–400/Z78의260m 진행축. 가속24m→측면 배출62m→상부 낙하50m→그네 단절22m→회복/마지막 질주68m→외벽34m. 배출4/낙하3/그네 설치3곳과 관련 앵커. 22m 단절 제외 주행 바닥238m.
- 공간: 하부48m, 서로 다른 천장 높이와 폐쇄 전이벽, 큰 원동기/집진기, 비틀린 보·손상 슬래브/철근·잔해·늘어진 케이블·국소 오염. 중성 콘크리트와 밝은 외부 대비 유지.
- 보존: 17의 기존07 및 관련18 소품 비활성 보존, A01–06/B/C 유지. 08 외부를 절대 오프셋(190,0,59.25)으로 새 출구에 정렬. 원본 에셋/재질·기존 로컬 폰트/설정 변경 보존. 수정 전 씬은 `.harness/local/runner-escape/escape-route-before.unity`에 백업.
- 검토: Scene View 눈높이 진입/낙하/출구와 높인 그네 깊이 시점 확인. 초기 천장 단차에서 보인 하늘 틈을 폐쇄 벽으로 수정. 최종 씬 저장·재개방 후 A01–06 81항목 PASS, 새07 394항목 PASS, 기존03 Failures=0. 저장된19 transforms1187/renderers931/colliders169, 누락 스크립트/재질/비영속 메시0. 정적 검증 보고서 `.harness/local/runner-escape/validation.txt`.
- 구현 범위: 배경·충돌·기믹 배치 앵커. 실제 발사/낙하 타이밍/그네 조작/피해/추격/탈출 판정은 미구현. AI 자동 플레이/이동 입력 없음. 실제 속도·묘기 통과·성능은 사용자 확인 필요.
- 참고: `a-runner-route.md`. 최종 캡처 `a-runner-entry-final.png`, `a-runner-loading-final.png`, `a-runner-gap-final.png`, `a-runner-exit.png` (Codex visualizations의 기존 탈출로 폴더).
- 커밋/푸시는 요청받지 않아 실행하지 않음. 묶음 check-work 결과는 아래에 기록.

- 2026-10-28 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work: 안전성/문서·규칙/Unity 컴파일 PASS, 자동 테스트 SKIPPED. 문서 길이 경고14(기존12+이번 Task log/todo2), hard0. SurfaceWear 셰이더 오류0, 작업 범위 git diff --check 오류0.

## 2026-10-28 — 마지막 질주 작업 저장 요청
- 사용자 저장 승인에 따라 A 씬·전용 메시/머티리얼/제작 코드·관련 Task/지식 문서를 경로 한정으로 커밋/푸시한다. 공용 LiberationSans 폰트·Multiplayer/Vivox 설정·로컬 start-work.ps1 수정은 이번 저장에서 제외한다.
- 현재 Unity Pipeline은 종료 상태다. 씬은 직전 제작 완료 시 저장/재개방·dirty0 확인했으며 마지막 검사 뒤 씬/코드/에셋 수정은 없다. 이후 변경은 log/todo의 검사 결과 기록뿐이다. 직전 Unity 컴파일 PASS와 정적 검사 결과를 재사용하고 문서/규칙 검사를 갱신한다. 자동 테스트는 SKIPPED.
- 보조 읽기 검토: 신규64개 에셋/스크립트에 .meta 존재, 신규 GUID 중복0, 새 메시·재질60개 모두 저장 씬에서 참조됨. 원격 작업 브랜치와 HEAD 0/0 및 작성자 @RagSpirin 확인.
- 저장 전 로그 길이 제한으로 발생한 문서 검사 실패는 compact-docs에 따라 과거 원문을 History/001·002로 보존 이동해 해소했다. 미완료 사용자 검토 항목6개는 유지했으며 현재 Task 길이 경고0/hard0이다.
