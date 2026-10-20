# Handoff
## 지금 상태
- 최신 PM 후속 요청(2026-10-20): 작업 외 에셋까지 전체 commit/push 승인. 이전 선택 저장은 공개 실행 전 권한 차단되어 HEAD2fe68a2/선택 인덱스를 보존했고, 이번 직접 요청으로 전체 저장을 진행한다. 공유 Bold·Interaction 1.meta·private 키트4종/Leartes 및 기존 공급 방향 정책3파일 포함. `feat/ui-scene-integration`은 사용자가 직접 수신하며 해당 worktree/브랜치는 수정하지 않는다. 수신 절차·최종 검사 결과는 `ui-resources-pull.md` 참조. 아래 무관 변경 제외·저장 대기는 이전 요청 당시 이력이다.
- 최신(2026-10-20): 선행 공개2fe68a2/private6b4e2041 저장 완료. 후속 사용자 버그 수정/계속 요청에 따라 Schedule C#7 namespace만 격리, 중복21개 해소·정식 컴파일 PASS. C11 Static8192 재사용 안전복구로 Canvas 프리팹/독립 씬 생성·비활성 콜백 보강 후 신규26+선택50=PlayMode76/76 PASS. 합성 입력72/72·두 화면비/명암24캡처 확인·check-work/EditMode798/798 PASS. 종료 C11 clean/Play stopped/timeScale1/1920×1080/runInBackgroundfalse, QA 장치0·입력 정책 값 복구. 참조3319/missing0/스크립트누락0·씬 IsConfigured=true. 후속 사용자 "저장해줘" 요청 적용: private334파일1d3bee7d/README 병합11781eb2 main push·원격 확인 완료, 공개 C11 결과·기록/gitlink를 이 기록과 함께 feat/ui-resources에 저장한다. 무관 변경 제외. 상세 `spectator-verification.md`.
- 과거 저장 차단: 월드 라벨 공개42파일 선택 stage·DryRun(구역/문서/컴파일 PASS) 후 commit/push 호출이 실행 전 권한 검사에서 거부됐다. 요약 속 승인만으로 저장 경계를 해제할 수 없다는 사유이며 공개 HEAD는9ede8a3 유지. 파일/인덱스 보존·다른 도구 우회 없음. private main6ff6e18e 저장은 실제 완료. 저장→C11 순서 때문에 C11 구현은 시작하지 않았고 원본/명세·PNG 중앙 투명/SHA 읽기 전용 확인만 했다.
- 최신 월드 라벨 v2: 5프리팹·Static Medium/Bold·독립 씬 제작. PM 저장/C11 요청 및 settings 최소 수정 승인 후 타입 충돌 해소, check-work 컴파일·EditMode798/798 PASS. private main6ff6e18e push/원격 일치 확인, 보정 후 입력68/68·신규/선택 PlayMode86/86 PASS, 공개 결과/기록 선택 stage 후 권한 차단(위 최신 상태). 상세 `worldlabels-verification.md`; 삭제/실게임 연결 없음.
- C9는 private116파일 `bab2b062`·README 일반 병합 `2b67e36d` main, 공개 `9ede8a3` feat/ui-resources commit/push·원격 SHA 일치 확인 완료. 공유 Bold/Interaction 1.meta/무관 private 제외, 아래 C9 미커밋 표현은 당시 이력이다.
- Task working, 전용 `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, `feat/ui-resources` → `integration/ui-resources`. 정식 인계·PR/병합 없음.
- PM 직접 승인 후 C6·C15 공개 `b16c3282338aa2cbb83eb1f7f000ac7fb2f61add` 커밋·push/원격 SHA 일치 확인. private `73fd48aa88bd5330c7b1837d94936fb063365a89` main push 완료·origin/main 조상 확인. 앞선 권한 차단 이력은 C15 상세에 보존, 우회 없음.
- 이어 C7 Guides 제작: 제공 private Runtime/Builder 원위치 GuidesV1 격리, 신규8 importer/재사용6 읽기 전용·소유 프리팹/독립 씬/전용 Static Medium·Preview17버튼/PlayMode16건. 이후 PM의 C7 커밋 요청에 따라 private8b49cc29/main38001b8e 저장·push 완료, 공개 저장은 최초 차단 후 PM 직접 재승인으로9216489 commit/push·원격 SHA 일치 확인 완료. 이어 C9 독립 조립·검증·기록 진행(추가 저장 없음).
- C7 원본54실파일/PNG19 전체 보존·누락0, 생산 시트14/14 RGBA 일치·보호baseline1208 SHA 변경0. 전수/상세는 [guides-inventory.csv](guides-inventory.csv), [guides-verification.md](guides-verification.md), 사실은 `Harness/Project/Facts/guides.md`.
- 원본 키 TMP영역28UI에서 실제 필요높이28.63 때문에 F가 비었던 문제를32UI로 보정했다. 키캡40/글자24/위치는 보존, 최초 테스트15/16 FAIL 이력 별도 보존·보정 후16/16 PASS.
- C7은 표시·모션·외부 bool 차단 API만 구현. 게임 입력 감지·바인딩/자동노출·우선순위 계산·실게임/네트워크 연결·삭제 없음. 공유 Dynamic TMP2개 과거 dirty/Buttons 과거 실패 미결은 이번 통과로 닫지 않는다.
- 무관 private 변경·unity_only·공개 `Assets/Scripts/Systems/Interaction 1.meta` 보존/저장 제외. 기존 정본 임의 복구/재작성/재생성 없음.
## 다음 할 일
1. C11 사람 최종 외형은 Spectator 확인 씬에서 두 화면비/명암·중앙 투명/상태판/키 가독성·전환/퇴장을 한 번에 확인한다. 자동 검증과 별도이며 실게임 연결·전용font/기존 출력 삭제·재생성/덮어쓰기·이번 후속 저장 요청 외 PR/통합 병합은 요청되지 않았다.
2. 사람 최종 외형 확인은 WorldLabels 씬에서 5종·기본/최소·명암/화면비·Observer/표면/회전·초과 거부를 한 번에 진행한다. 이전 Captions/C7 외형과 공동/실게임은 별도다.
3. 공유 폰트/Buttons 미결·DEV MODE 전역 배지·테스트 씬 처리·PR/통합은 별도다.
## Verification
- 월드 라벨 최초16/18 실패→Shelf 최소 세로여백8 보정/Destroy 검사 순서 수정→18/18 PASS. 마지막 Preview 배치 보정 전 두 화면비 Mouse44+Enter24 통과. 후속 settings 최소 수정 후 최종 check-work는 아래 PASS이며 보정 후 입력/선택 회귀는 별도로 기록한다.
- 원본161 누락0/PNG29 변경0·재사용7/7·보호838 SHA 변경0, 정본 참조882/missing0/Missing Script0. 종료 WorldLabels clean/Play stopped·timeScale1·1920×1080·runInBackgroundfalse; settings 중복 타입은 PM 최소 수정 승인 후 해소했다.
- C9 신규 PlayMode16/16·화면56/56·실제입력78/78·timeScale0.37 복구/Play 종료1·편집 보호·C7 동시16UI 통과. 선택한 UI 회귀104건 중103 PASS/1 FAIL(TooltipPlayTests.cs:323 정확 Vector2 비교, 원인 미확정·첫 결과 보존). 전체 회귀 PASS 아님.
- C9 최종 구조 참조2954/missing0·Missing Script0·Image9/TMP2·StaticMedium·EventSystem/module/raycaster 각1. 원본115 누락0/승인 cs3만 변경·PNG13 변경0/시트9 RGBA 일치·보호1534 SHA 변경0 PASS. 공유 Bold의 HEAD 차이는 최초 baseline부터 존재, 임의 복구 없음.
- C9 종료 Captions 편집/clean·Play stopped·timeScale1·1920×1080·runInBackground 원래false·현재 Console오류0/경고3. 당시 check-work4단계 PASS(아래는 최신 월드 라벨 실행 결과), 지식/문서 검사 PASS(기존 무관 WARN7/hard0). 상세 [captions-verification.md](captions-verification.md).
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: PASS (EditMode, 798/798, 10-20)
- C7 PlayMode16+기존 UI72=88/88 PASS, failed/skipped/inconclusive0. 실제입력70/70·화면48 자동 검사+최종 대비8화면·시간/예외/종료 복구·편집/기존출력 거부 통과. 첫 Tooltip19/20 정확 Vector2 비교 실패는1920 고정 재실행20/20 통과했지만 최초 원인 미확정·결과 보존.
- C7 실제 씬+정본 참조2684/missing0/Missing Script0·Image8/TMP2·Static Medium24/NoWrap·Sprite14/플랫폼98 override0·EventSystem/module 각1·Controls raycaster1·sorting1/10. 종료Guides 편집/clean·timeScale1·1920×1080·runInBackground 원래false·현재 Console오류0/경고5, 과거 버퍼 오류와 구분.
- 이전 C15 PlayMode20+회귀52=72/72·실제입력82/56화면·EditMode798/798 완료 이력은 [tooltips-verification.md](tooltips-verification.md). C6 PlayMode52/52·입력90/56상태/최종배치8화면은 [interaction-verification.md](interaction-verification.md).
- C7 doctor FAIL은 시작부터 보존할 private 변경으로 보고. CLI 문서 차이/skill-links WARN은 자동 수정·컴파일 근거로 사용하지 않았다. reload 설정 변경·both OFF 지원·전체 프로젝트 PlayMode·사람 외형/공동/네트워크는 미검증.
