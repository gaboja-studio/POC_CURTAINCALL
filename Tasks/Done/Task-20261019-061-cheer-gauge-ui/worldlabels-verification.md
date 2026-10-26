# 월드 라벨 v2 검증 (2026-10-20)

## 범위·현재 상태
- PM 최초 요청: 제작 worktree `D:/github/POC_CURTAINCALL-cheer-gauge-ui`에서 `curtaincall_worldlabels_v2` 조립·독립 검증. 후속 `저장하고, 이번에는 C11 관전·사망 UI 하자` 요청으로 월드 라벨 private/public commit·push 승인. 실제 게임 씬/로직 연결·삭제 없음.
- Task061 working. 기존 공유 Bold·Interaction 1.meta·무관 private·하네스 정책 변경 보존. 연결 worktree는 조작하지 않았다.
- Wood/Cloth/Paper 소품 표면3종, Item/Shelf 근접 정보2종. 크기/scale/거리와 소재 외형은 제공 검토 제안이며 최종 게임/아트 승인 아님.

## 최초 보존·조립
- 최초 원본161파일/PNG29·기존 출력/폰트/시스템 코드/테스트 씬838파일 SHA를 scratch에 보존. 전수 목록 `worldlabels-inventory.csv`.
- 신규 소재 PNG12만 importer 보정, smallplate4+icons3은 기존 정본을 읽기 전용 참조. 복제본/정본 SHA7/7 일치.
- 제공 Runtime3/Builder를 private 원위치 `CurtainCall.UI.WorldLabelsV2`로 보정. 공개에는 어댑터·확인 도구·테스트만 추가했다.
- 안전 Builder는 Play/compile/import/dirty씬/PrefabStage/기존 출력 폴더를 사전 거부한다. additive 소유 씬만 저장하고 원래 활성 씬 복원, 실패 시 부분 출력 보존·자동 삭제 없음.
- 5프리팹·Static Medium/Bold·`Assets/Scenes/Tests/WorldLabels/WorldLabels.unity` 조립 성공. 각각 루트+직접7, Image5/TMP2; 정보UI 관찰자 미지정 숨김.

## 최초 실패·복구
- doctor FAIL1: 기존 private dirty, WARN3: 원격 없는 연결branch·CLI 문서 버전·Skill 링크. 자동 복구/Skill setup 반복 안 함.
- 첫 `unity status`에 command 전용 caller/skill 플래그를 전달해 unknown option 거부. status는 지원 인자만으로 재조회해 정확한 프로젝트 ready 확인.
- 최초 읽기 eval·Refresh eval 각각 HTTP400/main-thread5000ms timeout. status stopped/ready 진단 후 읽기/구역 검사로 import 완료 확인, 중복 조립 없음. 중간 명령 discovery exit6 출력 없음도 보존.
- 최초 scope FAIL: 작성 중 Preview.cs.meta 누락. Unity import 후 scope338파일 PASS.
- 최초 컴파일 FAIL: Builder의 실제 Unicode U+2028/U+2029 문자 리터럴로 CS1010/CS1011/CS1012. 숫자 char 비교로 수정 후 Compile PASS(completed). 테스트 assertion 완화 없음.

- 최초 PlayMode18건은16 PASS/2 FAIL. Shelf 최소높이56에서 Bold32 실측38.18UI > 원본제안 텍스트32UI라 수용 실패. 외곽96×56/실제Bold32를 유지하고 최소높이에서만 수직여백12→8(텍스트40UI)로 보정했다. 기본96×64의 여백12는 그대로다. 테스트에 최소 텍스트높이40/위치-8 검증을 추가했다.
- 나머지 실패는 Observer Destroy 직후 코루틴이 다음 LateUpdate보다 먼저 검사한 순서였다. 실제 소멸(null)을 확인한 뒤 한 번의 LateUpdate를 기다리도록 수정, 숨김 assertion은 유지했다. 첫 결과 JSON은 scratch에 보존했다.
- 수정 후 Compile PASS, 직후 run_tests가 HTTP400으로 시작 실패했다. editor_status stopped/ready·리로드 아님 확인 후 테스트를 별도 호출했다. 시작 실패를 테스트 통과로 계산하지 않는다.

## 검증 진행
- 수정 후 신규 PlayMode18/18 PASS, failed/skipped/inconclusive0. 근접 경계·관찰자 교체/비활성·빌보드 복원·표면만 숨김·overflow 보존·Static 글리프·시간정지·최소크기를 검사했다.
- 최초 Mouse22건은 콜백 통과했지만 CLI `simulate_key`의 즉시 InputSystem.Update 경로에서 Enter 첫 콜백이 누락됐다. 포커스/Submit 바인딩 정상 확인 후 KeyboardState를 정상 다음 프레임에 주입하니 콜백 발생. bitfield에 QueueDeltaStateEvent를 시도한 진단 오류도 보존, 제품 입력 코드 변경 없음.
- 정상 프레임 경로로 1920×1080·1600×1000 각각 Mouse22+Enter12=34/34 콜백 통과. 히스테리시스 false→true→true→false→false→true·표면 off/on·무효크기 보존도 상태로 대조했다. 이는 Input System 합성 입력이며 사람 승인이 아니다.
- 첫16:10 캡처에서 확인용 마지막 열 버튼 테두리 잘림을 발견했다. 정본5종은 그대로 두고 Preview 버튼 컨테이너/열 폭을 stretch로 보정, 상태문구는 전역 DEV 배지를 피하도록 내렸다. Builder 소스와 소유 씬의 기존 인스턴스에 Unity API로 반영·저장(프리팹 재생성/Apply 없음). 이후 Play 재검증은 아래 컴파일 차단 때문에 미실행이다.
- 보존 재검사: 보호838 SHA 변경0·원본161 누락0·PNG29 변경0·재사용7/7 일치. 변경은 승인 private cs4·신규 소재 importer meta12뿐이다.
- 별도 읽기전용 correctness 재검토에서 확인된 중대 문제 없음(실행 검증과 구분).

## 후속 승인·차단 해소 진행
- PM `저장하고, 이번에는 C11 관전·사망 UI 하자` 요청으로 월드 라벨 저장 승인. 최초 save-work DryRun은 settings 중복 타입으로 실패해 commit/push하지 않았다.
- PM 선택 `최소 수정 포함` 승인 후 settings 동봉 버튼 namespace만 `CurtainCall.UI.SettingsV1.CommonButtons`로 격리하고 View/Builder 참조 alias2곳을 수정했다. 설정 UI 제작/자료 삭제 없음. 이 미추적 키트의 최소 로컬 수정은 월드 라벨 저장에 섞지 않고 보존한다.
- 직후 첫 컴파일 검사는 오류목록 없이 실패했으나 진단 recompile_status는 completed/failed=false/compilationFailed=false였다. 표준 검사 재실행으로 판정한다. 아래 차단 기록은 당시 이력이며 최종 결과는 후속 절을 따른다.

## 최종 차단·종료(해소 전 이력)
- 최종 재컴파일 FAIL: `Assets/_ThirdParty/curtaincall_settings_v1/Assets/CurtainCallUI/CommonButtons/Runtime/CurtainCallButton.cs:14`가 기존 `CurtainCall.UI.CurtainCallButton`과 충돌(CS0101/CS0579/CS0111). 월드 라벨 밖의 키트는 읽기만 하고 변경/삭제/비활성화하지 않았다. 해당 키트 수정은 PM 요청 필요.
- check-work: 구역 PASS·문서 PASS, 컴파일 FAIL, EditMode SKIPPED(컴파일 단계 실패). 선택 UI 회귀도 미실행이며 과거798/798/103/104를 이번 결과로 재사용하지 않는다. 위18/18·입력68/68은 Preview 배치 마지막 보정 전 결과다.
- 편집 검사: 정본5종 각8노드/Image5/TMP2, 참조882/missing0/Missing Script0, persistent API 거부·직렬화/dirty 불변. 현재 로드된 마지막 성공 어셈블리 기반 검사이며 최종 소스 컴파일 통과 근거는 아니다.
- Builder 호출은 컴파일 실패 가드에서 쓰기 전 거부. 첫 검사 도구가 기존 출력 가드까지 도달한다고 예상해 실패했으나 실제 거부 사유를 분리해 재검사했다. 기존 출력 전용 가드·PrefabStage 실행 검증은 미완료, 코드 검토와 구분한다.
- 종료 WorldLabels 편집/clean·Play stopped·timeScale1·1920×1080·runInBackground 원래false. EventSystem/module/raycaster 각1·Preview 버튼12. 컴파일 오류는 남아 있다. 추가 Play/재캡처는 수행하지 않았다.
- 다음: settings 중복 타입이 승인 범위에서 해소된 뒤 컴파일→보정 후 두 화면비/입력→선택 UI 회귀→check-work를 재개한다. 월드 라벨 미커밋, 정식 인계/PR 없음.

## 승인 후 최종 재검증·저장 진행
- settings 최소 namespace/alias 보정 후 check-work: 구역 PASS·문서 PASS·컴파일 PASS·EditMode798/798 PASS. 위 FAIL/SKIPPED는 해소 전 이력이다.
- 정상 컴파일 상태에서 편집 보호 재검사: 참조882/missing0/Missing Script0, 정본5종 각8노드/Image5/TMP2, persistent API 거부·직렬화/dirty 불변, 기존 출력 전용 재생성 가드 거부 확인. PrefabStage 실행 검증은 미실시다.
- 최종 보존 재검사: 보호838 변경0·원본161 누락0·PNG29 변경0·재사용7/7 일치. private 변경은 승인 cs4/importer meta12뿐이다.
- private 원본162파일 선택 저장 `e26bd4b1`, 원격 README 변경을 일반 병합한 `6ff6e18e2d41cbfec476b681293abc6f522ceff2`를 main에 push하고 원격 SHA 일치 확인. 무관 키트/settings/Leartes 저장 제외.
- 제공 LICENSE.txt:22 trailing whitespace·Tools/write_manifest.py:433 EOF 빈줄은 원본 보존. diff --check의 기존2건을 기록하며 hook/signing/검사 설정 우회 없음.
- 보정 후 첫 입력 재검사는 1920×1080 34/34 통과 후 1600×1000 Mouse Sample_4 콜백 누락. 후속 진단은 매 입력 현재 좌표/실제 Raycast의 부모 Button을 대조한다. 에디터 비포커스에서 frame1 정지도 관측해 검사 중에만 runInBackground를 켜고 종료 시 원래false로 복구한다. 제품 입력 코드는 변경하지 않았다. 최종 보정 후 1920×1080·1600×1000 각각 Mouse22+Enter12=34/34, 총68/68 콜백 통과. 각 화면비의 버튼12개 모든 모서리가 화면 안이며 TMP overflow0, 매 클릭 실제 Raycast의 부모 Button을 대조했다. 16:10 명암 최종 캡처에서 마지막 열 테두리 잘림 해소를 확인했다. 이는 합성 입력/자동 캡처이며 사람 외형 승인이 아니다.

- 최종 PlayMode를 개별 substring filter로 순차 실행: WorldLabel18/18·InteractionPrompt16/16·Guide16/16·Tooltip20/20·Caption16/16, 합계86/86, failed/skipped/inconclusive0. 과거 기록의 회귀104건을 이번 실행 수로 재사용하지 않았다. Tooltip 이번20/20 통과는 과거 정밀 좌표 간헐 실패의 원인 규명을 뜻하지 않는다.
- 종료 실제 확인: WorldLabels 편집/clean·Play stopped·timeScale1·1920×1080·runInBackground 원래false. 신규 UI 확인용 입력/레이아웃 보정 외 실게임 변경 없음.

## 공개 저장 실행 전 권한 차단
- save-work DryRun 최종 PASS: 구역358파일·문서/규칙·컴파일up_to_date, clean씬1. 기존 문서 WARN8/hard0·무관 private dirty230건은 보존했다.
- 승인 월드 라벨 출력/코드/기록/gitlink42파일만 선택 stage, 공개 diff --cached --check PASS. 공유 Bold·Interaction 1.meta·기존 직접 pull 정책3파일·무관 private 키트 제외.
- 공개 commit/push 호출이 실행 전 권한 검사에서 거부됐다. 사유는 요약에 기록된 승인만으로 명시적 저장 경계를 해제할 수 없으며 사용자 직접 확인이 필요하다는 것이다. 공개 commit/push 미실행, HEAD9ede8a3·파일/인덱스 보존·재시도/다른 도구 우회 없음. private main6ff6e18e 저장은 이미 성공했으며 별개다.
- 저장 후 C11 제작이라는 요청 순서 때문에 C11 구현은 시작하지 않았다. 원본 읽기 전용 확인만: 새 비네트2장 RGBA3840×2160, 중앙60%/반대쪽alpha0, 재사용8 정본/명세SHA 일치. C11 출력·코드·구역 추가 없음.

## 2026-10-20 컴파일55개 수정 후 저장 시도
- 현재 요청은 컴파일55개 수정 후 저장. shop9파일 namespace만 격리해 오류55→0, check-work/EditMode798/798·WorldLabel PlayMode18/18 재실행 PASS.
- private 상점 원본277파일9661aa85·README 일반 병합6b4e2041 main push 완료. 공개 commit/push는 실행 전 권한 차단됐다. 월드 라벨 포함 명시 재승인이라는 AI 해석은 정정, 사용자 확인 대기·HEAD9ede8a3·파일/선택 인덱스 보존·우회 없음. 상세 `compile-errors-verification.md`.
- WorldLabels clean·Play stopped·Console오류0/경고0 확인. 기존 외형/Tooltip/공유 폰트 미결은 유지한다.

## 사람 확인·미포함
- 최종 외형/손맛·공동 테스트·실게임/네트워크·카메라/Observer 로컬 소유권 배정은 미검증.
- 벽 가림/양면/조명/PBR mesh·대상 선택/LOS·게임 거리/상호작용 계산은 제공/이번 범위에 없다.
- 기존 Tooltip 정밀좌표 회귀 실패·공유 Dynamic 폰트/Buttons 미결은 이 작업 통과로 닫지 않는다.
