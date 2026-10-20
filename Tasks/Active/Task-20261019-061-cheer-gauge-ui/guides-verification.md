# C7 조작 가이드·키캡·마우스 검증 (2026-10-20)
## 승인·경계
- PM 직접 요청으로 C6·C15 공개 `b16c3282338aa2cbb83eb1f7f000ac7fb2f61add` 커밋·feat/ui-resources push 및 원격 SHA 일치 확인. private73fd48aa는 origin/main 조상 확인. 앞선 두 저장 차단은 과거 이력으로 보존하며 우회하지 않았다.
- 선행 저장 후 C7 제작 시작. `Guides/Guides.prefab`, 독립 `Guides/Guides.unity`, Preview17버튼·공개 안전 Builder·전용 Static Medium·PlayMode16건을 추가했다. 제작 시 C7은 미커밋이었다. 이후 PM의 C7 커밋 요청으로 이번 저장을 승인받았으며 C9는 분리한다. 삭제·실게임/네트워크 연결·입력 감지·키 바인딩 결정·자동 노출/우선순위 계산 없음.
- private 제공 Runtime/Builder 원위치 재사용·GuidesV1 namespace 격리. 전역 EditorWindow/Inspector/PlayReset/저장·삭제 제거, 공개 어댑터만 신규 importer/소유 출력 저장. 실패 시 부분 결과 보존·기존 출력 충돌/dirty 씬 거부.
## C7 저장 (2026-10-20 PM 요청)
- private C7만124파일 커밋8b49cc29. 첫 push는 원격 README 자동 갱신8b3a6fa4보다 뒤여서 non-fast-forward 거부됐다. README만 변경된 것을 확인하고 일반 병합38001b8e 후 main push 성공(LFS13/17MB). 강제 push·reset·hook 우회 없음.
- 제공 CONTRACT_REVIEW.md의 Markdown 줄바꿈 공백과 LICENSE.txt의 기존 trailing whitespace로 private diff --check는2를 반환했다. 원본/라이선스 바이트 보존을 우선해 그대로 저장하고 신규 C#의 공백 오류와 구분했다. 공개 검사 결과는 별도다.
- C9 captions·feedback·unity_only·무관 Leartes 변경 및 Interaction 1.meta는 저장 제외/보존. 공개 결과·Task/Integration·보존된 문서 History·gitlink는 이번 C7 저장에 포함한다.
- 공개 저장 직전 DryRun은 구역/문서/컴파일 PASS(씬 clean, up_to_date), 테스트는 해당 저장 검사에서 SKIPPED였다. 문서 압축 후 기존 무관 WARN7/hard0만 남았다.
- 공개 선택 add/commit/push 호출은 실행 전 권한 검사에서 거부됐다: C7 직접 승인이 현재 문맥에는 요약으로만 있어 재확인이 필요하다는 사유다. 다른 도구/명령 분할로 우회하지 않았다. 공개 저장 완료를 보고하지 않으며 파일을 보존한다. C9는 폴더·README 읽기 전용 확인만 했고, C7 저장 선행 조건이 미충족이므로 구현은 시작하지 않았다.
## 전수·보존
- [전수 CSV](guides-inventory.csv)에 최초54실파일+meta69=123행 경로/크기/SHA 기록. PNG19·Source/Tools/Docs/Previews/TTF/라이선스 전체 유지·누락0.
- 생산14=신규8(넓은/아주 넓은 키캡4·마우스4)+기존 PerformanceHud 정본6(판4·일반 키캡2). 재사용6은 명세 SHA/규격 읽기 전용 검증·기존 importer 변경 없음. 신규8 meta만 보정.
- 생산 시트14/14 RGBA 픽셀 일치. 보호baseline1208 SHA 변경0. 승인 제공 코드3·신규8 importer meta 이외 키트 원본 변경0, PNG19 바이트 변경0. 무관 private 변경·unity_only·Interaction 1.meta 및 공유 Dynamic TMP2개 보존.
## 표시 계약
- 544×80/하단중앙(0,176), Image8/TMP2. 키40/96/144×40, 마우스32×32·이동 화살표 간격8. Pretendard Medium24/NoWrap/Ellipsis/자동 축소·richText·parseCtrlCharacters 없음.
- 실제 TMP 필요 높이28.63UI 때문에 원본 키 글자 영역28UI에서 F mesh가 비었다. 키캡40/글자24/영역 위치는 유지하고 글자 영역 높이32UI로 보정, 정본은 재생성하지 않고 Unity API로 해당 RectTransform만 수정했다.
- ShowKey/ShowMouse는 편집·비활성·미설정·무효enum을 빈 문구보다 먼저 거부한다. 유효 빈 문구는 즉시 숨김. 제어문자/줄 구분자 공백 처리, 태그·리터럴 역슬래시 문자는 그대로 표시.
- SetContextBlocked(true)는 즉시 숨김/내용 삭제·Show 거부. false는 자동 복원 없음. HideImmediate는 차단 유지·ResetForRuntime은 해제. 일반 Hide는 퇴장 후 비활성 자식에 문구가 남을 수 있으며 즉시 폐기는 HideImmediate를 사용한다.
- unscaled120/100ms/EaseOutCubic/아래4UI, 반복 Show/Hide 비재시작·반전 현재값 보존. stable root는 유지하고 Motion 자식만 숨김. HasRuntimePresentation은 첫 alpha0 포함·Hide 후 false, IsVisible은 자체 alpha/활성 상태 기준이다.
- Overlay1920×1080/Match.5/PPU100/Sprite200·sorting1/Controls10. Mask 밖·모든 가이드 Graphic/CanvasGroup 입력 비차단, Controls에만 raycaster/EventSystem/module 각1을 둔다. 상위 C11/P1/W6는 호출자가 bool로 합산·주입하며 실게임 연결하지 않았다.
## 검증 진행
- 보정 후 신규 GuidePlayTests16/16 PASS, failed/skipped/inconclusive0. 실제 Resources 프리팹의6모드 기하·Static TMP/글리프·긴문구/리터럴·무효enum 보존·유효 빈값 숨김·차단/해제·Hide/Reset·실제 Update/반전·timeScale0·비활성/선행 Awake를 검사했다.
- 실제 씬+정본 참조2684/missing0·Missing Script0·Image8/TMP2·Static Medium24/NoWrap·Sprite14/플랫폼98 override0·EventSystem/module 각1·Controls raycaster1·sorting1/10 통과.
- 재측정 batch는48캡처의 glyph/mesh/범위·alpha 검사 및 실제70입력 모두 통과. 최초 대비 보정 반영 전 캡처도 삭제하지 않았다. 명시적 컴파일·Play 재진입 후1920×1080/1600×1000·밝고 어두운 배경의 추가8화면에서 실제 TMP색·Screen크기·상태/17버튼 겹침0을 검사하고 밝은16:10 화면의 어두운 상태 문구를 목시 확인했다.
- 원래timeScale .37에 대한Reset/Disable/의도적 예외 복구5항목 및 시간정지0에서Play종료 후1 복구 통과. 편집Show거부/직렬화 보존/Builder기존출력 충돌거부도 통과했다. 독립 재진입·종료 후 씬clean 확인.
- 최종 PlayMode:Guide16 + Tooltip20/Cheer3/Buttons2/Panels3/Icons3/Performance9/Backstage7/Notifications9/Interaction16 =88/88 PASS, failed/skipped/inconclusive0. 최초 Tooltip19/20 실패는 아래 이력을 유지하며 1920 프리셋 고정 재실행20/20 PASS만으로 간헐 실패 원인을 확정하지 않는다.
- 표준 check-work:구역/문서/컴파일 PASS·EditMode798/798 PASS. 문서 경고12/hard0 및 private 미저장 변경 경고는 보존했다. 최종 구조 참조2684/missing0·보호1208 변경0·시트14/14 RGBA 일치 재확인.
- 종료 상태:Guides 씬 편집 모드/clean·timeScale1·GameView1920×1080(프리셋3)·runInBackground 원래false 복구. 현재 Console 오류0/경고5·compilationFailed=false. 캡처 버퍼의 과거 오류81과 현재 Console0은 구분하며 로그를 수동 삭제하지 않았다. 사람 외형 승인·공동 플레이·실게임 연결은 별개다.
## 실패·환경 이력
- 첫 컴파일은 공개 Builder의 실제 U+2028/U+2029 char 리터럴에서 CS1010 등으로 실패. `(char)0x2028/0x2029`로 보정 후 컴파일 PASS. 테스트나 파일 보존 이력을 삭제하지 않았다.
- 첫 PlayMode16건은15 PASS/1 FAIL: Typography의 F characterCount0. 실제 씬에서 key rect24×28/preferred13.41×28.63/overflow=true·action은 정상인 것을 확인하고 위32UI 보정을 적용했다. 첫 실패 결과는 scratchpad에 별도 보존했다.
- 첫 실제 화면 목시에서16:10의 Preview 상태 문구가 버튼과 겹치고 밝은 배경에서 대비가 낮았다. 확인용 상태 문구만 top-left(32,-24)/1856×32 및 배경별 색으로 보정했다. 본체 디자인·기존 출력 재생성 없음.
- 첫 화면/입력 batch:48캡처 중1 bounds 실패,70입력 중36 실패(1600×1000의34입력+시간정지2입력 모두 delta0). 첫 결과·이미지는 보존했다. 독립 재진입 후1600×1000에서 Enter와 포인터 각1회가 실제 호출 성공했다. 캡처 후와 각 입력 전 GameView 포커스·Canvas 갱신을 명시한 재측정에서48/70 모두 통과했다. 첫 실패 당시 포커스/Screen 상태를 수집하지 않아 최초 원인은 미확정이며 가이드 기능 결함으로 단정하거나 실패 이력을 삭제하지 않았다.
- C7 이후 첫 기존 UI 회귀는 Guide16/16 PASS 후 Tooltip19/20에서 중단됐다. RepeatedHideDoesNotRestartRealExitAndEndsExactlyHidden의 Vector2 정확 비교가 Expected/Actual 모두 출력상(0,-4)인데 불일치했다. C15 소스·테스트/자산을 임의 수정하지 않고 첫 JSON을 보존하며 나머지 회귀와 안정된 화면 크기에서 재현 검사를 분리한다.
- 첫 verify-fast는 신규 facts frontmatter `status` 누락으로 FAIL. active를 추가했으며 최종 재검사 결과는 아래 기록한다.
- 대비 재검증 첫 Play 전환 직후 eval은 Pipeline HTTP400으로 중단됐다. finally 복구 후 Editor stopped 확인·진입 명령을 분리해 재개했으며 이 실패를 화면 PASS로 세지 않았다.
- doctor FAIL은 시작부터 보존할 private 변경이며 보고했다. CLI 문서 버전 차이·skill-links WARN은 자동 수정/컴파일 근거로 사용하지 않았다. 대상 Editor는 정확한 worktree project-path로 지정했다.
- 보조 AI는 조사→구현→최종 읽기 전용 안전 점검 각1명 순차 사용(동시 최대1명). 안전 점검에서 확정 결함은 없었으나 실행 PASS 근거로 세지 않았다. Unity/Git·테스트는 메인만 실행. reload 설정 변경·both OFF 지원·전체 프로젝트 PlayMode/실게임 검증은 주장하지 않는다.
