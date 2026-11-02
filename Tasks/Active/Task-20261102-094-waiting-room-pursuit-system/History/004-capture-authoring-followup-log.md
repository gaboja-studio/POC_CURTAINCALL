## 포획 후 공연에서 온전한 몸으로 기어가는 결함
- 사용자 실행 중 Tightrope의 실제 NGO 호스트: LostParts=13(양다리/왼팔), HumanoidModel.Stage=3, Original.enabled=true·Body3.enabled=false. PursuitVictimPose는 이미 제거됨. limb-bug-live-before.json 및 실제 렌더러 조회로 상태 확보. 현재 조건을 Apply하면 Original=false/Body3=true로 바로 복구됨을 확인.
- 원인: PursuitVictimPose가 포획 전 Renderer.enabled를 기억했다가 OnDestroy에서 복원하여 포획 중 변경된 부상 메시 선택을 덮어썼다. 부상 데이터가 바뀌지 않아 이후 표시 갱신도 없었다. 기어가기 자체는 외팔 상태의 의도된 동작이다.
- 수정: Hidden이 되는 시점에 forceRenderingOff를 저장·적용하고 종료 시 그 임시 가림만 복원한다. Renderer.enabled를 부상 표시 코드에 맡겨 살아 있는 사지를 되돌리지 않는다. 기존 다른 시스템의 forceRenderingOff도 보존.
- 회귀 PlayMode 2/2 PASS: 가림 중 부상 변경/종료 2회(8.22초), 실제 Start→LAN 호스트→Dev 2일차 아침→추격 포획→공연 전환 2회(51.84초). 후자는 LostParts=13/State=Normal/포획 연출 없음/Body3만 표시/가림 해제까지 검사. capture-visibility-tests.json, capture-performance-tests.json.
- 재개 시 doctor는 같은 Task에서 반복하지 않음. 먼저 사용자 Play 증거를 확보하고 정지·미저장0 확인 후 이 워크트리 에디터만 종료했다. origin/dev fetch 결과 28개 앞섬(b0669a96). ff-only 반영은 기존 BaseGameSettings/escape-labels/EscapeSceneTransfer 변경을 덮게 되어 Git이 거절했다. 원본 변경과 인덱스를 보존하고 동기화는 보류, 현재 브랜치에서 결함을 수정/검증했다. commit/push 없음.
- Pipeline screenshot game은 활성 Tightrope 대신 잔존 대기실 카메라를 잡아 limb-live-reapplied.png를 게임 화면의 합격 증거로 사용하지 않는다. 실제 Game 뷰는 Computer Use로 별도 확인한다.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 이번 수정 묶음 check-work 1회: 안전성/문서·규칙/컴파일 PASS, 테스트 옵션은 SKIPPED이며 별도 선택 실행한 위 PlayMode 2개는 모두 PASS. 에디터 재개 전 미저장 씬0 확인. 문서 길이 경고43/hard0, 기존 private 에셋 변경은 보존.
- 최종 별도 실제 Play: Start→LAN 호스트→Dev 2일차→포획→Tightrope로 다시 전환. LostParts=13/State=Normal/Stage=3/Original=false/Body3=true/모든 forceRenderingOff=false/포획 연출 없음 확인(limb-bug-live-after.json). Computer Use로 실제 Game 뷰 및 임시 근접 시점에서 양다리·왼팔이 없는 외형 육안 확인. 임시 카메라 요청 해제·개인 Dev 설정 복원, 확인 가능한 공연 Play를 켜 둠.

## 근접/추격 화면과 기획자 배치 창
- 시작 시 에디터 Start 정지·dirty=false 확인 후 해당 에디터만 종료. origin/dev fetch(b0669a96) 성공, ff-only 병합은 기존 BaseGameSettings/escape-labels/EscapeSceneTransfer/Characters index 수정과 겹쳐 중단. 기존 작업 보존·현재 브랜치에서 계속, commit/push 없음. doctor는 같은 Task에서 반복하지 않음.
- DBD Visual Terror Radius 공식 개발자 설명과 Dying Light 공식 추격 단계 업데이트를 조사했다(링크 plan/guide). 근접은 기존 깊이 검사 외곽선(5m,빨강3px), 실제 로컬 대상 추격은 중앙을 비운 빨간 테두리(.65강도/.19폭/.35맥동). 효과 사용/강도/맥동을 설정에서 조절. 타겟 ID를 NGO 스냅샷으로 전달하여 다른 단원 추격과 구별한다.
- PursuitPlacement에 타입·개별 순찰 경로를 저장. 기존 26영역/11체의 위치·방향·타입·경로를 보존하여 전환, 기존 스폰/경로도 보존. 단계 달리기 m/s 값 추가(0은 기존 배율 호환).
- Mannequin Designer: 방 좌우/목록/구조도 전환, 실내 탑뷰 카메라와 천장 절단, 10종 팔레트 드롭, 바닥 Y 보정, 선택/타입/제거/회전/이동, 순찰 지점 추가/드래그/순서/제거, 단계·알림·연출 설정, Undo/Redo/저장. Play에서는 편집 잠금·실제 Actor 위치/동작/방별 수를 표시한다. 큰 영역이 작은 방/마커를 덮지 않도록 구조도 렌더 순서 보완.
- EditMode 3/3 PASS(0.51초): 바닥/천장/허공, 시작 위치 회전·이동 시 경로 보존/재정렬/삭제 Undo, 기존 배치 전환 멱등성·독립 경로. 최초 3개는 테스트 러너의 unsaved 임시 씬에 additive scene을 만드는 Setup 제약으로 실패하여 fixture만 Single로 수정 후 통과. designer-edit-tests.json.
- PlayMode 최종1/1 PASS(17.88초): Start→LAN호스트→대기실, 배치11체 모두 생성·Placement연결, 근접 아웃라인 셸/빨강, 타인 추격 시 효과 없음, 로컬 추격 시 표시, 거리/추격 해제 및 OnDisable 셸/Canvas 정리. pursuit-feedback-play-tests.json.
- 추가 실제 플레이에서 Awake/OnEnable 순서 때문에 isActiveAndEnabled로 건너뛰는 배치 누락 발견(5/11). 직렬화된 enabled+activeInHierarchy로 수정하고 위 테스트에 전체 개수 검사를 추가. 이후 별도 Start→호스트→대기실에서11/11 생성, 만료18초 시 Walk11체/10체 시작점 대비 .3m 이상 이동 확인.
- Computer Use 실조작: Male02 팔레트 드롭→마우스 이동(Y=-4.985)→15도 회전→감시 지점1개 추가 확인. 선택 시 시야·5개 기존 순찰 경로, 단계별7.2m/s/시야/알림/화면설정 표시 확인. 임시 Male02와 그 경로는 제거하여 최종11체 유지. Play 실시간 구조도/탑뷰에서 이동 확인.
- 실제 Game 뷰에서 로컬 추격 스냅샷을 고정하여 빨간 윤곽선1체/화면 테두리/중앙 시야·인벤토리 유지 확인(Intensity=.6203). 이 시각 검토는 AI를 잠시 멈춘 수신 상태 재현이며, 실제 순찰은 앞 단계에서 별도로 확인했다. 임시 카메라 해제·Dev 개인 설정 복원·Play 정지. 다인 네트워크 수신 화면 실기는 기존 미완료 항목.
- 원본 WaitingRoomBase SHA256 C63F43D893A896B96C9955468499E425C50A5049904E3F99DC12F22975D310F7 유지. 새 C# 메타 생성 확인. git diff --check 통과(기존 줄바꿈 경고만). 조작 설명은 designer-guide.md.
- 최종 check-work 1회 PASS: 안전성96파일/문서·규칙/컴파일 up_to_date. 테스트 옵션은 SKIPPED이며 선택 실행한 EditMode3+PlayMode1은 위 보고서대로 PASS. 지식 검사15폴더 PASS, 문서 길이 경고43/hard0와 기존 private 에셋 경고 유지. 검사 시 Start 정지·dirty0. 검토 후 복제 대기실과 배치 창을 열어 두었다.

## 층별 구조도와 현재 방 강조
- 사용자 요청으로 평면도 아래에 층별 측면도를 추가. 앞뒤/높이(Z/Y)·좌우/높이(X/Y) 전환, 두 지도에서 클릭 방 전환과 기존 Play Actor 위치 갱신 경로를 공유한다.
- Collider 영역은 여러 층보다 높게 잡혀 있으므로 실제 배치/순찰 지점 Y와 바닥 탐색으로 층 높이를 얻어 2.1m 높이의 방 도식으로 표시. 현재 씬26영역은0/-5/-10m로 확인. 감지 영역·게임플레이·씬 배치는 변경하지 않았다.
- 현재 방의 주황색 배경과3px테두리/외곽괄호를 모든 방보다 나중에 그려 겹쳐도 보이게 함. 현재 방 이름을 지도 위에 표시. 측면 겹친 방의 개수는 현재 방과 툴팁에 표시하여 숫자 뭉침 방지.
- 에디터 정지·dirty0 확인→종료 후 origin/dev fetch 성공(861fb672). ff-only는 기존 BaseGameSettings/escape-labels/EscapeSceneTransfer/CommonInventoryUI/Characters index 수정 보호를 위해 Git에서 중단. 기존 변경 보존, commit/push 없음. doctor 같은 Task라 반복하지 않음.
- 컴파일 PASS. Computer Use로 두 방향 전환 및 측면도의 -10m 기계실 클릭→탑뷰/평면도/측면도/현재 방 이름 동시 변경 확인. 새 자동 테스트는 추가하지 않음(에디터 표시 변경). Play 재실행은 생략했으며 실시간 위치는 기존 Actor 목록을 새 투영으로 그리는 경로를 유지.

## 탑뷰 이동·자동 방 타겟·전체 배치 합계
- 빈 공간 좌드래그/중클릭 드래그 팬, 현재 층의 중심점 자동 방 선택, 층 선택/자동 선택 토글/F 맞춤 추가. 자동 타겟 변경은 카메라 중심·확대율을 유지한다. 방 밖이면 강조/선택/배치를 해제하고 마지막 방 맞춤으로 복귀한다. 현재 층의 주변 방 마커도 회색으로 표시한다.
- 전체 배치 합계는 모든 방의 유효 Placement를 중복 제거하여 계산하고 기존 Count 방식도 지원. 방 목록/현재 방에 방별 수를 표시하고 Play 실행 Actor 수는 별도로 구분한다. 현재 씬 합계10체와 실제 참조10체 일치. 이전 기록의11을 기대한 첫 진단은 개수 가정만 실패했으며 현재 배치를 바꾸지 않고 실제 참조와 대조해 확인했다.
- Computer Use: 탈의실→사무실 빈 곳 드래그에서 방/두 지도 강조 동시 변경, 사무실 마네킹 선택 시 시야/순찰5지점 표시, 방 밖 팬 시 타겟/강조 해제, 방 맞춤 버튼 복귀 확인. CLI 이벤트/상태 검사: 방0→4→지상13, 방 외부 판정, zoom1.7/span20 유지, F 맞춤, 중클릭 팬/마우스업 해제 모두 PASS. 씬 dirty=false. 새 NUnit 테스트/Play 재실행은 생략(에디터 탐색 변경).
- 재개 시 정지·dirty0 확인 후 해당 에디터 종료→origin/dev fetch(861fb672). ff-only는 기존 BaseGameSettings/escape-labels/EscapeSceneTransfer/CommonInventoryUI/Characters index 변경을 보호하기 위해 거절됨. 기존 작업 보존, commit/push 없음. 같은 Task doctor는 반복하지 않음.

## 포획 정렬·청각/Behavior·렌더·유혈 개선
- 시작 시 정지·dirty0 확인 후 에디터 종료, origin/dev fetch 성공(861fb672). ff-only는 기존 5개 로컬 변경과 겹쳐 Git이 중단했다. 원래 작업 보존, commit/push 없음. 동일 Task doctor는 반복하지 않았다.
- 포획은 VictimPose의 월드 anchor/upright를 스냅샷으로 전달하고 누운 골반/머리 축에 Actor를 배치한다. 몸 루트만 바라보던 기존 회전과 포획 중 상체 yaw를 제거. 실제 휴머노이드 네 방향과 기존 부상 외형 회귀 통과.
- UPM으로 com.unity.behavior@1.0.16 설치. _ThirdParty/curtaincall/Pursuit/Behaviour에 실제 authoring+compiled 그래프(반복 Start→Try In Order→6개 Action) 생성·씬 Director에 연결. 호스트에서만 수동 tick, 청각은 이동 거리/동기화된 달리기·공중 상태를 사용한다. 소리 위치 기억, 준비/추격/놓친 지점 수색/복귀와 단계별 설정을 배치 창에 추가. Play 마커에 행동·렌더 상태 표시.
- 모든 플레이어와 최대 상대 접근 속도×(최소5초+.3초)+여유8m 밖이고 카메라 가시/근접·포획 예외가 아니면 forceRenderingOff만 적용. 현재 설정 경계77.96m. AI/이동/동기화 지속 및 이전 가림값 복원. 실제 자동 경로: 카메라 뒤 먼 Actor 숨김→플레이어 근접 복귀→원위치 복구 모두 PASS(render-culling-check.json). 절대 FPS/드로콜 수치는 측정하지 않았다.
- 기존 게임+개인 유혈 설정 연동. 한 파티클 시스템의 반복 분출, 최대12개 재사용 화면 혈흔, 설정 OFF/Hidden/종료 시 즉시 정리. PNG/재질/프리팹 모두 _ThirdParty/curtaincall/Pursuit/Behaviour. 원래 BloodSpurt 리소스는 수정하지 않았다.
- 첫 그래프/에셋 생성 및 별도 호스트 시작 eval은 Pipeline 5초 응답 제한으로 시간 초과. 중복 호출하지 않고 상태 조회로 정상 완료·연결·clean 확인. 리로드 중 recompile_status 400은 ready 확인 후 정상 결과 확보. 첫 신규 테스트 코드의 기존 partial Private 헬퍼 이름 충돌은 PursuitPrivate로 고쳐 컴파일 PASS.
- 신규 PlayMode2/2 PASS(35.8초, behaviour-play-tests.json). 마지막 코드의 두리번 주시점 보완 후 전체 관련 PlayMode6/6 PASS(103.55초, pursuit-behaviour-regression.json): 그래프 행동/렌더, 네 방향/유혈, 기존 부상 가림 복원, 포획→공연2회, 근접·추격 피드백, Start/재진입 ESC·인벤토리.
- 별도 Start→LAN호스트→Dev 2일차→실제 AI 접근/포획 확인. elapsed1.7066, 머리 방향 dot1.0/위치 오차0/혈액3회/graphReady=true(behaviour-live-capture.json). Computer Use 실제 Game 뷰에서 골반 위 정렬·머리쪽 공격·몸 분출·화면 혈흔과 탑뷰 Capture 표시 확인. 검토용 시점 요청은 Play 종료로 제거, 개인 혈흔 설정 복원.
- 현재 사용자 배치에는9체가 있어 실제 생성9체 확인. 이전 11체 고정 검사 대신 현재 유효 배치 수와 생성 수를 비교하도록 회귀를 수정했다. 이번 구현은 방 배치/마네킹 타입을 바꾸지 않고 Director의 그래프/효과 참조만 연결했다. 새 기능의 다인 수신 화면 실기와 최종 공포감 평가는 미검증.
- 최종 check-work 1회 PASS(2026-11-03): 안전성123파일/문서·규칙/컴파일, 선택 테스트 옵션 SKIPPED이며 별도 PlayMode6/6 PASS는 위 보고서에 기록. 문서 길이 경고44/hard0와 기존 private 변경 경고 유지. 원본 WaitingRoomBase SHA256 불변. 복제 대기실/배치 창을 열어 두고 Play 정지·씬 dirty0 확인.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 대기실 F9 타이머 치트
- 시작 시 Play 정지·씬 clean 확인 후 에디터 정상 종료, origin/dev fetch(861fb672) 성공. ff-only 병합은 BaseGameSettings/escape-labels/EscapeSceneTransfer/CommonInventoryUI/Characters index의 기존 로컬 변경으로 중단되어 보존했다. 에디터를 같은 워크트리로 다시 열었다. 커밋/push 없음.
- F9의 RestTime 전용 영역에 초기화·추격 철회, ±60초, 60초 남음, 즉시 만료/추격 시작, 설정 배열 기반 단계 고정/자동 복귀 추가. Dev+호스트/오프라인 제한. 현재 시간대 기본 설정 복원, 양수 시간은 추격 철회, 안전 구역에서도 강제 추격 작동.
- 단계 override는 런타임에만 유지하며 AI/시야/청각/알림 공통 적용. ResetStep은 override를 정리한다. 철회는 부상 보존·리타이어 해제 후 포획 Pose/혈흔/입력 정리·배치 복원·빈 피해자 Snapshot 전송. 수신 측은 빠진 피해자와 이전 포획 상태를 정리한다.
- PlayMode PursuitCheats_ 2/2 PASS(23.82초): 시간 증감/강제 만료/단계 고정/자동/Dev·권한 가드, 포획·리타이어 철회/부상 보존/재추격, 수신 Snapshot 정리. EditMode PursuitRulesTests 7/7 PASS. 결과 pursuit-cheat-play-tests.json, pursuit-cheat-rules-tests.json. 실제 별도 클라이언트 접속 검증은 미실시.
- Start→임시 LAN호스트→Dev 2일차 대기실→F9 화면 확인. 실제 1분 버튼 클릭 60초 적용, 4단계 버튼 클릭 Timer=Pursuit/5.218초/4단계 고정 확인(pursuit-cheat-ui-review.json). 단계 버튼 잘림을 줄바꿈/내용 폭 제한으로 수정하고 재확인했다. 검토 후 추격 철회·GameView 최대화 해제·Play 정지. 최종 검사는 아래 기록.
- 최종 check-work 1회 PASS: 안전성132파일/문서·규칙/컴파일 up_to_date, 기존 문서 경고44/hard0. 자동 테스트 옵션은 SKIPPED이고 별도9/9 PASS 결과는 위 파일에 기록. 복제 WaitingRoomPursuit 재개방·씬 dirty0, 다인 수신 화면 실기만 남음.

## 2026-11-03 배치 창 공통 층 필터
- 에디터 Play 정지/씬 clean 확인 후 정상 종료·origin/dev fetch 성공. 기존5파일 변경으로 ff-only 병합 중단, 보존 후 같은 워크트리 에디터 재개방. doctor는 같은 Task에서 이미 실행하여 반복하지 않음.
- 원인: 구조도는 모든 층을 겹쳐 표시, 탑뷰 farClip=70은 아래10m까지 렌더링, 클릭은 중심 방의 배치만 검사했다. ActiveFloor/VisibleRooms를 평면·측면·탑뷰·선택/드롭에 공유하고 탑뷰 farClip을 바닥 아래 .35m로 제한, nearClip도 위층 바닥 전으로 제한했다.
- 층 선택을 창 상단 공통 도구줄로 이동하고 선택 층 방/배치 수 표시. 방 선택 즉시 해당 층 반영, 층 변경은 유효 방 타겟/없으면 가까운 방 맞춤. 보이는 같은 층 마커 클릭은 소속 방도 선택, 드롭은 실제 놓는 위치 방을 사용. Play 마커/개수도 현재 높이로 층 필터.
- EditMode PursuitDesignerFloorTests 2/2 PASS(0.14초): 겹친 층 방/마커/히트 필터·옆 방 마커 선택·방 전환 즉시 층 일치·카메라 아래층 제외. 처음 복합 필터 문자열은0건이라 PASS로 처리하지 않고 단독 필터로 실행했다. designer-floor-tests.json.
- 실제 UI: MachineRoom=-10m에서4방/4체만 구조도 표시, 회색 ST_Stairwell 마커 직접 클릭→소속 방/마네킹/순찰 선택 성공. 층 목록0m 클릭→19방/4체/하단 절단-0.35m/다른 층 사라짐 확인(designer-floor-ui-review.json). 기존 전체9체 유지, 씬 dirty=false. 검토 후 MachineRoom으로 복귀·창 최대화 해제. 런타임 실기 재실행은 하지 않음.
- 최종 check-work 1회 PASS(135파일 안전성/문서·규칙/컴파일 up_to_date, 문서 경고45/hard0). 테스트 옵션 SKIPPED이며 별도2/2 PASS 결과는 위 기록. 씬 dirty0·Play 정지 유지.

## 2026-11-03 로비 지면 판정
- 재현: Lobby bounds minY=-3, Ground ray 시작=-.5에서 아래로 검사하여 Ground=false. 동일 X/Z의 Y2.4 하향 ray는 RoundFloor Y0/normalY1 검출. 바닥 충돌체가 아니라 검사 기준 버그.
- FloorReference(배치→순찰→스폰→볼륨 하단)로 해당 층 위2.4m에서 바닥 아래.75m까지만 검사. NavMesh도 같은 층 높이 제한. 다른 층 바닥으로 잘못 내려가는 것을 방지하며 창의 fallback 높이도 공유.
- 정상 종료가30초 timeout을 반환(사용자에게 보고, 강제 종료 없음). fetch 성공/ff-only는 기존5파일 변경으로 중단, 재개방 후 단일 Editor PID45400 ready 확인. 기존 변경 보존·commit/push 없음.
- EditMode PursuitAuthoringTests4/4 PASS(.22초): 일반 바닥/천장/허공, 로비형 볼륨과 실제 Add·아래층 거부, 마이그레이션·순찰 이동/Undo. 실제 로비3×3지점9/9 Ground=true,Y=.015 확인(lobby-ground-tests.json,lobby-ground-review.json). 씬 dirty=false, 배치 창 Lobby로 선택. 최종 검사는 아래 기록.
- 첫 check-work는 누적 log138줄 제한으로 문서 FAIL. compact-docs에 따라 완료 초기 기록 원문을 History로 보존·확인 후 분리. 재검사 안전성138파일/문서/컴파일 PASS, 경고45/hard0, 별도 테스트4/4 PASS. 현재 씬 clean/Play 정지.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
