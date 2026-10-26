# C11 관전·사망 UI 검증

## 요청·범위 (2026-10-20)
- 후속 사용자 직접 "저장해줘" 요청으로 C11 결과/기록·Schedule 충돌 수정의 private/public commit·push를 진행한다. 아래 추가 저장 제외/없음은 제작·검증 종료 시점의 이력이며 이 후속 요청으로 대체된다. 무관 변경/삭제/실게임/PR·공개 통합 병합 제외는 유지한다.
- 사용자 현재 요청: 선행 저장 후 `_ThirdParty/curtaincall_spectator_v1` C11 진행.
- 선행 저장: 공개 `2fe68a273ff540c7d85db792d999c2140febedb6` 커밋·push/원격 SHA 일치 완료. 이전 공개 권한 차단은 실제 완료 전 이력으로 보존한다.
- C11 독립 표시 API·안전 조립기·Static Medium·Canvas 전체 프리팹·확인 씬·샘플/테스트만. 실게임 연결·사망/대상/카메라/입력/네트워크·자동 슬롯 중재·삭제·C11 추가 commit/push 없음.
- 전용 worktree/Unity: `D:/github/POC_CURTAINCALL-cheer-gauge-ui`, port7801, 6000.3.25f1. 같은 작업이므로 doctor 중복 실행 안 함. 시작 doctor HeadMismatch FAIL/CLI·skill-links WARN은 선행 수정 검증 기록에 보존.

## 원본·정본·보호 baseline
- 전체129파일/PNG16개 누락 없이 목록·SHA256 기록: [spectator-inventory.csv](spectator-inventory.csv). Source/Tools/Docs/Previews/폰트/라이선스 보존.
- 생산 PNG10개=신규2+재사용8. 재사용 smallplate4/키캡2는 PerformanceHud, 눈/해골2는 CommonIcons 정본 SHA·동봉 명세 SHA 8/8 일치, 정본 읽기 전용 참조.
- 신규 비네트2: RGBA3840×2160, 중앙 x768..3071 전체 alpha0·반대쪽 alpha0. 실측 alpha bbox Left(0,0,646,2160)/Right(3206,0,3840,2160).
- 생산 시트3888×4528: SHA 일치·10/10조각 RGBA 픽셀 정확 일치.
- 기존 공개 Fonts/UIs/테스트 씬/Systems 및 다른 private UI 키트2737파일 SHA baseline 보존. 종료 시 비교한다.

## 유지할 표시 계약
- Image11/TMP4. 좌우 비네트는 `00_EdgeEffects`, 상태판은 `20_ContextHUD`. 두 가지와 cross reference를 함께 보관하는 Canvas 전체 프리팹.
- 상태판360×64·하단 중앙(0,160), 상태18/설명24/키24·키40/글자32, Pretendard Medium Static. 한 줄 고정 크기·Ellipsis/원문 보존, Rich Text/제어문자 파싱 끔.
- Hidden/Dead/Spectating/NoTarget, 비네트강도0/.70/.40/.40, alpha-only unscaled120/100ms. 긴 키는 양쪽 모두 억제, 안내는 실제 입력 바인딩이 아닌 데모.
- `OccupiesBottomSlot`은 퇴장 중에도 true, 우선순위 C11>P1>W6>C7는 외부 호출자 계약만. C9 별도 슬롯, 기존 HUD 자동 탐색/변경 없음.

## 후속 수정 요청
- 저장 범위: private 미추적 C11 129파일·Schedule203파일 및 각 상위 .meta=334파일을 원본/라이선스/제작 자료 보존해 저장한다. Schedule 기능 조립/게임 연결은 여전히 제외. private diff --check의 동봉 OFL 공백2건/제작 도구 EOF1건은 원본 그대로 보존했고 새 코드 공백 오류로 간주하지 않는다.
- 사용자 직접 요청 "버그를 먼저 수정하고 계속 진행하자."에 따라 setup에 Schedule 최소 namespace 격리 구역을 추가했다. private Schedule203파일 baseline 보존 후 Runtime6/Editor1의 namespace 선언만 ScheduleV1/.Editor로 변경, 그 외 파일 변경/추가/삭제0. 재컴파일 후 C11 복구·검증을 이어간다. commit/push는 요청되지 않았다.

## 저장 결과 (후속 요청)
- 사용자 직접 "저장해줘" 요청 적용. private334파일 커밋 `1d3bee7d`, 원격 README 변경만 일반 병합한 `11781eb298886486c24e9e8691a9bd47db2f2e9d`를 main에 push·원격 SHA 일치 확인했다. LFS22개 전송 완료. 무관 private 변경은 제외/보존했다.
- 공개 저장 대상은 C11 코드/폰트/프리팹/확인 씬·테스트·사실/Task/Integration 기록 및 위 gitlink다. 현재 기록을 담은 feat/ui-resources 커밋/원격 이력으로 확인한다. 공유 Bold/Interaction 1.meta/정책·브랜치 전략 변경 및 무관 키트/Leartes는 제외한다. PR/공개 통합 병합 없음.

## 재개 결과 (현재)
- Schedule C#7 namespace 최소 격리 후 정식 재컴파일 PASS. C11 대기는 delayCall에서 update의 idle 틱으로 보정하고, 일회 구독 해제·전체 preflight 유지. 기존 폰트 재사용 `ResumeAfterFont()` 성공, 프리팹/독립 씬 실제 생성 및 큐 종료 확인.
- 최초 C11 PlayMode25/25 PASS, failed/skipped0. 후속 정적 점검에서 비활성 Preview 콜백이 시간 정지를 다시 걸 수 있음을 실제 재현(paused0/disable복구.37/비활성콜백0)했다. Run의 isActiveAndEnabled 가드 및 비활성 호출/재활성/Reset 이전 배율 복구 회귀1건을 추가, 정식 재컴파일 PASS. 최종 신규26/26·C7 Guide16/16·C9 Caption16/16·WorldLabel18/18=선택 PlayMode76/76 PASS, failed/skipped0. 전체 프로젝트 PlayMode 통과 주장은 아니며 기존 Tooltip/Buttons 미결과 구분한다. 최종 check-work 구역/문서/컴파일 PASS·EditMode798/798 PASS. 최초 timeout/외부 충돌 실패 이력은 아래와 구분해 보존한다.
- 구조 감사: 씬+정본 SerializedObject 참조3319/missing0/Missing Script0, 정본 Image11/TMP4, 씬 IsConfigured=true. 편집 프리팹 asset의 IsConfigured=false는 activeInHierarchy=false/기본 GetComponentInParent Canvas=null 때문이며 includeInactive=true에서는 Spectator Canvas를 확인했다. 실인스턴스26테스트/씬 구성 통과와 구분한다. Build/ResumeAfterFont 기존 출력 보호 예외 실제 확인, 출력 덮어쓰기 없음. 종료 Spectator 씬 clean/Play stopped·timeScale1·1920×1080·runInBackgroundfalse, 에디터 ready/compilingfalse/compilationFailedfalse·Console errors0/warnings0.
- 실행 UI 합성 입력72/72 PASS(1920×1080·1600×1000 각각18버튼×Mouse/Enter). 정상 입력 프레임·별도 QA 가상 Mouse/Keyboard로 검증했고 UnityEvent 직접 Invoke/ExecuteEvents 우회 없음. 물리 장치·사람 조작 검증은 아니다.
- 명암2×6상태×2화면비=24PNG 캡처, 두 contact sheet와 원본 긴이름 화면을 직접 확인했다. 중앙 투명/상태판·아이콘/키 억제/긴이름 말줄임·원문 보존·버튼 화면 경계 통과. 16:10 상단 전역 DEV MODE 배지와 설명 영역 인접은 기존 별도 미결이며 C11 정본 변경 범위 밖이다.
- C7 데모: 단독 표시→C11 점유 시 억제→퇴장 중 슬롯 유지/가이드 숨김→퇴장 완료 슬롯 반환/가이드 복원 실제 확인. 실제 C9는 연결하지 않은 독립 슬롯 위치 설명만 표시한다.
- 첫 입력 검사 Mouse18건 통과 후 `simulate_key` Enter 제출 실패. 해당 도구는 즉시 `InputSystem.Update()`를 호출한다. down/up 분리도 제출되지 않았고, 정상 프레임에 `QueueStateEvent(KeyboardState Enter/해제)`를 주입하자 동일 버튼 Dead 실행 확인. C11/UI 코드는 바꾸지 않고 검사 도구만 정상 입력 프레임으로 보정, 첫 결과 보존 후 전수 재실행한다. bitfield 키에 QueueDeltaStateEvent 사용은 API 거부되어 사용하지 않는다.

- 입력 검사 중 추가 도구/환경 제약도 분리 기록: capture_game_view save_path는 프로젝트 밖 scratch 경로를 거부하므로 inline PNG를 받아 scratch에 저장한다. 후속 Mouse 검사도 포커스 상실 시 미실행되어, 원본 InputSettings를 보존한 DontSave 복제본에 QA 동안만 IgnoreFocus/AllDeviceInputAlwaysGoesToGameView를 적용하고 정상 프레임의 MouseState/KeyboardState로 전수 재검사한다. 제품 입력 정책·ProjectSettings 파일은 저장/수정하지 않는다. 복제본만으로도 물리 장치 주입은 실패했으나 별도 QA 가상 장치로 전수 통과했다.
- 종료 복구 중 원본 InstanceID 조회 실패: InputManager.settings setter는 기존 HideAndDontSave 임시 설정을 즉시 파괴하는 엔진 구현임을 소스로 확인했다. 원본 InputSettings asset은 없음(FindAssets0). 복제본의 보존 값으로 새 HideAndDontSave 임시 설정을 구성하고 변경한 두 값을 사전 값 ResetAndDisableNonBackgroundDevices/PointersAndKeyboardsRespectGameViewFocus로 복구, QA 복제본/가상 장치2개 정리·잔여0 확인. Play stopped/timeScale1/runInBackgroundfalse 확인. 동일 원본 객체 참조 복구로 주장하지 않는다.

- 최종 SHA: C11 원본129 누락0/추가0·승인 cs2/신규 importer meta2만 변경, PNG/문서/제작 자료 변경0. 기존 보호2737 누락0/변경0. Schedule203 누락0/추가0·C#7 namespace 외 변경0. 무관 기존 dirty 보존, C11/Schedule 추가 commit/push 없음.
- 최종 문서/지식 검사 PASS, git diff --check 공백 오류0. 기존 크기 WARN9/hard0와 미저장 private 변경 안내는 보존하며 Unity 통과 근거로 대신 쓰지 않는다.

## 최초 실패·차단 이력 (아래는 재개 전 상태)
- 최초 refresh 뒤 compilationFailed=false/Console errors0 확인. 제공 namespace 격리·신규 코드/PlayMode25건 작성 후 엔진 검증 중이다.
- 최초 Build 응답은 Pipeline main-thread 5000ms timeout. 중복 생성하지 않고 실제 로그/산출물 확인: 전용 Static8192 폰트 저장까지 완료했으나 이후 BuildProject의 idle preflight가 거부되어 프리팹/씬 생성 실패. 빈 clean additive씬/부분 출력 보존, 삭제·강제복구 없음. 명시 복구 진입점 보정 후 재검증한다.
- 이때 실행한 check-work는 구역/문서 PASS, 컴파일 단계 editor_status 30000ms timeout FAIL, EditMode SKIPPED. C# 오류로 단정하지 않으며 이후 진단에서 compiling/updating/compilationFailed/play 모두 false 확인.
- 중간 SHA 비교: 원본129 누락0·승인 cs2/신규 importer meta2만 변경, PNG/문서/제작 자료 보존. 기존 보호2737 누락0/변경0. 종료 시 재비교한다.
- 후속 `verify-unity.ps1 -ProjectPath . -Compile` 결과 FAIL. 새 `curtaincall_schedule_v1/Assets/CurtainCallUI/CommonButtons/Runtime/CurtainCallButton.cs`의 `CurtainCall.UI.CurtainCallButton` 중복 CS0101/CS0579/CS0111 총21개. C11 baseline 보호목록에는 해당 키트 파일0개, 현재 폴더 존재 확인. 이 외부 키트는 C11 수정 허용 구역 밖이므로 변경하지 않았다.
- 복구 코드 `ResumeAfterFont()`/delayCall·전용 Static provenance GUID 검증 보정까지 작성했으나 새 코드 로드를 컴파일 실패가 차단한다. refresh 직후 호출은 오래된 로드 assembly의 API 부재 오류였으며, 이후 정식 재컴파일에서 위 외부 타입 충돌을 확인했다. 복구 API 실행 성공/프리팹·씬 생성 완료로 간주하지 않는다.
- 현재 산출물: 전용 Static Medium8192 폰트·제공 Runtime/Builder 격리/안전 보정·공개 ProjectBuilder/18버튼 Preview·PlayMode25개 코드. 프리팹/확인 씬은 미생성, PlayMode/회귀/화면/실제 입력은 컴파일 차단으로 SKIPPED(0건 실행), 준비 스크립트만 구문 PASS.
- 종료: 첫 실패 때 만든 root0/clean/path없음 임시 additive씬만 닫음. 기존 WorldLabels 씬1개 clean·Play stopped·timeScale1·runInBackgroundfalse 보존. 파일/부분 출력 삭제·C11 추가 commit/push 없음. PM의 외부 키트 최소 namespace 수정 권한/작업 구역 배정 후 컴파일→ResumeAfterFont→신규/회귀·화면/입력 검증 필요.
- 최종 Console groundTruth: compilationFailed=true/compiling=false/errors21/warnings5. `verify-fast.ps1` PASS(기존 WARN9/hard0), `verify-knowledge.ps1` PASS. 최종 SHA 재비교도 원본129 누락0·승인cs2/meta2만 변경/보호2737 누락0·변경0. 공개HEAD/원격 feat/ui-resources 모두2fe68a273ff540c7d85db792d999c2140febedb6 확인.
- 사람 최종 외형/손맛·공동/실기기·배포 플랫폼 메모리·실게임/네트워크는 별도 미검증.
