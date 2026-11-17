# Log
- #416 병합 상태 verify-unity: Compile/Common settings PASS(exit0), dirty scenes0. 게임 로직 추가 수정 없이 최신 dev와 충돌 없는 PR로 갱신.
- PR 생성 직후 dev에 #416 탈출로 바닥 정리가 병합되어 gitlink 충돌 발생. 씬/프리팹 YAML 충돌은 없었음. private에 이미 병합된 d31a2fef가 UI162868bd와 바닥94303505를 모두 포함함을 ancestor 검사로 확인해 해당 포인터로 해결. 이후 추가된 대형 외부 팩은 작업 범위에 포함하지 않음. dev6aa745ef 병합 상태 컴파일/설정 확인 후 PR 갱신.
- 2026-11-17 저장·제출 완료: 게임 구현95b7abf2 및 private 에셋162868bd push. PR https://github.com/gaboja-studio/POC_CURTAINCALL/pull/417 (feat/ui/ingame-ui → dev), 지정 팀원 검토/병합 대기. 마지막 check-work PASS, diff 공백 검사 PASS. new-pr DryRun 템플릿 확인 후 무관한 폰트 캐시4개를 로컬에 보존하기 위해 명시 stage/gh --body-file 경로로 제출. 사용자 실전 검증·추격자 그림자 성능 비교는 후속으로 유지.
- 사용자 저장·dev 제출 요청 수신. 재개 fetch 시 d2163566/origin-dev 동일, 프로젝트 Unity 종료 확인. private sprite kit 162868bd를 CURTAINCALL_PaidAssets/main에 push(LFS14개 포함). 최신 private main의 기존 관객 반응 에셋도 보존한다. 자동 TMP font cache4개는 제출 제외·로컬 보존, 관련 경로만 stage하여 저장한다.
- d2163566 병합 후 verify-unity -Compile: Compile/Common settings PASS(exit0), dirty scenes0. 처음 조회는 Editor 초기 로딩 중 Pipeline 미등록으로 ENV_BLOCKED였으며 ready 확인 뒤 정상 통과. 제출 시 HEAD/origin-dev 0/0, 커밋·push 없음.
- 몸 조준 변경 묶음 check-work exit0: 안전성·문서·컴파일·공통 설정 PASS. 자동 테스트는 별도 24+9개 실행 결과 사용. 제출 직전 Unity 정상 종료 후 dev 6커밋(a8d755d5 → d2163566)을 fast-forward, 대기실 수정이며 현재 작업 충돌 없음. 병합 상태 컴파일/공통 설정 재확인.
- 최종 몸 조준 검사: PursuerWorldUiTests 24/24, WorldIndicatorTests 9/9 PASS. Play frame326/time5.51 → frame1092/time18.93 진행 확인. 10-body-target.png에서 몸 영역 브래킷·가려지지 않는 진행 바 확인. Show Grab Path OFF 유지, 테스트 씬 저장 없이 종료. 실제 두 peer 플레이/최종 가독성 검증은 사용자 체크리스트로 남김.
- 2026-11-17 최신 몸 영역 조준 요청: 시작 동기화 b34664aa → a8d755d5, 기존 작업 보존. 준비 표적과 실제 전투 Aim을 GrabAimPoint로 통일(현재 몸 높이68%). targetHeadOffset/previewTargetHead 제거, 정본 에셋은 Editor API로 저장. 테스트 씬/실제 showcase도 같은 몸 조준 기준으로 변경.
- Play 검토에서 몸체가 진행 바 일부를 가리는 현상을 발견하여 렌더 면만 카메라 쪽으로 보정. 화면상 위치·크기는 보존하고 전투 Aim/피격 범위는 유지. 원근/직교 투영 보존 포함 WorldIndicatorTests 9/9 PASS. 첫 복합 필터는 0 tests여서 통과로 계산하지 않고 단일 필터로 실행.
- 설정별 정본 위치·값 및 실제 플레이 17개 검증 항목을 manual-verification.md에 작성. 이전 04 캡처의 머리 위 방식은 최신 동작이 아닌 이력으로 표시.

- 2026-11-17 피드백 반영: 그림자·원/사각 진행 표시 승인, 중앙 위험 아이콘 제거 확정. 그랩 도착 원 설계 철회. Capture.AdvanceArm/SweptHandTouches에서 고정 Origin→Aim의 프레임별 구형 손 sweep과 모든 플레이어 캡슐 접촉 확인. 타겟 외 중간 접촉·공중 높이 판정이 있어 F 공중 통로로 수정한다. 사용자 요청대로 신규 F 시안 피드백 전 제작 단계로 넘어가지 않는다.
- 같은 Task 재개이므로 doctor 재실행 안 함. Unity 프로세스 없음 확인 후 origin/dev fetch 성공·0/0, 기존 원화·문서 변경 보존. 게임 코드·씬·프리팹 변경 없음.
- 내장 imagegen으로 기존 v1을 참조한 world-ui-airborne-v2.png 생성·시각 검토·private Source~에 복사. B/C 아이콘 제거·승인된 진행 표시 보존, F는 공중 캡슐 통로+방향 점선+예고 바 제안. v1 원본 보존. 구현 계획에서 원인 기호 레이어 제거, SweptVolumeIndicatorView/실제 판정 연결·회수 시 숨김을 추가. Unity 실행/새 테스트는 하지 않았음.

- 2026-11-17: doctor 1회 실행. FAIL: private submodule 초기화 판정, 현재 브랜치 Task 없음, 기존 Task ID 중복, Unity 6000.3.25f1 미설치. WARN: 에디터 연결 없음·CLI 버전 차이·gh 인증. 현재 Task를 신규 생성해 브랜치 연결 해소. 다른 기존 문제는 미변경.
- 시작 시 Unity 프로세스 없음·기존 변경 없음 확인. sandbox의 FETCH_HEAD 쓰기 제한으로 기본 fetch 실패 후 승인된 상승 실행으로 fetch 성공. HEAD/origin/dev b933060a 동일, 0/0이므로 병합 변경 없음. 커밋·push 안 함.
- 정적 조사: 원형 마커는 몸체 casting을 끄지 않아 플레이어 그림자가 중복 가능. 대기실 추격자 GroundMaterial은 방향 안내이며 그림자 아님. 좁은 그림자 조사에 보조 AI 1명 사용, 파일 수정 없음.
- UI 목록 5묶음, 추가 대상(투척물·포획 플레이어 투척·보스 돌진·화염 예고), 후속 후보(외줄 소멸·절단 부위·OnGUI HUD)를 inventory.md에 분리 기록.
- 내장 imagegen 원화 2장 제작·시각 검토. `_ThirdParty/curtaincall_world_ui_v1/Source~/Concepts/`에 원본 복사. 01 SHA256 E1EDD4C6347A932457BB52A967C291A75A9B44B32CDE6AD58A383C808B344EB8. 프롬프트는 concept-prompts.md. 신규 런타임 sprite/prefab/code/settings 변경 없음.
- verify-knowledge: OK (22 folders). Unity 플레이/컴파일·성능 비교는 실행하지 않음. 신규 테스트 없음; 실행하지 않은 테스트를 PASS로 기록하지 않음.
- 다음: 사용자 원화 승인/수정 의견 → 승인안 레이어 제작 → 프리팹·컴포넌트·GameSettings 연결 → Unity/사용자 검증. 추격자 그림자 교체는 성능 비교 후 확정.
- 원화 02 SHA256 005BD5B5A5AE10B4E89AF42F7868EB009796E0B0B8DDEA13C4B102119543E935. private 저장소에서 신규 키트 폴더가 untracked임을 확인; 원본/공개 문서 모두 미커밋 상태로 보존.
- 제출 전 origin/dev fetch 재실행 성공, HEAD와 동일 0/0. check-work -SkipUnity 1회 exit 0: 안전성·문서/규칙 PASS, Unity 변경 없음으로 컴파일 생략, 자동 테스트 미선택. 기존 문서 길이 경고 77개(hard 0), private 원화 미커밋 주의. 게임 검증 PASS를 의미하지 않음.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-17 사용자 v2 승인으로 단계 3–7 착수. unity-cli/unity-pipeline 스킬 적용. 재개 fetch에서 dev 9개 커밋을 확인하여 에디터를 닫고 7e599c96으로 fast-forward. 새 Task-148 충돌을 피해 현재 미추적 Task를 151로 이동.
- 생성 이미지 마스크 2회는 가장자리/배경 알파 노이즈로 생산 사용 제외, private Source~/Drafts 보존. 승인 실루엣을 Unity native analytic mask로 독립 RGBA PNG 8종 생성, shared material 4종 및 6-layer prefab 5종 구성. 절차적 경계는 크기 변형 시 선 두께를 유지하며 stroke sprite를 재사용한다.
- WorldUiSettings 정본 에셋과 GameSettings.WorldUi 연결. 공통 IWorldIndicator/WorldIndicatorView, 원·사각/지면통로/공중체적/대포뷰, 공격·착지·장애물·대포·그림자 adapter 구현. 위험 판정/공격 시간 변경 없음. 플레이어 body casting Off, 부상 바디 포함·소지품/분리 팔다리 제외. 추격자 blob opt-in 기본 false.
- Builder 첫 실행: NewScene 전환이 보관 중인 prefab handle을 해제하여 MissingReferenceException. 새 씬에서 AssetDatabase 경로로 다시 로드하도록 수정, 재실행 성공. 프로젝트 기존 씬은 저장/수정하지 않음.
- 실제 Play 테스트: frame 504/time8.43 → frame2253/time37.90 → frame5187/time87.45, 자동 프레임 진행 확인. 초기 캡처 저장. 설치된 capture_game_view는 경로를 Assets 아래로 재작성하므로 증거 파일은 최종 Task/Evidence로 옮긴다.
- 초기 검사: WorldIndicatorTests 6/6, ActorShadowPresenterTests 6/6, WarningBatchTests 2/2 통과. Pursuer 필터339 중 기존/관련324통과, 신규15개는 fixture 초기화 NRE로 실패하여 수정 후 재검사 중. NonParallelizable 속성은 프로젝트 NUnit에서 지원하지 않아 제거.
- 추가 사용자 피드백: 공중 통로를 기본 표기에서 제외. 준비 중 머리 위 표적 추적 → 마지막 위치 고정 → 기존 선딜 → 발사로 변경. 통로는 옵션이며 확정 후만 표시. 탈출로 대상/근접 시 대기실 화면 위험 테두리 재사용, 거리 설정 요청. 기존 원/사각 진행 표시·그림자·대포 작업은 유지.
- 경계/어댑터 추가 후 WorldIndicatorTests 7/7 통과. PursuerWorldUiTests 초기화 수정 후 15/15 통과(추가 피드백 전 기준). 기존 Pursuer 회귀324통과 결과 보존. 이후 새 타겟 단계/카메라 효과를 추가 검사한다.
- 추가 지시 구현: Preparing .35초 → 마지막 실제 Aim 확정 → 기존 Warning → Strike. 범위 및 근접 포획 판정 보존. 타겟 프리팹/레이어 추가(최종9 sprites/6 prefabs), showGrabPath=false 기본. escapeThreatDistance=18m, escapeThreatEnabled=true.
- Pursuer 회귀 최신348/348 PASS(새24 포함). 화면 효과 테스트는 처음15 중9개가 Graphic.OnPopulateMesh 오버로드 reflection 선택 문제로 실패; 테스트 helper를 인자 타입으로 선택하도록 수정하여 재검사.
- EscapeRouteSettings 정본에 grabPrepareSeconds .35를 Editor API로 저장. 네트워크 메시지는 Preparing와 확정 점프 좌표를 포함하는 v10이며 모든 peer 같은 빌드 필요.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출 직전 Unity 정상 종료 후 fetch/fast-forward: 7e599c96 → b34664aa(13커밋), 코드 충돌 없음. 새 trampoline Task가151을 사용하여 현재 미추적 UI Task를153으로 변경. 경로·문서 참조 갱신. 대기실 추격자 연동 영향 확인을 위해 병합 후 컴파일/화면 효과 검사 재실행.
- Editor가 자동 초기화한 기존 TMP 폰트4종의 atlas/glyph cache 차이는 작업 시작에 없던 자동 변경임을 확인, 로컬 .harness/local/world-ui-font-cache-backup에 보관하고 HEAD 내용으로 복원. 사용자 작성 에셋 수치 변경 없음.
- 최신 Play 캡처9개를 Task/Evidence로 수집하고 CLI가 만든 Assets/Tasks 복사본은 Editor API로 제거. 04–06은 화면 가장자리 포함 Game view, 07–09는 Play 중 Review Camera 직접 렌더. 모두 production prefab을 쓰는 scripted review이며 실제 네트워크 실전이라고 주장하지 않는다.
- 최종 개별 검사: Pursuer348/348, WorldIndicator7/7, ActorShadow6/6, WarningBatch2/2, PursuitFeedback15/15 PASS. 폰트 생성 노이즈 외 기능 변경은 유지. 사용자 검토·두 peer 실전·추격자 그림자 비용 비교는 미실시이며 Task Active 유지.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- b34664aa 병합 후 PursuitFeedbackTests 15/15 재통과. 최종 check-work exit0: Safety/Fast/Compile/Common settings PASS. 재개 후 TMP 폰트 자동차이는 재발하지 않았음. Unity를 저장 안 된 씬 없이 정상 종료하고 최종 동기화 확인.
- 캡처 증거 04–09와 설정/키트/검증 사용법을 implementation-report.md에 정리. 커밋·push 미실행. 현재 기본: 경로OFF, 준비.35초, 위험거리18m, 대포8m, 플레이어castingOff, 추격자blobOff.

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-17 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
