# Log

이전 기록은 [History/030](History/030-before-all-packages-log.md)에 보존했다.

- 최종 check-work 안전성/문서·규칙/Unity 컴파일 PASS(up_to_date), 별도24필터281/281 PASS. 중복 테스트는 SKIPPED. context hard0/WARN42, 세 씬 누락 스크립트0·현재 열린 씬1/dirty0·Play정지·맵/프리팹/Resources/ThirdParty diff0. 커밋/푸시 없이 작업 브랜치에 보존했다.

## 2026-11-02 공통에 없는 전용 절단 표시 제거
- 사용자가 원본 공통에 없는 UI임을 정정했다. 전용 표시를 남겨 둔 부분을 제거했다. origin/dev/작업 브랜치 fetch·dev 차이0/작업 브랜치 유지, 기존 변경 보존. 동일 Task doctor 결과 재사용.
- Unity API로 A의 EscapeBodyStatusHud 인스턴스1개를 제거하고 전용 프리팹·표시2스크립트·생성기·해당 UI 회귀와 각 meta를 DeleteAsset으로 삭제했다. 다른 제품 코드/씬의 타입·GUID 참조가 없는 것을 확인했다. 절단 상태/판정·캐릭터 모델 코드는 수정하지 않았다.
- 제거 중 자식으로 묶여 있던 타이머 참조도 끊겼다. 공용 W1 타이머를 독립 EscapeTimerCanvas(기존 정렬35/1920×1080/매칭0.5)에 분리하고 기존37개 프리팹 표시 오버라이드를 Unity SerializedObject로 복구했다. EscapeRouteTimer.display 재연결/480초/좌상단(48,-64) 유지. 원본 공용 타이머 프리팹은 수정하지 않았다.
- 전용 HUD 외6408개 Transform의 이름/위치/회전/크기/활성 불변 확인. 재저장 프리뷰: 전용 표시0·타이머 연결 정상(공용 어댑터1/패키지뷰1)·누락 스크립트0. EscapeRouteTimerTests12/12 PASS·최종 컴파일 오류0, Play 미실행. 증거 .harness/local/remove-body-hud/. 통합 검사는 이어 기록한다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 안전성/문서·규칙/Unity 컴파일 PASS, 별도 타이머12/12 PASS. 전용 HUD0·공용 타이머 참조 유효/480초·누락 스크립트0·열린 씬1/dirty0. Play·commit/push 미실행.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-02 공장 에셋 적용·제출
- 후보 문서를 Task에 보존했다. 두 패키지는 기존 private 모델/텍스처를 재사용해 전체 패키지 재임포트0. 콘크리트/마모 철/탁한 황동으로 재질을 통일하고 벽 거푸집 이음·체결 흔적/바닥 마모를 월드 좌표로 표현한다. URP17 cluster 조명·SRP Batcher(code0)와 편집 렌더를 확인했다.
- 정적 외형913→98묶음, 미사용/LODGroup 없는 비활성 LOD931 제거. 판재8곳은 형태 보존, 실질 기계·배관·컨베이어36곳 교체. 충돌1638/기믹·시스템498 설정 불변. 벽/바닥은 독립 편집 가능하다. 파생 메시290/경량 텍스처27은 private Task073에 저장했다.
- 씬 의존 파일 용량 추정717.5→532.8MiB, 텍스처155→87. 실제 FPS/로딩 시간은 미측정. Play/MPPM/입력 주입 없음. 편집 첫 캡처는 준비 프레임을 추가해 벽/바닥 표시를 재확인했다.
- private resource/exit-route-factory-art 26f81164 push 완료. Unity가 자동 갱신한 AllIn1SpriteShader2개 파일은 이번 저장에서 제외하고 로컬에 보존한다. 현재 제출에는 앞선 공통 묘기 동기화·한글/그네·전용 절단 HUD 제거도 포함한다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출 최신화: dev c4b51736 통합(자동 병합 충돌0), 관련 EditMode37/37. dev에서 넘어온 Task081 길이 초과 로그는 원문 History001 보존 후 현재 기록을 유지해 압축했다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-02 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/268 (feat/exit-route/design-map → dev)
- 제출 완료: 게임 PR #268 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/268 (dev 검토 대기, 병합하지 않음). private 에셋 PR #17 및 main d5de413f 반영 후 포인터 검증/게임 브랜치 push 완료. 최종 안전성·문서·컴파일 PASS, EditMode37/37. 로컬의 무관한 자동 셰이더 변경2개는 보존/제외했다.

## 2026-11-02 단색 산업용 그네·줄
- dev와 작업 브랜치861fb672 차이0, A 백업 후 봉15개/줄86개 외형 보완. 자체 메시5개/재질2개, 원본 벤더 변경0. 기존 단색/초록색 피드백·사라짐 참조 유지.
- Transform/Collider/MonoBehaviour6,809개 동일·Renderer3,052개 유지·새 정점 기존 경계 내·편집3시점 확인. 대상 삼각형14,400→48,624, 실제 성능/Play 미측정. 저장 완료, commit/push 없음. 상세 industrial-rigging.md.
- 최초 검사는 log 말미 빈 줄로 diff 검사 실패, 컴파일 생략. 해당 문서 공백을 정리해 다시 검사한다.
- 최종 check-work: 안전성/문서·규칙/컴파일 PASS, 자동 테스트 SKIPPED. 열린 A dirty0·Play stopped. 기존 문서 길이 경고와 앞선 private 미저장22건은 보존했다.

## 2026-11-02 봉 검은 띠·녹 표면
- A 저장/Unity 종료 후 fetch, dev861fb672와 작업 브랜치 차이0·재개. 같은 Task doctor 결과 재사용.
- 봉15개 공유 재질에 검은 띠4개/끝단 녹·마모 적용. 자체256x1024 텍스처1개·추가 메시/Renderer0. 노란색/초록색 편집 렌더 확인, MPB 원복·저장 씬 바이트 동일·Play 미실행. industrial-rigging.md 참조.
- 최종 check-work 안전성/문서·규칙/컴파일 PASS·테스트 SKIPPED. A dirty0 저장, Play stopped. commit/push 없음.

## 2026-11-02 전력선·공장 심부 분위기
- origin/dev와 HEAD 차이0. A 외줄 외형56·그네 지지줄30 재질 분리. 주광/안개 조정 및 공장 콘크리트 높이별 암부 적용. 편집 전후8구도·기믹/충돌 직렬화2,132개 동일. A 저장, 실제 Play 미실행.
- 첫 검사에서 컴파일 통과 후 작업 문서 소제목 정리가 파일 지문을 바꿔 검사 중단. 저장된 파일 기준으로 재검사한다.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS. 열린 A dirty0. 자동 테스트 SKIPPED.

## 2026-11-02 콘크리트 습기·오염
- dev와 HEAD 차이0. Clock Factory 누수 마스크1장을 기존 private 경량화 흐름으로 파생해 콘크리트·바닥·음영 재질에 연결. 벽 누수는 강도와 높이 분산, 바닥은 줄눈/불규칙 습윤 반사 적용. 추가 패키지 불필요.
- A 편집8구도 전후 렌더, 입구·대포 뒤벽 반복도 보정, 씬 바이트 동일·저장 dirty0. 실제 Play 미실행. 증거 .harness/local/damp-concrete/.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0, 자동 테스트 SKIPPED.

## 2026-11-02 승강기·발전기 최소 임포트
- 학원 원본 2개 수신. private에 화물칸·발전기 프리팹과 의존 파일만 저장. 20개/9.83MiB, 미사용0, A 의존0. URP 재질·LOD·편집 미리보기 확인. asset-candidates.md 참조.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0, 자동 테스트 SKIPPED.

## 2026-11-02 추천8개 패키지 A 적용
- 사용자 추가 지시로8개 원본 모두 다운로드. 기존 두 공장 팩에 승강기·발전기·스위치·작업대/공구·운반 카트·펜스를 더했다. 기존 Transform/충돌/Mono6,809개 설정 동일, 신규 충돌10개·Renderer16개. 상세 all-package-placement.md.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0, 자동 테스트 SKIPPED. 원본8개 6,759,768,963바이트 수신 완료.

## 2026-11-02 벽 녹물 자연화
- dev 차이0. 반복 사각형 누수 셀을 연속 투영으로 대체, 넓은 중성 습기 얼룩에 옅은 국소 녹물 추가. 벽 광택 .49→.28. 입구/대포 뒤벽 포함 편집8구도 비교, 저장 씬 SHA256 전후 동일. 증거 .harness/local/natural-wall/.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0, 자동 테스트 SKIPPED. 실제 플레이 미실행.

## 2026-11-02 컨베이어 그네 반동·프레임 저하 조사
- 매 프레임 CC 토글/IgnoreCollision 재등록·NetworkTransform.Teleport 호출을 연속 Transform 추종으로 변경. 잡는 순간의 순간이동은 유지. 반대 펌핑 입력의 비행 즉시180도 전환과 중복 보정을 수정.
- A 컨베이어6개 가속225/최대각속도150/수평상한8.5/상승상한+.25. 전체레일24위상·각도10도간격·4자리 캡슐표본에서 충돌쌍0. 실제 Play 프레임 측정 미실시. 상세 swing-performance.md.
- 최초 테스트는 컴파일 리로드 중 시작해 진행 상태가 남아 취소. 최신 컴파일 대기 후 재실행한 CannonSeatTests 38/38 PASS. 신규 반대 입력 회귀 포함.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, A 저장 dirty0. 별도 EditMode38/38 PASS. 실제 FPS·4인 연속 비행 체감 미검증을 유지한다.

## 2026-11-02 첫 대포 이후 하강 그네 난도 조사
- A04A_01_CavernExit는 수평상한8m/s·줄길이4.2m. 앞선 컨베이어6개 보완 대상이 아니었다. 무입력 정적 탄도에서 각속도140일 때10도 이탈은 다음 외줄과 .27m,20도는1.33m 횡방향 오차. 대각선 발사와 직선 수신줄의 교차가 좁은 성공 시점을 만든다. 실제 Play 미실행·씬/코드 수정 없음, .harness/local/first-swing/audit.json 참조.

## 2026-11-02 첫 그네→다음 외줄 접합 보완
- A04A_01 방향(6,0,-10)→(5,0,-13), UpperWire 시작(67,-10,65)→(65.2,-10,68), 끝점(67,-10,53) 유지. 동일 Renderer/Collider 참조와 케이블 재질을 사용하고 메시를 새 경로에 맞췄다. 작성 도구에도 좌표 반영.
- 실제 Step 반동8초의 전진 표본496개에 대해 무입력 탄도와 수신 폭/몸 반지름을 비교. 지지 범위 표본24→148, 실플레이 성공률 아님. 그네11각도/4자리와 변경 외줄의 비트리거 충돌체 겹침0. 편집2구도·씬 저장, 증거 .harness/local/first-swing/.
- 네 탑승 자리별 탄도도 추가 확인. 각496개 전진 표본 중 지지범위67/92/136/48개. 최초 최종 검사는 앞선 지식 문서 status 누락으로 실패해 보완 후 재검사한다.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0. 자동 테스트 SKIPPED·실제 Play 미실행.

## 2026-11-02 맵 렌더 비용 1차 최적화
- 활성 Renderer2,989/그림자 조명111 확인. A Spot 그림자110→11, 주광 포함12로 축소. Collider/Mono2,142개 설정 동일·씬 dirty0 저장.
- 편집1280x720 교차 반복 렌더 평균 입구5.98→3.98ms, 컨베이어2.57→1.64ms, 마지막구역2.39→1.95ms(변동 큼). 실제 플레이 FPS 미측정. 상세 map-performance.md.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, A 저장 dirty0. 실제 게임 FPS/최저 프레임은 미측정이며 자동 테스트 SKIPPED.

## 맵 최적화 후속
- 기계11곳 원거리 삼각형486,750→243,366, 정적617 Renderer→78묶음(16m), 활성 메시2700→2161. 복원→재생성 결과 동일, 원본/묶음 삼각형76,177개 동일, 충돌1648개 불변.
- ParticleSystem266개 AlwaysSimulate→Automatic, 추격불 지속 .45→.35. 단일구간14효과의 같은 시드/3초 표본140→112입자. 분출 배율은 기존값. 편집8구도·ABBA렌더 비교, 실제 게임 FPS 미측정.
- Rebuild 검증은 CLI30초 응답 제한을 넘겼지만 네이티브 작업이 완료돼 verified.json 결과와 저장 상태를 재확인했다. 맵 직접 편집 전 Optimization/Restore editable geometry 메뉴 사용. 자세한 절차 map-performance.md.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 열린 A dirty0. 자동 테스트 SKIPPED. 네이티브 복원/재생성 및 기하/충돌 검증 결과는 verified.json에 보관했다.

## 2026-11-03 구조 이음새·면 겹침 폴리싱
- 최적화 묶음 복원 후 원본803개를 검사. 겹친 면 후보113→0, 메시57개에3~15mm 깊이 보정. 닫힌 면 유지. 보수판4개/코어12개 접합 정렬, 작성 도구 반영.
- Collider1648개 필드/Mono 설정 동일, 코어 높이는19.92→20으로 월드 범위4cm씩 보정. 편집9구도 확인, 원본617→78묶음 재생성·저장. 실플레이 이동 중 확인 미실시. surface-polish-followup.md 참조.
- 최종 check-work 성공 증거의 현재 작업 지문 일치 확인: 안전성·문서 규칙·컴파일 PASS, 자동 테스트 SKIPPED. 열린 A isDirty=false 저장 확인. 실제 Play 미실행.

## 2026-11-03 그네 이탈 뒤 공중 조작
- dev/작업 브랜치 fetch, HEAD와 origin/dev 차이0/0. 현재 Unity 편집 상태로 실제 통합 불필요. doctor는 기존 private 미커밋 에셋/중복 Task ID로 FAIL, CLI 버전/skill 링크 경고. 기존 변경을 보존하며 이번 코드 수정에는 직접 영향 없음.
- 원인: 그네 이탈 뒤 A/D가 수평 관성 전체를180도/초로 회전시켜 짧은 입력도 착지 방향을 계속 바꾸었다. 실제 Play는 사용자 범위대로 실행하지 않았으며 코드 경로를 확인했다.
- 공통 HumanCannonPilot/CannonAirControl 수정: 관성은 보존하고 카메라 기준 수평 보정만 추가(0.1초 지수 응답/키 해제 시 감쇠). 기존 수평 상한/상승 탄도/부상 배율 유지. 반동 W/S의 같은 입력만 최대0.15초 유예, A/D·새 방향은 즉시 적용. 일반 대포/트램폴린 경로 유지. A 배치/씬/대기실 미변경.
- EditMode CannonSeatTests41/41 PASS: 횡보정/반대입력 제동·상한/높이·키 해제/잠금/재도약 초기화·30/60/144fps 응답·카메라 방향·부상 보정값 확인. .harness/local/pole-air-control-tests.json. A dirty=false 저장 확인, 실제 조작 체감/멀티플레이 미검증.
- 최초 최종 컴파일은 비행 상태 필드명 변경의 기존 장비 코드 참조 누락(CS0103)으로 FAIL. 기존 필드명을 유지해 장비/PlayMode 회귀의 참조까지 보존하고 다시 검사한다.
- 보완 후 최종 check-work 안전성·문서 규칙·컴파일 PASS, 최신 어셈블리로 EditMode41/41 재확인 PASS. A dirty0. 실제 Play/MPPM은 실행하지 않았다.

## 2026-11-03 원본 외줄 UI 대조
- dev/작업 브랜치 fetch, origin/dev 차이0/0. 같은 Task doctor 결과 재사용. 사용자에게 다른 UI 종류를 비동기로 확인하면서 균형 UI를 먼저 대조했다.
- 원본 Tightrope 씬을 읽기 전용 추가 로드/해제해 실제 그래픽/레이아웃 비교. 같은 PerformanceHud 프리팹이지만 A 위험 트랙12/채움4px 대 원본23/15px, 스케일 MatchWidthOrHeight 대 Expand 차이 발견. 균형 값·위험 비율·입력 표시·배치/색 연결은 같은 경로다.
- A의 해당 크기/스케일·편집 샘플 플래그를 원본에 맞추고 저장. 실제 씬 diff는 기존 UI 프리팹 인스턴스1개 오버라이드만, 추가/삭제 문서0. 원본 씬/프리팹·대기실 미변경. 작성 도구에도 동일 치수 반영. 실제 Play 미실행, 세부 증거 .harness/local/rope-ui/.
- 재비교에서 동적 TMP SubMesh를 제외한 균형 그래픽/배치32개 동일, 두 CanvasScaler Expand 확인. 최종 check-work 안전성·문서 규칙·컴파일 PASS, 자동 테스트 SKIPPED, 열린 A dirty0.

## 2026-11-03 원본 재사용·UI 전반 조사
- dev/작업 브랜치 fetch, dev 차이0/0. 같은 Task doctor 결과 재사용. 게임 코드/씬/설정은 이번 조사에서 변경하지 않았다.
- 실제8개 씬·프로젝트 Mono845개·UI Rect1451개 읽기. 누락 스크립트0/감지된 끊긴 참조0. A 타이머/장치 알림 CanvasScaler 차이2곳, 공통 균형 키 A/D 하드코딩, 관전C11/외줄 안내/C7·C15 연결 차이 확인. ui-reuse-audit.md에 확정 차이/설계 차이/의도된 설정 구분.
- 같은 네 가지 타이머 스냅샷을 편집 복제본에 전달해 숫자·크기 상태 비교. 실제 Canvas 런타임 글자 크기는 미확정. HeldPropsView 부재는 Humanoid 도구 표시가 있어 결함에서 제외. 공용 메뉴/장비/음성/C6 및 맵별 반동/480초 유지 확인.
- 증거 .harness/local/reuse-audit/; 원본은 저장 없이 닫았으며 A만 열림 dirty0. 실제 Play/MPPM/자동 테스트 미실행.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS, 자동 테스트 SKIPPED. 조사 문서 저장 완료.

## 2026-11-03 재사용 UI 보수·추가 조작 조사
- dev/작업 브랜치 fetch, dev 차이0/0. doctor 같은 Task 결과 재사용. U01~U08 수정, 표시 전용 공용 관전/안내 연결·원본/A 균형/관전·보조 툴팁 키 재지정·탈출 외줄 C8 설명 추가. ui-reuse-fixes.md 참조.
- 추가 결함2곳: 자유비행 상태 방어의 Retired 누락, 메뉴 입력 차단 중 관전 대상 변경. 공통/원본 이동/부상/장비 및 인원 조건도 대조, 맵별 튜닝 유지. 원본의 비착지 장식 투영 규칙·운반자 PlayerMover 경로는 결함으로 오판하지 않음.
- A native 연결/개발 환경8항목 통과, Transform/Collider 불변, 원본/공용 UI/대기실 씬 미변경. 신규 표시 회귀3/3, 원본 연결/자유비행7/7. 묶음 문자열 필터0개는 제외하고 개별 선별 검사 진행.
- 원본 회귀는 Undo가 늦게 반영된 A의 저장 확인 창에서 대기. Computer Use로 Unity 화면 확인·Save 후 완료, Undo flush 후 저장하도록 도구 보완. 실제 Play/MPPM 미실행.
- 선별13필터177/177 PASS, 저장 A의 네이티브 연결/개발 환경8항목 재확인 PASS. 이동 Transform/Collider 동일, 현재 씬 dirty0. 결과 .harness/local/reuse-fix/test-summary.json 및 verified.json.
- 최종 check-work 안전성·문서 규칙·컴파일 PASS. 묶음 스크립트 자동 테스트는 SKIPPED이며 별도 선별177/177 결과를 사용한다. 실제 Play 미실행, 저장 완료.
