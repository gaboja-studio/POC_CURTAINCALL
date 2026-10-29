# C9 자막·화자·방송 검증 (2026-10-20)
## 승인·경계
- 최신 PM 요청 `C9 커밋하고 push해줘.`로 C9 private/public 저장 승인. 아래 추가 저장 제외·미커밋은 구현 당시 이력이다. 삭제·실게임 연결·무관 변경 저장은 여전히 제외한다.
- 저장 사전 검사 첫 실행은 Play 상태로 컴파일 단계 중단, edit 전환 후 구역/문서/컴파일 PASS(up_to_date). 테스트는 저장 검사에서 SKIPPED, 위 실제 실행 검증과 구분한다.
- C9 private116파일 커밋 `bab2b062`, 원격 README만 바뀐 `5f066764`를 일반 병합한 `2b67e36d`로 main push 완료. 기존 라이선스22행 후행 공백·원본 Tools/write_manifest.py 끝 빈 줄은 바이트 보존을 위해 유지(diff --check exit2), 원본 수정·hook 우회 없음. 공개에는 C9 소유 출력/코드/기록과 이 포인터만 선택 저장한다.
- PM 직접 재승인 후 C7 공개92164890a9424a226eaf6f96710afc080de48f8a commit/push 및 원격 SHA 일치 확인 완료. private38001b8e 저장 완료. 앞선 실행 전 차단은 우회하지 않고 이력으로 보존했다.
- 이어 C9 제공 키트 조립·독립 확인 씬·표시 API·검증·하네스 기록 진행. C9 추가 commit/push·삭제·실게임 연결·큐/노출시간/오디오 동기/발화 우선순위 추가 없음.
- 전용 Captions 코드/프리팹/폰트/씬·private 제공 cs3 원위치 보정만. 기존 PNG9 정본/importer 읽기 전용·전체 원본 보존. 무관 private captions 외 feedback/Leartes/unity_only 및 Interaction 1.meta 보존.
## 확인 방법
1. `Assets/Scenes/Tests/Captions/Captions.unity`를 열고 Play. 첫 진입은 숨김 상태다.
2. 대사/화자 없음/방송/기본 화자/긴 이름/두 줄/문자 그대로 버튼으로 표시. 본문 초과·빈 본문·무효 폭은 기존 자막을 유지하며 상태에 거부 사유를 표시한다.
3. 폭 범위·퇴장/즉시 숨김·역전/반복·시간 정지·배경 전환을 확인하고 초기화한다. 시간 정지는 초기화/비활성/예외/Play 종료 때 원래 값으로 복구한다.
## 최초 전수·보존
- [전수 CSV](captions-inventory.csv): 최초115행=실파일49+meta66, PNG13(생산9+미리보기4). scratchpad 최초 baseline은 재작성하지 않는다.
- 보호1534파일(기존 정본 폰트/프리팹/씬·Packages/ProjectSettings·다른 키트 PNG/meta/cs) SHA 기록.
- 생산9/9 시트 조각을 RGBA 각 채널로 비교 일치. 기존 Notifications4/PerformanceHud4/CommonIcons1 정본 PNG SHA 일치, 신규 그림0. Source/Tools/Docs/Previews/TTF/라이선스 보존.
## 공유 폰트 보존 미결
- 화면 검증 중 git status에서 공유 `Assets/Resources/Fonts/CheerGauge/Pretendard-Bold SDF.asset` dirty를 확인했다(HEAD 대비1140추가/12삭제, glyph/character table·atlas). 처음 이를 새 변경으로 해석했으나 최종 최초 baseline 대조로 철회했다.
- 최초 C9 baseline SHA와 현재 SHA는 모두 `f9ddfae99f5ab4f7a6c6da2c19614531d0cf719d899cb03ca8b640bd597aecd0`, HEAD는 `d0232c9895e79c95bcc9d552b772aa0040aad257af58a8f8c9ab3d658037e0c1`. 즉 C9 시작 때 이미 있던 차이이며 C9 추가 변경 증거가 아니다. baseline은 재작성하지 않았다.
- 최종 보호1534 SHA 변경0으로 audit PASS. 기존 공유 Dynamic 미결 자체는 해소되지 않았고 임의 복구/폐기/재작성 없이 보존한다. C9 전용 Static 폰트와 구분한다.
## 구현·실행 검증
- 최초 구역 검사는 신규 코드 meta8개가 아직 임포트되지 않아 FAIL. Unity Refresh 후 재검사 구역 PASS·컴파일 PASS(up_to_date). 출력 최초 조립 성공·기존 PerformanceHud 씬 clean 복원, 기존 정본 보존 확인.
- 구조 검사 참조2954/missing0·Missing Script0·Image9/TMP2·StaticMedium18/24·기존 Sprite9/Sliced8/Simple1·단일 EventSystem/module/raycaster 통과. 검사 코드 첫 실행의 AssetDatabase.GetAtPath 오기는 AssetImporter.GetAtPath로 보정했으며 프로젝트 컴파일 실패와 구분한다.
- 최초 신규 PlayMode16건14 PASS/2 FAIL: EarlyAwakeShowSurvivesBothComponentAwakes 선행 Awake Accepted=false, TypographyAndLongSpeakerAreLiteralAndFixed maxVisibleLines expected1/actual99999. 첫 JSON 보존·원인 조사 및 최소 보정 완료. 최초 실패를 통과로 바꾸지 않는다.
- maxVisibleLines는 TMP 비직렬화 필드라 prefab 로드 시 기본99999가 되는 것을 소스에서 확인, 최초 런타임 초기화에 스타일 재적용(기존 테스트 완화 없음). 선행 Awake 실제진단은 RuntimePlaying=true/RootActive=true지만 ViewActive/MotionActive=false·활성 Play 거부였다. enabled+activeInHierarchy로 선행 Awake를 허용하되 실제 비활성/편집 guard 유지하도록 보정했다.
- 명시 컴파일 PASS 직후 첫 editor_play는 Pipeline HTTP400으로 실패. editor_status stopped/ready 확인 후 별도 호출로 정상 진입, 실패를 기능 PASS로 세지 않는다.
- 실제1920×1080 어두운 배경 방송 PNG에서 하단 띠·화자/확성기·본문 두 줄 및19버튼/상태 문구를 목시 확인했다. 전역 DEV MODE 배지는 이번 소유 UI가 아니므로 수정하지 않는다.
- 보정 후 신규 PlayMode16/16 PASS, failed/skipped/inconclusive0. 기하·입력 비차단·한글/문자/두 줄·초과/빈값/CanFit 메시 보존·폭 원자성·반복/역전·unscaled·비활성/선행 Awake·Preview 복구를 검사했다. 기존 실패 JSON은 삭제하지 않았다.
- 조립 직후에는 보호1534 SHA 변경0/원본115 누락0/제공 cs3 이외 원본 변경0·PNG13 바이트 변경0·시트9/9 일치 확인. 최종 audit도 변경0이며 공유 Bold의 HEAD 차이는 최초 baseline부터 존재했음을 별도 확인했다.
- 1920×1080/1600×1000 × 명암 배경 ×14표시 상태=56캡처 자동 검사 실패0. 밝은1600×1000 화자 없음 화면도 목시 확인했다. 사람 최종 외형 승인을 대신하지 않는다.
- 실제 Mouse/Enter 입력78/78 PASS(19버튼×2입력×2해상도76+시간정지 중2), listener 각각1/호출 증가1 확인.
- timeScale0.37에서 정지 후 Reset/Disable/의도적 예외 각각 원래 값 복구, 정지 중 Play 종료 후1 복구. C7 동시 표시 API/별도 슬롯16UI 간격·비중첩 통과. 첫 동시 캡처의 C7 임의 문구는 Static 폰트 미포함 글자여서 기존 예시 문구 `행동 안내`로 재실행·캡처하여 C7/C9 한글과16UI 간격을 목시 재확인했다.
- 명시 컴파일 PASS. 편집 씬 방송 샘플은 기존 Builder를 재실행하지 않고 소유 씬 인스턴스 API로 부분 적용·저장했다. persistent 프리팹 런타임 Show/CanFit/폭 변경 거부·직렬화 불변, 기존 출력 Builder 재생성 거부·씬 clean/timeScale1 확인.
- 후속 회귀 C9 16/16·C7 16/16 PASS. Tooltip20건 중19 PASS/1 FAIL: RepeatedHideDoesNotRestartRealExitAndEndsExactlyHidden, TooltipPlayTests.cs:323의 정확 Vector2 비교에서 표시값은 모두(0,-4). 첫 결과 JSON 보존, 기존 C7 때도 관측된 항목이며 이번 C9 구역 밖 코드를 수정하거나 전체 회귀 PASS로 보고하지 않는다.
## 최종 결과
- 선택한 UI PlayMode104건=103 PASS/1 FAIL, skipped/inconclusive0. 나머지 회귀 Cheer3/Buttons2/Panels3/Icons3/Performance9/Backstage7/Notifications9/Interaction16 모두 PASS. 이번 Buttons2/2로 과거 원인 미확정 이력을 해결 처리하지 않는다.
- check-work 구역/문서/컴파일 PASS·EditMode798/798 PASS. 지식 검사 PASS, 문서 크기 기존 무관 WARN7/hard0. 공개 diff --check PASS(줄바꿈 안내만).
- 최종 구조 참조2954/missing0·Missing Script0, 단일 EventSystem/module/raycaster 유지. 최종 audit 원본115 누락0·승인 cs3만 변경·PNG13 변경0·시트9/9 RGBA 일치·보호1534 SHA 변경0 PASS.
- 최종 동시 캡처 재진입 첫 eval은 Pipeline HTTP400으로 실행 실패, stopped 복구 후 Play 진입/준비 확인을 분리하여 재실행 성공. 프로젝트 컴파일 실패와 구분한다.
- 종료 Captions 편집 씬/clean·Play stopped·timeScale1·runInBackground 원래false·1920×1080. 현재 Console 오류0/경고3. C9 추가 commit/push·삭제·실게임/오디오 연결 없음. 사람 최종 외형·공동/네트워크는 미검증.
