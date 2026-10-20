# Handoff
## 지금 상태
- Task working, 전용 `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, `feat/ui-resources` → `integration/ui-resources`. 정식 인계·PR/병합 없음.
- PM 직접 승인 후 C6·C15 공개 `b16c3282338aa2cbb83eb1f7f000ac7fb2f61add` 커밋·push/원격 SHA 일치 확인. private `73fd48aa88bd5330c7b1837d94936fb063365a89` main push 완료·origin/main 조상 확인. 앞선 권한 차단 이력은 C15 상세에 보존, 우회 없음.
- 이어 C7 Guides 제작: 제공 private Runtime/Builder 원위치 GuidesV1 격리, 신규8 importer/재사용6 읽기 전용·소유 프리팹/독립 씬/전용 Static Medium·Preview17버튼/PlayMode16건. 이후 PM의 C7 커밋 요청에 따라 private8b49cc29/main38001b8e 저장·push 완료, 공개 결과/하네스/gitlink 저장은 DryRun PASS 후 실행 전 권한 검사에서 차단됨(현재 문맥의 요약 승인 대신 직접 확인 필요). 우회 없이 파일 보존.
- C7 원본54실파일/PNG19 전체 보존·누락0, 생산 시트14/14 RGBA 일치·보호baseline1208 SHA 변경0. 전수/상세는 [guides-inventory.csv](guides-inventory.csv), [guides-verification.md](guides-verification.md), 사실은 `Harness/Project/Facts/guides.md`.
- 원본 키 TMP영역28UI에서 실제 필요높이28.63 때문에 F가 비었던 문제를32UI로 보정했다. 키캡40/글자24/위치는 보존, 최초 테스트15/16 FAIL 이력 별도 보존·보정 후16/16 PASS.
- C7은 표시·모션·외부 bool 차단 API만 구현. 게임 입력 감지·바인딩/자동노출·우선순위 계산·실게임/네트워크 연결·삭제 없음. 공유 Dynamic TMP2개 과거 dirty/Buttons 과거 실패 미결은 이번 통과로 닫지 않는다.
- 무관 private 변경·unity_only·공개 `Assets/Scripts/Systems/Interaction 1.meta` 보존/저장 제외. 기존 정본 임의 복구/재작성/재생성 없음.
## 다음 할 일
1. 승인된 C7 공개 저장을 완료한 뒤 C9 captions를 별도 작업한다. C9 결과의 추가 commit/push는 요청 전까지 하지 않는다.
2. 사람 최종 외형 확인은 기능 완성 후 한 번에 진행. 독립 Guides 씬6모드·긴문구/차단·두 화면비/배경·시간 정지·초기화 확인, 공동/실게임과 구분한다.
3. 공유 폰트/Buttons 미결·DEV MODE 전역 배지·테스트 씬 처리·PR/통합은 별도다.
## Verification
- 구역 검사: PASS (10-20)
- 1 문서/규칙: PASS (10-20)
- 2 컴파일: PASS (10-20)
- 3 테스트: PASS (EditMode, 798/798, 10-20)
- C7 PlayMode16+기존 UI72=88/88 PASS, failed/skipped/inconclusive0. 실제입력70/70·화면48 자동 검사+최종 대비8화면·시간/예외/종료 복구·편집/기존출력 거부 통과. 첫 Tooltip19/20 정확 Vector2 비교 실패는1920 고정 재실행20/20 통과했지만 최초 원인 미확정·결과 보존.
- C7 실제 씬+정본 참조2684/missing0/Missing Script0·Image8/TMP2·Static Medium24/NoWrap·Sprite14/플랫폼98 override0·EventSystem/module 각1·Controls raycaster1·sorting1/10. 종료Guides 편집/clean·timeScale1·1920×1080·runInBackground 원래false·현재 Console오류0/경고5, 과거 버퍼 오류와 구분.
- 이전 C15 PlayMode20+회귀52=72/72·실제입력82/56화면·EditMode798/798 완료 이력은 [tooltips-verification.md](tooltips-verification.md). C6 PlayMode52/52·입력90/56상태/최종배치8화면은 [interaction-verification.md](interaction-verification.md).
- C7 doctor FAIL은 시작부터 보존할 private 변경으로 보고. CLI 문서 차이/skill-links WARN은 자동 수정·컴파일 근거로 사용하지 않았다. reload 설정 변경·both OFF 지원·전체 프로젝트 PlayMode·사람 외형/공동/네트워크는 미검증.
