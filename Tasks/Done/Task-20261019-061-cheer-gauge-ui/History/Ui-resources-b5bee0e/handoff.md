# Handoff
## 지금 상태
- 2026-10-21 Task working·PM 자체 작업. 전용 worktree `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, `feat/ui-resources` → `integration/ui-resources`. 다른 worktree/실게임 씬 연결·PR/병합 없음.
- 현재 타이틀 로고: `Assets/Resources/Prefabs/UIs/TitleLogo/TitleLogo.prefab` 정적 프리팹1개·본체/그림자 Image2 조립. 메인768×384/위치576,-48·그림자0,-3/alpha.32, 본체white. 외부 Canvas/1920×1080 LayoutStage용이며 자체 Canvas/버튼/런타임 스크립트 없음.
- 실제 replacement_v2 신규 Sprite2 importer만 보정·원본 PNG/GUID/전체 자료 보존. 보호560파일 누락0/승인 importer2 외 변경0, 두 화면비·명암·3배치12실행캡처/mesh24/비차단24·참조39 누락0 검증 완료. doctor FAIL은 제공 키트 미추적으로 보고/보존, CLI 버전/skill-links WARN2는 자동 수정하지 않았다.
- 후속 사용자 "좋아 커밋 푸시 해줘." 요청으로 타이틀 private 키트/meta와 공개 프리팹/검증·지식 기록/gitlink 저장·push 승인/진행. 이전 제작 당시 저장 제외만 대체, 메뉴 연결/런타임/영구 테스트 씬·삭제·PR/병합 제외는 유지. 임시 QA는 scratch에서만 실행했다.
- 선행 C12 공개5613dcc/privatef60ae144 저장·push/원격 확인 완료. TimeManager 단일 파일 포함은 사용자 명시 승인으로 해결했으며 범용 ProjectSettings 수정 권한이 아니다.
- 선행 W3 공개0707167/private23b9d0a7·W2 공개3b2a3a4/private42477d6f·C14 공개6e5fb38/privatee1ddf391·C16 공개f9e8666/private76a7e908 저장 완료. 이전 문서의 저장 진행/차단은 당시 이력이다.
- C12 패널640/부제목128/제목 여백28 적용. 최종 간격 기하1/1/check-work Edit798 통과, 전체28/28·회귀55/55는 간격 변경 전이다. 상세 [results-ui-verification.md](results-ui-verification.md).
- W3 20/20·W2 35/35·C14 23/23·C16 16/16 검증 이력과 실패/보정/캡처는 각각 schedule-ui/shop-ui/settings-ui/curtain-verification.md를 따른다. 이번 타이틀의 회귀 결과로 재주장하지 않는다.
- C11/월드 라벨/C9/C7/C6/C15 이전 검증·저장·권한 차단은 해당 verification 문서와 plan/meta/History에 보존한다. 완료 체크 원문은 [History/005-todo-completed-ui-before-title-logo.md](History/005-todo-completed-ui-before-title-logo.md).
## 다음 할 일
1. 타이틀 제작·검증 완료, private65파일 f32c85f2 main push·원격 SHA 일치·clean 확인. 공개 프리팹/기록/gitlink는 이 기록과 함께 저장하며 최종 push는 원격 SHA/최종 보고로 확인한다. 사람 외형/실게임 연결은 별도. 상세 [title-logo-verification.md](title-logo-verification.md).
2. 사람 최종 외형/손맛 확인은 기능 완성 후 한 번에 진행한다. 기존 UI와 타이틀의 물리 장치·실기기/공동·실게임 연결·네트워크는 자동 검증과 별도다.
3. 공유 Dynamic TMP2개 기존 dirty/추가 SHA·Buttons 글리프 실패, Tooltip 정확 Vector2 비교 회귀1건은 미결이다. 임의 복구/폰트 재작성하지 않으며 이번 로고 통과로 닫지 않는다.
4. 백스테이지/로비 외형 승인 범위·DEV MODE 전역 배지·테스트 씬 유지/삭제 결정·PR/통합은 별도다. 다른 worktree·메인 원본 수정/무관 변경 저장 금지.
## Verification
- 최초 타이틀 check-work: 구역 PASS 뒤 문서 크기 handoff41/todo82 HARD로 FAIL, Compile/테스트 SKIPPED. compact-docs로 handoff는 최신 스냅샷, todo 완료 원문은 History005로 옮겨 수정했다. 보정 후 check-work 구역/문서/Compile/Edit798/798 PASS. 실행 검증 후 QA객체0/Play stopped/씬 clean/GameView1920×1080/timeScale1/runInBackgroundfalse·Console오류0/경고0 확인.
- 구역 검사: PASS (10-21)
- 1 문서/규칙: PASS (10-21)
- 2 컴파일: PASS (10-21)
- 3 테스트: PASS (EditMode, 798/798, 10-21)
