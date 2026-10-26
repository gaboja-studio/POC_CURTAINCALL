# C6 검증 기록
## 범위·보존
- 2026-10-20 PM 승인 C6 계획. InteractionPrompt.prefab 1종·독립 Interaction.unity·Preview·안전 Builder·표시/모션 테스트. 실게임 연결·삭제·커밋·push 없음.
- 키트49실파일/meta64/PNG15, 생산8=신규 링2+PerformanceHud 재사용6. 전체 원본·미래 제작 후보 보존. 전수 CSV의 최초 SHA는 변경 전 원본 기준, 제공 코드3개 namespace/안전 API 보정은 허용 변경이다.
- 원본49개 최종 대조에서 누락0, 변경은 승인된 제공 코드3개뿐이다. PNG15·Source/Docs/Tools/Previews/폰트/라이선스는 최초 SHA와 같다.
- 생산8 시트 crop RGBA 픽셀 일치. 링128×128/alpha0..255/bbox(3,3,125,125), 나머지 판256×112/키캡80×80. 재사용6 PNG 바이트 SHA 일치, 보호baseline397 확보.
- doctor FAIL: 시작부터 private 무관 tracked 변경과 미추적 unity_only/C6 키트 있음. 보고 후 보존, 복구·폐기·반복 doctor 없음.
## 구현 중 실패·수정
1. 첫 eval 읽기 조회가 Main thread operation timed out after 5000ms. editor_status/list_open_scenes 및 최소 eval 성공 후 계속 진행. 이번 작업에서 지속적인 권한 서비스 장애는 확인되지 않았다.
2. 신규 Preview/테스트의 U+2028/U+2029 리터럴 때문에 컴파일 오류18개. 문자 코드 조합으로 치환 후 errors0. 이전 오류 로그를 통과로 해석하지 않는다.
3. 첫 조립은 새 Static 폰트의 fallbackFontAssetTable null에서 NullReferenceException. 폰트·링 importer 결과 보존, prefab/scene 미생성 확인. null을 빈 목록으로 처리하고 안전한 기존 전용 Static을 읽기 전용 재사용해 다음 조립 성공. 실패 시 임시 씬도 보존하도록 Builder 보강.
4. 신규 PlayMode 최초15/16: 선행 Awake Show 거부. 테스트 호출 위치를 같은 C6 오브젝트로 조정한 재실행14/16(선행 Awake 실패·RectTransform 종료 위치의 미세 float 차이). 실제 원인은 자기 Awake 전 isActiveAndEnabled 검사이며 View/Motion의 enabled+activeInHierarchy 검사로 보정; 좌표 테스트 허용 오차0.001 적용. 이후16/16 PASS(2.76s).
5. C1 프리팹 자체에 Canvas가 없어 최초 문맥 인스턴스가 Canvas 밖이었음. 새 C6 씬의 C1 인스턴스만 PerformanceHud Canvas 자식으로 증분 이동, Builder 동기화. 기존 정본 재생성/Apply 없음.
6. 최초 입력90건은 Mouse45건 통과/Enter45건 호출0. Submit 액션 활성·실제 Enter binding·focus 정상 확인. CLI simulate_key는 즉시 InputSystem.Update를 호출한다. 정상 프레임에 KeyboardState를 QueueStateEvent한 단독 Enter는 호출332→333 확인; bitfield에 QueueDeltaStateEvent는 거부되어 사용하지 않는다. 최종 전체 재검사 결과는 아래에 별도 기록한다.
7. 첫 전체 화면은 C1 이외 문맥 HUD가 초기 숨김. Preview Start/Reset/전환에 기존 공개 API로 합성값 주입, Controls를 양옆으로 옮겨 중앙 C6/P1/W6·좌하단 C5를 피하고 문맥 Static 원본 폰트17개를 새 씬에서만 복원했다. 기존 정본/폰트 자산 재작성 없음.
## 자동·실행 검증
- 신규16개: 레이어·참조·초기 숨김·네 조합/색/링·기본/명시 보조·finite Clamp·무효 보존/검사순서·미설정 거부·SetProgress 비캐시·대상 변경·100% 유지·제어문자/literal/긴문구·Show/Hide 비재시작·중간 전환·timeScale0 실제Update·disable/re-enable·즉시숨김/Reset·실제 선행Awake.
- 테스트 시간 검사는 프레임 오차 허용. 기본120/100ms 설정·실제 Update 끝점 확인과 중간 전환(관찰용2초 설정)을 구분한다.
- 개별 PlayMode 회귀: Cheer3/3·Buttons2/2·Panels3/3·Icons3/3·Performance9/9·Backstage7/7·Notifications9/9·Interaction16/16 PASS(총52/52). 비동기 시작 응답0/0이 아니라 test_status 완료 결과로 판정.
- 첫 화면 수집은 프리셋 전환 직후 실제 Screen281×881이 남아 중단. 다음 프레임 실제 크기를 확인하도록 수집기를 보정, 실제1920×1080/1600×1000·밝고 어두운 배경·14상태씩 총56개 캡처의 글리프/빈mesh/표시alpha 실패0. 두 비교판과 전체 화면 목시 확인.
- 전체 화면에서 HUD 문맥이 초기 숨김 상태인 점을 발견했다. 합성 데이터/Controls 배치 보완 후 최종 재검증을 진행했다. 최초56개 결과만으로 HUD 동시 배치 통과를 주장하지 않는다.
- HUD 합성값 보완 후56상태 재수집: 활성 C6/HUD/Controls 전체 TMP의 glyph/빈mesh·C6 alpha 실패0. C6 비교판2개 재확인.
- Controls 고정 X가16:10 우측에서 잘려 우측 블록을 화면 오른쪽 anchor로 보정했다. 이어 실제 P1 전체 rect와 Controls 첫 행16UI 중첩을 검출해 첫 행Y256/StatusY544로 조정했다. 이미지 외곽과 RectTransform 전체 영역을 구분해 재검사한다.
- 보호 자산397개 SHA256 변경0 재확인. 최종 입력·배치/구조는 아래 완료 결과 참조. 최종 보정 후 개별 PlayMode 회귀도 동일8종52/52(실패0·생략0) 통과. 표준 check-work 구역/문서/컴파일·EditMode798/798 PASS, handoff Verification에 기록. 검사 후에도 stopped/Interaction 씬clean/1920×1080/timeScale1/background=false 재확인.
- 기본120/100ms 설정으로 Update 논리 곡선 점검: 정규화50% 등장alpha.875/퇴장.125, 등장 끝1/퇴장 끝숨김. elapsed를 강제 주입한 로직 검사이며 실제 프레임 시간 측정과 구분한다. 실제 Update/일시정지/중간 전환은 PlayMode 테스트로 별도 확인.
- Preview가 기억한 원래 timeScale.37에 대해 Reset/OnDisable/예외 복구 모두.37 확인. 독립 Play 재진입 초기숨김 및 timeScale0 종료 후1 복구 확인. Play 전환 직후 연결 재설정으로 수집기2회 중단; 부분 JSON 보존 후 editor_status에서 playing/reload완료를 확인해 남은 단계만 수동 재개했다. 지속 권한 서비스 장애는 아님.
- 종료 상태: stopped/Interaction 씬clean/1920×1080/timeScale1/runInBackground=false. 편집 Show false·내용/활성 보존, 기존 출력 Builder 재호출은 쓰기 전 충돌 거부. 최종 Play 구간 Console 신규 오류0(이전 실패 버퍼는 보존).
## 최종 배치·입력·참조
- 최종8화면(두 실제 해상도×두 배경×공연/백스테이지): Controls22개 화면 밖0·C6/P1/W6/C5 전체 rect 겹침0. 대표 전체 화면 목시 확인. DEV MODE 배지의 기존 C1/W1 겹침은 범위 밖으로 보존.
- 실제 Input System 경로90건 통과: 22버튼×Mouse/Enter×2해상도88건+timeScale0 Mouse/Enter2건. 호출 증가 각1, persistent listener 각1. Enter는 KeyboardState 큐→정상 프레임→UI 모듈 경로이며 직접 OnSubmit/onClick 호출이 아니다.
- 직렬화 참조1869개 검사/missing0, EventSystem1·InputSystemUIInputModule1·활성 raycaster Controls1, sorting HUD0/C6 3/Controls10, Dynamic TMP 참조0.
- 384×80/(0,-144)·11레이어·키캡/링 중심 차이0·Canvas PPU100/1920×1080/Match0.5·Radial360/Top(2)/Clockwise·Body.92/Shadow.32 확인. Medium24/보조18·NoWrap/Ellipsis·AutoSize/richText/parseCtrlCharacters Off.
- Sprite8 모두 PPU200/FullRect/RGBA32, 플랫폼56조합 override0. 판256×112/border24·키캡80×80/border16·링128×128/border0. 아래 GUID는 키트 복사본이 아니라 실제 프리팹 Sprite 정본 참조다.

| Sprite 축약명 | 실제 정본 GUID |
|---|---|
| smallplate_shadow | 81ea2dbe1538f5d4e807fe56853b501c |
| smallplate_body | 45c41aceabb933645acc526ea158c6a2 |
| smallplate_frame | 71d8650d90093ce4e905240cd374386b |
| smallplate_corners | 7afd517212230c44892fa9e2a00a8427 |
| keycap_basic_body | 504104e3202fbe1448c164fd1481f719 |
| keycap_basic_frame | 0f359e28d04a23143b25238d711202dc |
| prompt_hold_track | 0042e13da7ec89a43ae9431d4c4ab7b1 |
| prompt_hold_fill | eba7e92f71fd6354d9627a445bcd947e |
## 미검증·기존 미결
- 사람 최종 외형 승인·공동/네트워크·실게임 연결은 미실행. 실제 F 키의 게임 상호작용 검증이 아니다.
- 공유 Dynamic TMP2개 과거 dirty/baseline SHA 차이·Buttons 과거 글리프 실패 원인 미확정, 임의 복구 금지. 이번 회귀 결과로 원인을 닫지 않는다.
- reload 설정 변경 없음, Domain/Scene Reload 둘 다 OFF 지원 주장 없음.
- 시작 변경 목록에 있던 `Assets/Scripts/Systems/Interaction 1.meta`는 폴더용 meta다. 삭제 금지에 따라 그대로 보존하며 정리 완료로 주장하지 않는다.
