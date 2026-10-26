# C14 설정 창·공용 컨트롤 검증

## 범위
- 2026-10-20 PM 현재 요청. `SettingsUi` 전용 코드/프리팹/독립 씬으로 조립한다. 기존 `Systems/Settings` 게임 규칙 코드 수정 없음.
- private 제공 Runtime 원위치 사용. 복제 버튼만 `CurtainCall.UI.SettingsV1.CommonButtons`, 나머지는 기존 `CurtainCall.UI`이며 전체 키트가 SettingsV1로 격리됐다고 주장하지 않는다.
- 3탭×3행은 검토용 데모. 실제 옵션 목록/기본값/범위/저장·즉시 적용/취소 정책 결정이나 AudioListener/Screen/PlayerPrefs 연결 없음. 삭제·commit/push·PR/병합 없음.
- 현재 생산36PNG(신규20/재사용16)와 누적165고유PNG 이력 수치는 다르다. 전체 키트 baseline258파일/PNG43, 기존 프리팹/폰트/게임 Settings 포함 최초 보호 baseline411파일, 기존 buttons/panels 정본 키트 추가 후626파일을 scratchpad 비공개로 보존한다. 개별 private 경로·SHA 전수 목록은 공개 저장하지 않는다.

## 구현·검증 계획
- 부작용 없는 제공 조립 API·공용 helper, 기존 출력/meta·dirty 씬·Play/import/compile/PrefabStage 거절·additive 격리·부분 실패 보존.
- Canvas 전체 SettingsPanel 및 공용 Tab/Slider/Toggle 각각 프리팹. 독립 확인 씬의 설정 창은 Play 초기 Hidden, 명시적 열기. EventSystem/InputModule은 확인 씬에서 제공한다.
- Native Toggle/ToggleGroup·Slider·Button, 별도 visual/focus. Slider handle32×32/Y delta0, fill0/.5/1, dirty/탭 편집 유지·deep copy/검증·busy/토큰/재진입·disable/unscaled 검사.
- 합성 ExecuteEvents·raycast 검사와 실행 캡처, 물리 Mouse/Keyboard·사람 최종 외형 확인을 구분한다. 실게임 HUD/입력 소유·safe area·플랫폼/공동 검증은 별도다.

## 실행 이력
- 연속 Task 시작 doctor 결과 유지, 중복 실행 안 함. C14 대상 Editor7801/6000.3.25f1 ready 확인.
- 첫 AssetDatabase.Refresh eval은 `Main thread operation timed out after 5000ms`로 실패. 이어 구역/문서는 PASS였으나 컴파일 검사 진입 시 Editor가 컴파일/리로드 중이라 중단(컴파일 PASS 아님). idle 이후 재검사한다.
- 초기 wrapper 검토에서 탭 SemiBold 글리프가 데모 문구만 포함하는 문제를 조립 전에 보정: 임의 한국어 페이지 label을 지원하도록 Medium과 같은 전체 한글·ASCII·기호 Static 계약으로 확장한다. Bold는 고정 제목 ‘설정’의 실제 글리프만 요구하며, 테스트도 각 텍스트/역할별 요구를 구분한다.
- idle 이후 구역/문서/정식 컴파일 PASS. Build run_script 응답은 `Main thread operation timed out after 120000ms`였지만 재실행하지 않고 ready/실제4프리팹·씬·전용 SemiBold 생성 확인. 이후 컴파일 PASS로 첫 PlayMode 실행.
- 첫 PlayMode total23/passed22/failed1/skipped0. `ClosedCallbackCanReopen` 본문은 통과했으나 TearDown Destroy→OnDisable Closed에서 테스트의 영구 재열기 listener가 비활성 view에 Open을 호출해 InvalidOperationException 발생. 제품의 비활성 Open 거절 계약을 바꾸지 않고 테스트가 finally에서 listener를 제거하도록 정리했다. 첫 결과는 로컬 보존한다.
- 원본/보호 중간 검사626파일: 승인 Builder1+신규 importer20 외 변경0, PNG 변경0/누락0.
- 보정 후 구역/문서/정식 Compile PASS, C14 PlayMode23/23·failed/skipped/inconclusive0. 설정 씬 열기 eval도5초 응답 timeout이었으나 재호출 없이 list_open_scenes에서 SettingsUi/clean 확인 후 Play 진입했다. 실행 화면·선택 회귀·최종 check-work는 진행 중이다.
- 실행 QA 첫 시도: 미리보기 합성 클릭17/비활성 콜백 차단1 통과 후 `30_Toggle` raycast 실패로 캡처 전 중단. 추가 진단에서 활성화 직후 Graphic.depth=-1/등록 전 상태를 확인했다. 제품 수정 없이 QA를 비동기화해 페이지 전환 후 렌더 대기220ms를 넣어 재검사한다. Play 전환 직후 run_script 연결 reset1회는 ready/playing 재확인 후 실행했다. console 조회의 잘못된 `--last` 인수1회도 최종 검증 결과와 구분한다.
- 렌더 대기 후 1920×1080/1920×1200 각각 raycast15·합성 click6·native Move6, 미리보기 합성 click17·비활성 콜백 차단1 PASS. 명암×(세 탭/0·50·100%/disabled/focus) 총24캡처 확인. 밝은 배경의 독립 샘플 글자 대비 부족은 확인 씬 전용1888×96/raycast=false 국소 배경으로 보정했다. 실제4프리팹/제공 Runtime/컨트롤 색상 불변, 첫 배경 보정 후 같은 입력·24캡처 재검사는 통과했으나 16:10에서 상단 기준 샘플 영역이 중앙 패널 하단에 겹치는 배치를 발견했다. 확인 씬의 공용3샘플·국소 배경·설명만 하단 anchor로 보정하며, 후속 캡처 QA에 패널/샘플 배경 비중첩 검사도 추가했다. 최종 하단 anchor 보정 후 두 화면비×명암24캡처를 다시 확인했고 비중첩·화면비별 raycast15/click6/Move6·Preview click17/비활성 차단1 PASS. 실제 프리팹은 변경하지 않았다.
- 4프리팹 참조2484/missing0/MissingScript0, 기존 출력 덮어쓰기 거절 PASS. Medium Static8192/11325문자(Spectator), Bold Static2048/267문자(WorldLabels) 읽기 전용 재사용. 전용 SemiBold Static8192/11325문자 생성. 전용 asset140,136,503bytes로100MB 초과이며 현재 filter 미지정이다. 이번 저장 요청은 없으므로 `.gitattributes`의 기존 C11 한 파일 승인 범위를 확장하지 않았다. 추후 저장 시 이 C14 파일만 LFS 전송 대상으로 지정해야 한다. audit에서 SettingsUi 씬 clean/timeScale1/runInBackground=false 확인.
- 국소 배경 보정 후 구역/문서/Compile PASS. console groundTruth 오류0/경고14, 모두 제공 Builder들의 기존 TMP enableWordWrapping 폐기 예정 API 경고이며 컴파일 실패 아님. retained buffer의 과거 오류176건/이전5초 Pipeline timeout과 현재 오류0을 구분하며 콘솔을 임의 초기화하지 않았다.

## 후속 저장 (2026-10-20)
- 사용자 직접 "커밋 푸시해줘" 요청으로 C14 선택 저장 승인. private21파일 e1ddf391/main push·원격 SHA 일치 확인 완료. 무관 results/title_logo 및 results 로컬 보정은 미추적 상태 그대로 보존·저장 제외한다.
- `.gitattributes`에 C14 전용 SemiBold 한 파일만 LFS 지정했다. 원본140,136,503bytes/GUID 불변, 기존 공유 폰트 전송 방식 불변. 공개 C14 산출물·기록/gitlink는 이 커밋으로 feat/ui-resources에 저장·push한다. 아래 commit/push 없음은 제작 완료 당시 이력이다. 실제 공개 push 성공/SHA는 원격 확인 후 최종 보고한다.

## 최종 결과 (2026-10-20)
- 최종 하단 배치 보정 후 정식 Compile PASS, C14 PlayMode23/23·C16 선택 회귀16/16(실패/건너뜀0), 표준 check-work 구역/문서/컴파일·EditMode798/798 PASS. 전체 프로젝트 PlayMode의 기존 Tooltip/Buttons 미결까지 해소한 결과는 아니다.
- 실행24캡처: 1920×1080/1920×1200 각각 명암×6상태. 실제 이미지 확인·비중첩24건(최소8px), 화면비별 합성 raycast15/click6/Move6·Preview click17/비활성 콜백 차단1 PASS. 물리 Mouse/Keyboard/사람 최종 승인 아님.
- 최종 audit: 참조2484/missing0/MissingScript0·기존 출력 거절 PASS, EventSystem/InputModule 각1. SettingsUi 씬 clean/Play stopped/1920×1080/timeScale1/runInBackgroundfalse. 캡처 임시 Assets/.harness 잔류0. 보호626파일 누락0/PNG변경0/승인21파일 외 변경0 재확인.
- 최종 Console groundTruth compilationFailed=false/오류0/경고2. 두 경고는 C16 회귀의 Canvas/alpha 손상 시 안전 열림 방어를 검증하는 LogAssert.Expect와 정확히 일치한다(TransitionCurtainPlayTests.cs:229,240). 버퍼 누적 오류175건과 현재 오류0은 별개다.
- 산출물4개: `Assets/Resources/Prefabs/UIs/SettingsUi/{SettingsPanel,CommonTab,CommonSlider,CommonToggle}.prefab`. 확인은 `Assets/Scenes/Tests/SettingsUi/SettingsUi.unity` Play→데모 열기. 실제 옵션/서비스·물리 입력·사람 외형/손맛·실기기/공동은 별도, commit/push/PR/병합 없음.
