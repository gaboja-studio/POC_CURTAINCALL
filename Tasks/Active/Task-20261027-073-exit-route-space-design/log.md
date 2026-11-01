# Log

## 2026-11-01 저장·제출 요청
- 기존65파일 SHA256 백업/A 저장·정상종료 후 origin/dev b723ca79 최신 포함 확인. 누적 A 레벨/유동 인원/시점·HUD 변경을 함께 제출하며 대기실 씬 변경0,private 변경0. doctor 동일 Task 결과/기존 HUD16·물리115·집결26 검사 재사용,실제 MPPM 재검증은 남겨둔다.
- 안전성/문서·규칙/컴파일 PASS·기존 선별 테스트 재사용. f4e7ac9a 커밋/push 완료. 제출 스크립트의 기존 다중 슬래시 브랜치 거부와 GitHub connector403은 우회 없이 gh 로그인 계정 직접 PR 등록으로 처리,도구/브랜치명 변경 없음.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/259 (feat/exit-route/design-map → dev). 팀원 검토 대기,병합/실제 Play/MPPM 미실행. 이 제출 기록은 별도 문서 커밋으로 저장한다.

## 2026-11-01 중앙 조준점·절단 좌우
- 기존65파일 SHA256 백업·저장/Unity 종료 후 origin/dev b723ca79 최신 포함 확인,같은 Task doctor 재사용.
- A 기존 HUD 프리팹 GUID 보존: 중앙5px 원형 흰 점/얇은 어두운 외곽·실제1인칭만 표시. 외줄 실제3인칭·로딩에는 숨김,절단 도식 상시 표시 유지.4부위 화면 좌우 매핑 반전,LostParts 변경 없음.
- HUD EditMode16/16 PASS. 첫15/16의 실패는 EditMode fixture에서 director.Current 미설정으로 실제 시점 경로를 못 읽던 문제였으며 fixture 명시 바인딩 후16/16. 본체 코드 우회 없음.
- 편집 검사: 중심 앵커0.5/0.5·오프셋0/0·크기5/5·원형Knob·Image1/사람Graphic1,씬 인스턴스 참조 정상/GUID 동일/A dirty0·Play stopped. 증거 .harness/local/reticle-mirror-20261101. commit/push/Play/MPPM 없음.

이전 연결·MPPM·보수·구조 제거 원문: [History/022](History/022-before-body-hud.md). 더 이른 연결/성능 기록: [History/021](History/021-before-final-submission.md).

## 보존할 상태·문제
- 민규 대기실846ccd79 기준 + 연결부만 재접합. 새 대기실 내부 보수는 하지 않는다. 합류 타이머 없음·현재 세션 전원 출발·공용 로딩·기본3인칭.
- 기존 실제4인에서 집결/뒤 문/커튼은 확인했으나 보조3개 A 로딩 중 이탈로 전원 도착 FAIL. 사용자 재실행 중지. 연속 완주 완료 아님.
- 대포 착지의5.51m 요청/복제 시차와 간헐 리타이어는 최종 경로 미확정. A 심부 낙사 높이는 수정했으나 실제 추락 재검증 보류.
- 직전2인 구조 시연 중 사용자가 Esc 중지. Scenario 원본 보조3개/Default·opt-in false 복원. 이후 추가 구조2곳/160객체·끝2봉/2연결·전용 builder 제거, 발판12 Transform 원복. 원본16봉/5연결·컨베이어 낭떠러지·마지막 화염·가변 인원 보존.
- 기존 check-work 차단: 타 담당 Task079 log138/130줄. 독립 컴파일/개별 검사와 통합 검사 생략을 구분한다.

## 2026-11-01 2키 시점·사람 신체 상태·1인칭 손
- 사용자2키1인칭 요청 및 이미지2개/사람형·노란색 불필요 정정 반영. 같은 Task doctor 기존 결과 재사용. Unity 저장/정상종료, 공개25파일 SHA256 백업·stash 후 origin/dev f4712c04 최신 동일 확인, 기존 변경 복원/에디터 재개. private 변경0. 근거 .harness/local/first-person-body-hud-20261101-015000.
- EscapePerspectiveToggle는 A에서만 로컬 카메라 요청300 우선순위로1/3인칭 전환. 기본3인칭·원본 대포/탑승 요청 보존. 사망/관전/커튼/검토 비행 시 조작 차단. 인벤토리는 A에서만3/4키로 옮겨1키 비행/2키 전환과 충돌 방지.
- 좌하단224×258 사람형 HUD/전용 정적 SDF 폰트/프리팹을 A에 추가. 기존 NetworkPlayer.LostParts만 읽어 정상회색·절단붉은빗금/4부위 한글 상태를 표시.1인칭 생존 로컬만 표시, 입력 차단/피해/네트워크 변경 없음.
- 추가 요청으로 EscapeFirstPersonArms: 원본 humanoid 팔2개를 로컬 메시로 굽고 전용 URP Overlay 카메라(31번 FirstPersonArms층/깊이 지움)에 표시. 원본 재질/피부색 유지, 걷기·달리기 흔들림·잃은 팔 숨김, 운반/봉/대포 탑승·비행은 이동용 팔을 숨김. 원본 에셋 수정 없음.
- 편집 렌더에서 MaterialPropertyBlock 필드 초기값 null 문제를 발견해 메시 생성 시 지연 초기화로 수정. 초기 도식 검사4건은 상속 OnPopulateMesh reflection 중복으로 실패, DeclaredOnly 정정. 테스트의 NUnit collection 제약도 수정했다. 최종 재검사와 구분한다.
- 최종 기능 검사: Compile PASS(up_to_date), EditMode16/16 PASS(4원본 팔 생성/잃은 팔 숨김·복원/Overlay clearDepth·해제 포함). 편집 손2장·사람형 HUD2장 확인. 씬 감사 errors0/누락 스크립트0/글리프0·toggle/arms/HUD 각1·기본3인칭·16봉/5연결·원래12발판 Transform 일치. A dirty0/Play stopped·private 변경0. 실제 Play/MPPM은 미실행이며 네트워크 동작 검증 완료를 주장하지 않는다.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 마감: check-work 안전성/ThirdParty/Knowledge 정상, 기존 타담당 Task079 log138/130줄만 HARD라 문서 FAIL·통합 Compile 생략. 해당 파일은 건드리지 않았다. 독립 Compile PASS·16검사 PASS와 구분. TagManager는31층 이름 추가 시 에디터가 최신 직렬화 형식으로 저장(빈 rendering-layer 항목 정리), 실제 기존8층 이름/충돌표 보존. 저장 완료, commit/push 없음.

## 2026-11-01 달릴 때만 손 표시
- 최신 사용자 지시: 평소 숨김,달리는 동안에만 앞뒤 움직임. 기존 변경49파일 사본/SHA256 백업(.harness/local/sprint-only-hands-current.txt), Unity 저장/정상종료 확인 후 origin/dev와 작업 브랜치 fetch, dev f4712c04 이미 포함/ff-only 동일. 에디터 재개. doctor는 기존 중복Task ID FAIL1/CLI·skill링크 WARN2,이번 코드 수정에 영향 없음.
- EscapeFirstPersonArms만 변경: 지상 이동+IsSprinting+입력허용일 때만 표시, 걷기/정지/공중에는 즉시 숨기고 위상 초기화. 양손 반대 위상으로 Z ±5.5cm 앞뒤 움직임, 상하 흔들림은0.8cm로 축소. 기존 절단/1인칭/기물 점유 조건 보존. 실제 Play/MPPM은 중지 유지.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 완료: 독립 Compile PASS(up_to_date), A dirty0/Play stopped. 신규 테스트/Play 미실행(국소 표시·움직임 수정), 기존16검사 결과는 직전 구현 기록으로 구분. check-work 안전성/Structure/Knowledge/ThirdParty 정상, 기존 타담당 Task079 log138/130줄만 HARD라 문서 FAIL·통합 Compile 생략. commit/push 없음.

## 2026-11-01 달리기 손·기존 혼합 시점·사람 실루엣만 표시
- 기존49파일 SHA256 사본 백업(.harness/local/human-only-hud-current.txt). Unity 저장/정상종료 후 origin/dev+작업 브랜치 fetch, dev f4712c04 이미 포함/ff-only 동일, 에디터 재개. 같은 Task doctor 결과 재사용.
- EscapePerspectiveToggle는 선택1인칭을 유지한 채 외줄 조작/매달림/구조/곡예·대포 탑승/운반일 때 자기 요청을 내려 원래 카메라 사용, 일반 이동/비행에서 자동 재요청. A 도착 계단을 지난 뒤 지속75점 요청도 해제해 원본 외줄 구도0점을 가리지 않게 했다. 기본3인칭/2키 선택은 유지.
- HUD 프리팹을 Unity API로 수정: 텍스트0/패널·테두리 Image0,84×156 사람 Graphic1개만 좌하단(40,28). 상태 읽기만 유지. 전용 폰트는 참조 없음 확인 후 삭제(이번에 만든 미커밋 에셋만). builder에서도 글자/박스 생성 제거.
- 팔 간격0.60→0.84m, 앞뒤±7cm/높이±2.5cm/회전 교차 달리기 동작. 원본 애니메이터 대신 Transform 복사본을 HumanPoseHandler로 굽혀 팔꿈치/손가락 자세를 굽고 메시를 구운 뒤 복사본 제거. 달릴 때만 표시·절단 팔 숨김 유지. 공식 Steam 트레일러의20초 부근 손 구도를 참고했으며 외부 게임 에셋은 가져오지 않음.
- 편집 렌더 중 손의 과도한 화면 이탈을 줄였다. Unity muscle 이름은 Left Index 1 Stretched 형식이라 Hand. 필터를 수정. 임시 조회 JSON Vector3 자기참조 실패는 문자열 좌표로 정정했고 제품 코드 영향 없음.
- 최종 독립 Compile PASS(up_to_date)·EditMode16/16 PASS. 외줄 요청 양보/해제 후1인칭 자동 복귀,텍스트0/Image0/사람Graphic1,4모델 팔 생성·절단/복원·원본 골격/메시 무변경·카메라 정리 포함. 편집 렌더3위상/건강·3절단 실루엣 확인. A dirty0/Play stopped·누락 스크립트0/원본16봉/5연결·rope camera1 활성/private 변경0. 실제 Play/MPPM 미실행,commit/push 없음.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work: Safety/Structure/Knowledge/ThirdParty 정상. HARD는 기존 타담당 Task079 log138/130줄뿐이라 문서 FAIL·통합 Compile 생략. 독립 Compile PASS/16검사 PASS와 구분한다. A 저장 완료·Play 중지·commit/push 없음.

## 2026-11-01 절단 UI 상시 표시·달리기 손 교대
- 사용자 최신 요청: UI 항상 표시,달릴 때 왼손/오른손 한 손씩 박자 교대. 공개48파일 SHA256 사본 백업(.harness/local/alternating-hands-current.txt)·Unity 저장/정상종료 후 fetch. origin/dev b723ca79까지 진전되어 stash 보존 후 fast-forward 통합·복원. private gitlink149108fa/변경0. 같은 Task doctor 결과 재사용.
- 코드/문서6곳 stash 충돌(씬/YAML 충돌0)을 해결: dev의 공용 플랫폼3인칭/도착 계단 해제·근접 카메라/개발모드 복구 유지,기존 A의2키 혼합1인칭·가변 참가 수는 함께 유지. 구형 HumanCannonPilot의 UseThirdPersonView 분기는 새 dev 공용 정책으로 대체,탈출로 선택은 독립 요청으로 유지한다. 커밋/push 없음.
- HUD에서 카메라/Normal 상태/커튼 제한 제거: A 활성·로컬 스폰 조건만 확인. 외줄/3인칭/관전/검토 시점에도 자기 LostParts를 계속 표시한다.
- 손은 반대 위상별 전방 반주기만 표시,한 번에 최대1개. 복귀 손은 아래로 내려간 뒤 반대 손 등장. 절단 팔은 해당 차례에도 숨김. 처음 테스트는 EditMode fixture의 static Current 미설정으로 UI1건 실패,fixture 명시 등록 후 재검사한다.
- 최신 확인: HUD/손 EditMode16/16(상시 UI·48위상 한 손 제한/절단 차례),집결/가변 참가 회귀26/26 통과. 독립 Compile PASS(up_to_date). A 누락 스크립트0/사람 Graphic1·텍스트0/dirty0,기존12발판 Transform 일치. 최신 dev의 입구 근접 카메라 에셋을 A 실제 approachView에도 적용,공용 코드/대기실 배치는 dev 기준 유지. 보존48경로 누락0/private 변경0. 실제 Play/MPPM 미실행,commit/push 없음.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 마감 check-work: 안전성/문서·규칙/컴파일 PASS,자동 테스트는 선택 안 함으로 SKIPPED. 별도 EditMode42/42 PASS와 구분한다. 최신 dev에서 Task079 문서 한도 문제가 해소되어 기존 HARD 차단 없음. A dirty0/Play stopped,commit/push 없음.

## 2026-11-01 대포·그네 착지의 불쾌한 낙사 보완
- 사용자 제안 승인. 같은 Task doctor 재사용, Unity 저장/정상종료·공개45파일 SHA256 백업(.harness/local/fair-landings-current.txt), origin/dev+작업 브랜치 fetch, b723ca79 이미 포함/ff-only 동일. 기존 시점/UI/손·가변 인원·12발판 원복 상태 보존.
- 실제 첫 대포 탄도는1.9초에(48,-6,69),외줄Z75와6m 측면 차이. A03 실제 총구를 수신면(47.5,-5.96,75)로 향하게 하고 발사 값만 재산정. 다른 대포·공용 물리/피해/네트워크는 변경하지 않음.
- 52_AReadableLandings: 대포3곳/하강 그네2곳의 짧은 수신면5개(모두 다음 원본 외줄과 연결),착지 판정 등록·평면/진입면 도색·다음 진행 화살표·실제 천장에 매단 현수 지지10곳. 기존12곳 일괄 확대/삭제한 구조 계단 재추가가 아니다.
- 코어 끝 대포대는 접근측으로1.2m,반출동 측면 이음은 접근 방향으로 연장/폭 보완. A05_ButtonExitSwing는 하부 화염 복도 중앙을 향하도록 회전. 기존 다른 발판/외줄 형상/버튼·봉 조건은 보존.
- 소실 외줄 visibleSeconds4→6,hidden3/한 줄 무작위 소실·5작업섬1m 간격 유지. 동시 운반 점프를 준비할 시간이 늘어나도록 A 값만 수정.
- 신규 복귀 대포 수신면 끝2표본이 기존 흉벽과 겹쳐 앞쪽으로0.6m 이동/깊이2.8m로 정정. 제품 새 코드/원본 에셋 변경 없음. 임시 판독의 Unity fake-null은 명시적 null 검사로 정정.
- 초기 검사: 무입력 대포3/3의 최초 접촉이 안전 수신면,새 수신면45/45 지지/몸통 공간 정상. 다양한 그네 각도/속력의 정적 표본은 기록했지만 성공률/실제 조작 검증으로 해석하지 않는다. 기존5.51m 착지 요청/복제 지연 리타이어 의심은 공간 보수와 별개로 남아 있다.
- 최종 배치는 EscapeBackground/52_AReadableLandings 소유로 묶어 캠페인 배경 활성/해제 경계를 함께 따르게 했고,재부모화 전후5 Collider 월드 위치/크기 동일 확인. 재검사 대포3/3 안전 최초 접촉·수신면45/45 지지/몸통 여유,원본 블록 소실0/수정6(그 외 전부 동일). 컨베이어149·질주159·출구8 기존 Collider Transform 불변. 편집4시야 외부 sky색 픽셀0. A dirty0/Play stopped,추가 자동 테스트 미실행(씬 배치 변경),commit/push 없음.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 마감 check-work 안전성/문서·규칙/컴파일 PASS,자동 테스트 선택 안 함으로 SKIPPED. 정적3발사/45공간 및 편집4시야 결과와 실제 Play/MPPM 미실행을 구분한다. A 저장 완료·private 변경0·commit/push 없음.

## 2026-11-01 불필요한 벽·선·에셋 씬 배치 정리
- 사용자 목록화/제거 승인. 같은 Task doctor 재사용,Unity 저장/정상종료·공개46파일 SHA256 백업(.harness/local/polish-cleanup-current.txt),origin/dev+작업 브랜치 fetch,b723ca79 최신 포함/ff-only 동일. private 변경0.
- 6886 Transform/Scene 객체 참조910개 조사. 초기 이름 경로/targetObjectId는 동일 이름·PrefabInstance에서 중복되어 전체 GlobalObjectId+ancestorKeys로 교정. 장식 판독용 일시 TMP는ID0이라세션InstanceID 보조키로 분리,삭제 전 검사에서만 실패했으며 그때 변경 없음.
- 목록을 제시한 뒤 비활성 구형 동선340개(82루트),바닥 이음선14,공중44m 흉벽1,85m 세로 배관1/플랜지14를 제거. 처음370개 제거 전후 활성Collider859개·활성MonoBehaviour 직렬화 값 차이0.
- 추가 발견: 유지봉3곳의 좌우 케이블이 각2개씩 겹침. 실제 HumanCannonPole.leftCable/rightCable은 새6개만 참조,이전6루트는 미참조/같은 메시·재질·월드행렬. 고정 복제본30개(루트+하위5개×6)를 추가 제거. 줄 길이/앵커/점유/반동은 유지.
- 합계400개 씬 객체/118루트 정리,원본·공용 프리팹/재질/기물 에셋 파일 삭제0. 삭제 전 외부 참조0 사전 검사. 최종 누락스크립트0/누락참조0,등록대포4/봉16/협동5/외줄11/수신면5,표시6/숨김3초 유지.
- 편집 전후4시야 외부sky색 픽셀0. 대포3/3 안전 수신면 최초접촉·45/45 지지/몸통 여유 회귀 확인. 남은 활성 배경의 월드변환은 동일(화면 크기에 따라 자동 계산되는 RectTransform4개 제외). 실제 Play/MPPM 미실행·commit/push 없음.
- 저장 씬 직렬화 비교:1438블록 삭제/신규0,남은20 Transform은 삭제된 자식 목록만 달라짐. 렌더 중 계산된 UI RectTransform4개는 저장값 변경0. 상세 polish-cleanup.md/118루트 CSV 작성.
- 폴리싱 마감 check-work 안전성/문서·규칙/컴파일 PASS(up_to_date),자동 테스트 미선택 SKIPPED. A 단일 씬 dirty0/Editor ready/Play stopped. 기존 문서 길이 WARN39/hard0,commit/push 없음.
- 사용자 전후 비교 요청: 흉벽/코어 배관/시작 복도/마지막 질주로4시점·8캡처를 백업 원본과 저장 후 씬의 격리 PreviewScene에서 같은 카메라/보조 조명으로 생성. 이번 응답 중 이미 실행 중이던 사용자 Play·활성 씬은 유지(activeUnchanged/playingUnchanged=true),플레이 테스트/게임 씬 수정 없음. compare-views.cs 및 compare-*.png는 기존 로컬 증거 폴더. 비교4구간/경계 슬라이더/상태 복구 확인.

## 2026-11-01 착지 발판 철회·외줄/화염/톱·배치 수정
- 최신7항목 승인. Unity 저장/정상종료·기존48파일 SHA256 백업(.harness/local/route-tuning-current.txt). origin/dev+작업 브랜치 fetch,b723ca79 이미 포함/ff-only 동일,로컬dev도 동일. doctor는 같은Task 결과 재사용. start-work.ps1은 기존 feat/exit-route/design-map 이름을 regex에서 거절했으나 실제 현재 브랜치 일치/최신 포함을 별도 확인하고 그대로 진행,도구 규칙은 변경하지 않음.
- 에셋 겹침/관통·외줄과 착지 받침 중첩 금지 확정은 Decisions/escape-placement-clearance.md에 기록. 현재 실제 기믹/배선24/기존 대기실·공용 코드·원본 에셋은 보존.
- 52_AReadableLandings113객체(수신면5·지지/도색) 제거,원래 접근면2곳 확대는 이전 백업 PreviewScene에서 정확한 Transform 원복. A05_ReboundToButton+지지23·주변4조명12/연결 지지선3·무기능 대포 제어반/장식5를 제거,합계156객체/24루트. 봉 배열 요소를 먼저 정리,나머지 외부 참조0.
- 외줄11개 지지 폭/sceneShape0.4→0.55m,원래 굵기·길이 유지. 처음 Fiber 재질 대상30개는 매달림 줄이었고,외줄 실제 시각선은 별도 brutal-steel132개임을 확인해 모두 A 전용 밝은 황금색/약한 발광 재질로 적용. 원본 재질은 변경하지 않음.
- 추격 진행4→5.2초,시작 유예20/예고3/분출3 유지. 두 절단기 발사 메시 지름1.1→2.2m/절단 반경0.55→1.1/두께2배,수평·2초 주기 유지. 런타임과 같은 복제/루트 스케일1에서도 실제2.200m/두께0.186m와 판정 일치 확인.
- A06_FlameExitSwing 및 지지4개를 대포 방향1.25m 이동(Z90.5→91.75),잡기2.6→3.3m로 입구 접근 보존. 변경 전156자세 중20 화염 접촉,변경 후308자세×주기 표본 화염/고정 충돌0. 초기 자리 거리3.214<3.3m/이동 경로 장애물0. 정적 도약80중64 대포대 착지는 실제 성공률이 아니다.
- 대포3/3은 보완 발판 없이 원래 외줄 최초 접촉/실제 지지 판정 통과,외줄99/99 지지·몸통 공간 정상. 최종 누락스크립트/참조0,대포4/봉15/협동5/외줄11/수신면0. 씬 diff599블록 삭제/신규0,남은 변경은 Renderer162/Collider11/Transform15/의도한 스크립트 필드6개뿐. 기존 코드/UI/손은 백업 해시와 동일.
- 변경·범위·정적 검사 상세 rope-and-hazard-tuning.md. 실제 Play/MPPM·commit/push 없음,수정3구간 편집 렌더 확인.
- 마감 check-work 안전성/문서·규칙/컴파일 PASS(up_to_date),자동 테스트 미선택 SKIPPED. Knowledge15폴더 정상/문서 WARN39·hard0. A 단일 씬 dirty0/Editor ready,Play 미실행·저장 완료.

## 2026-11-01 양방향 외줄·실체 충돌·화염 절단
- Unity 저장/정상종료·기존54파일 SHA256 백업(.harness/local/route-contact-current.txt),origin/dev+작업 브랜치 fetch,b723ca79 최신 포함/ff-only 동일. 같은 Task doctor 재사용,기존 변경/branch/private 보존.
- 사용자 정정: 반대편 진입에서 W 대신S가 필요함. 실제 Collider 보행은 수정 전22/22 통과했으나 CourseControlSwitcher가 조작 전환 때 작성축을 강제한 것이 문제. A opt-in으로 플랫폼 시선축(없으면 몸 방향)을 줄축과 같은 부호로 맞추고 다음 프레임에도 유지,옆줄 좌우/동료 뒤 착지 부호도 수정.
- EscapeCooperativeSpan의 전개 미완료 때 Collider 비활성화가 직접 원인. 다리3곳6철판 상시 실체 충돌·-80 순서/Physics.SyncTransforms 적용. 진입 차단 시안6개는 원래 도약 진입에 영향을 주어 제거,최종 추가 장벽/발판0. 초기 교면 밖 공중에서 시작한 통행 표본6실패는 유효한 교면 내부 출발로 정정했다.
- 활성 MeshRenderer3416 전면 조사,최종 물리 검토1525/대응1496·잔여29는 효과/표면 장식/글자/별도 반동·운반/대포 투입구라 추가 수리 대상0. A 인스턴스650곳 실제 메시 Collider 추가/원본 파일 변경0. 회전 AABB만으로 물리 대응을 인정하던 초안을 실제 ClosestPoint/표면 검사로 교정했다.
- 첫652개 보완에서 세계 글자1개와 트램폴린 반동 몸판1개는 제거: 글자는 물리 자산이 아니고,후자는 운반자 자기 간섭1건을 만들었다. 최종650/650 표면 반응·운반3장치 자기 간섭0. private 수정0,대기실/공용 UI/기존 시점·손·착지 발판 철회 유지.
- 일반 화염14곳 예고0.65→1.15초/총주기5초/좌우위상2.5초,fireSeversLimbs A만 true. HostContacts의 기존 ServerCut 사용. 박스 접촉/고정 회전·밀도 필터를 즉사 여부와 분리해 형상/활성 보존. 별도 추격은 원래 ServerKill/유예20초/진행5.2초 유지.
- 선별 EditMode: CooperativeUpgrade23/23,SurfaceControl32/32,RouteGeometry46/46,PursuitFire14/14 =115/115. 초기 파이프 구분 필터는 부분 문자열로 해석되어 NO_PROJECT_TESTS였고,클래스별 개별 실행으로 실제 건수를 확인했다.
- 최종 실제 CC 외줄22/22·다리전개30자세 면관통0/양방향24/움직이는 교면600표본 아래관통0. 새 충돌 후 대포3/3 원본외줄 착지·줄99/99/그네308 간섭0. 화염6쌍 주기 표본 동시분사0. 누락스크립트/참조0,대포4/봉15/협동5/외줄11/수신면0. 범위/증거 route-contact-fixes.md/650행CSV 참조.
- 실제 Play/MPPM·commit/push 없음. A dirty0 저장,일부 정적/편집 회귀를 실제 온라인 완주로 해석하지 않는다.
- 마감 check-work 안전성/문서·규칙/컴파일 PASS(up_to_date),자동 테스트는 별도 선별115/115와 구분해 SKIPPED. Knowledge15폴더 정상/기존 길이 WARN39·hard0. 최종 A 단일 씬 dirty0/Editor ready·Play stopped,commit/push 없음.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
