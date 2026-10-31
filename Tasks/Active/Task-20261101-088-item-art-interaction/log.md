# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.
# 2026-11-01 착수
- doctor 1회: 샌드박스 안에서는 third-party/Unity 조회 실패. 외부 조회에서 저장소 clean, Unity 6000.3.25f1 ready, CLI 최신 beta.15, gh MoHoDu 확인. 기존 Task 번호 중복은 본 작업과 무관한 기존 문제.
- Unity eval 1회 main thread timeout. 저장된 Start 씬 확인 후 CLI graceful close 성공, fetch/전용 브랜치 생성/ff-only 동기화 완료, CLI reopen 성공.
- Computer Use 창 목록에 Blender 미표시. 명시 실행 요청은 app approval timeout. MCP 연결 정상. 원화는 내장 imagegen 사용.

## 2026-11-01 손바닥 관통 피드백 수정
- 사용자 실제 Player 이미지에서 말뚝 뽑개의 손바닥 관통 확인. 최초 560포즈 검사 중 grip 기준점 일치 항목은 관통을 검사하지 못한 거짓 합격이었다.
- 재개 전 현재 씬을 저장하고 Unity 종료 확인 후 origin/dev fetch/ff-only 동기화: 이미 최신. close CLI는 제한시간 초과를 반환했으나 대상 에디터 종료가 실제 확인되었고 해당 프로젝트를 다시 열었다. 기존 변경 보존.
- 원인: FBX 루트의 축 변환을 손 부착 회전으로 덮어썼다. Identity 프리팹 루트 + FBX 자식으로 변환을 보존하고 UV 손잡이 좌표 부호를 수정했다.
- Blender에서 말뚝 뽑개 폭을 0.7배(손잡이 직경57→40mm), 28개 동작 엄지 굴곡 재작성, 두 아이콘 재렌더. Unity에서 손바닥 바깥 접점과 캐릭터별 손가락 길이/두께에 따른 원통 쥠 보정 추가.
- 확대 증거는 Art/grip-*-canonical-*.png를 사용한다. before/offset/thumb/opposed/fit/wrapped/fitted/final 등은 수정 중간 증거로 최종 근거가 아니다.
- 메시 검사: 손바닥/손가락 삼각형 내부 표본과 손잡이 원통 사이 거리, 2mm 허용 오차. 원통이 아닌 헤어핀 묶음은 해당 검사 대상에서 제외. 온라인/연속 면 전체 무충돌 검사 대체 아님.
- 최종 Editor 검사: 4 Avatar/28 clip/560 pose, 손잡이 표면480표본 포즈, 실패0. 손바닥 최소여유2.345mm, 손가락 최저여유-0.652mm(2mm 허용 내). 최초 grip 좌표일치 검사를 이 검사로 교체했다.
- 실제 Player PlayMode: 4캐릭터×4부상×들기/타격32표본 모두 통과, frame1697→3493 실제 진행. 캐릭터 교체 때 ItemRightHand가 ItemStrike 기본 상태에 머물던 문제를 BindModel의 itemLayerState 초기화로 수정했다.
- 실제 Rabbit PlayMode 확대/전체 증거: Art/grip-live-rabbit-0-hand.png, grip-live-rabbit-0-full.png. 실제 Player 손바닥 여유8.882mm, 손가락 표본 여유-0.022mm.
- Unity recompile_status completed/failed=false. verify-knowledge OK(15 folders). commit/push 없음.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- check-work 후 본 작업의 대문자 증거 파일98개를 소문자로 정리하고, 기물 쥠 함정을 item-grip-false-pass.md로 분리. 재검사 Structure OK / Knowledge OK. 남은 Context HARD는 기존 Task-20261029-079-waiting-room-m2/log.md 138줄(제한130) 한 건이며 본 작업에서 수정하지 않았다. Unity 직접 재컴파일 completed/failed=false.
- 실제 장비 슬롯(UV) 선택 후 HeldItemVisual=null 확인(frame8977), 말뚝 뽑개 슬롯으로 복귀. 실행 오류는 없었으며 콘솔의 과거 error1건은 Pipeline의 'editor status' 명령 별칭 불일치이다.

## 후속: UV·이동·오른팔 추진·열쇠
- Unity 저장/종료→origin/dev fetch/ff-only(이미 최신)→같은 프로젝트 reopen. 기존 변경 보존, doctor는 같은 Task에서 재실행하지 않음.
- ItemRightHand mask에서 Body/Head를 제거하고, base/상체 레이어 전환 캐시를 레이어별로 구분했다. 이동 중 두 다리 손실은 Blender의 ItemCrawl_Cut2/3 전신 동작으로 오른팔을 뻗고 당긴다. 왼팔은 가슴 쪽에 둔다. NetworkPlayer.PresentationMoving은 이미 미스폰 시 mover로 위임하므로 최초 의심한 오프라인 판정은 별도 결함이 아니었다.
- UV는 오른손 IK·손목 방향/본체 반회전으로 몸 앞 실제 손잡이를 잡고, UvFlashlight LateUpdate에서 실제 본체 중심을 광원 시작점으로 쓴다. 실제 우클릭 입력 확대 증거 uv-runtime-rabbit-0-hand.png.
- build-small-tools.py: 단일 머리핀과 황동 route-key 모델/아이콘. 전체 재생성은 build-props.py 뒤 이 스크립트를 실행한다. build-right-crawl.py: 오른팔 추진4클립. 총8모델/16아이콘/32클립.
- 열쇠는 ItemCatalog 슬롯 기물이 아니라 기존 ExplorationGate/ExplorationState 공유 경로다. 획득 지점 모델과 열쇠 사용 gesture kind3를 연결하고 판정/인벤토리 규칙은 유지했다. 실제 검증 씬에 Key1 줍기와 선행 조건 문을 추가했다.
- 두 슬롯이 차면 시험 지급 버튼이 선택 기물을 바닥에 내려놓고 교체하도록 개선. 머리핀/열쇠를 포함한 8개 버튼을 두 줄로 표시한다.
- 실제 입력 검사 초기 실패: 비포커스 Editor가 가상 키 입력을 차단했다. 검사 동안 InputSettings를 복제하고 IgnoreFocus/AllDeviceInputAlwaysGoesToGameView를 적용, 원복한다. 원본 임시 설정의 UnloadUnusedAssets 소멸로 결과 저장이 누락된 종료 오류도 보존 플래그/선저장으로 수정한 뒤 재실행했다.
- 최종 이동: travel-validation.json, 4캐릭터×4부상×말뚝/머리핀/UV 48건 실패0. 실제 W 이동, Walk+Hold/Aim 또는 ItemCrawl_Cut2/3, 손바닥·손가락 표본2mm 허용, UV 본체 광원 위치를 검사했다. 기어가기 오른손 이동14.1~17.6cm, 왼손1.16~1.43cm(엉덩이 상대좌표).
- 최종 사용: key-hairpin-validation.json 11항목 실패0. 실제 F 누름/뗌으로 Key1 획득·월드 표시 숨김·슬롯 불변·문 개방·오른손 열쇠 동작, 머리핀 상자 개방·내구도5→4 확인. 바닥 드랍 기물이 더 가까워 최초 상자 검사가 줍기로 향했으므로 상자 이동 버튼도 검증 위치(.35,0,-.5 상대)로 조정했다.
- travel-rabbit-0-uv-lamp-hold.png / travel-rabbit-3-uv-lamp-crawl.png / travel-rabbit-3-stake-puller-crawl.png 실제 Player 화면 확인. 온라인 복수 클라이언트와 사용자 최종 플레이 품질 확인은 남음. Computer Use 직접 조작은 이전 Escape 중지 후 재개하지 않음.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 검사: Unity recompile_status completed/failed=false. check-work 안전성/Structure/Knowledge/ThirdParty OK, 기존 Task-079 log.md 138줄(hard130) 때문에 문서/규칙 FAIL·후속 자동 컴파일/테스트 SKIPPED. 별도 실제 입력 검증48+11항목 통과. 시험 씬을 초기 상태로 재시작해 사용자 검토 준비. commit/push 없음.
- 타격 피드백 재개: Unity 저장·graceful close 성공, 종료 PID 확인 뒤 fetch/ff-only(최신), 동일 프로젝트 reopen. Blender build-overhead-strike.py로 ItemStrike 4종을 머리 위 준비→힘 모음→하향 큰 원호→복귀로 재작성. 1.2초/접촉0.66초 유지, 메타/GUID 유지. 전체 재생성 시 build-interactions/refine-grips/build-right-crawl 다음 build-overhead-strike를 실행한다.
- 타격 최종 검증: overhead-strike-validation.json 4캐릭터×4부상×9시점144포즈 실패0. 오른손 머리 위 준비/앞아래 타격 궤적, 손잡이 메시 표본2mm 허용, 캐릭터/도구 바닥 관통 검사. 준비→접촉 말뚝 끝 높이 차1.08~1.21m. Rabbit 정상/오른팔만 실제 리타게팅 이미지 review-rabbit-stake-puller-*-itemstrike-40/55.png 확인.
- overhead-contact-validation.json 실제 F3회 모두 통과: 각 접촉 약0.683초, 세 번째 타격 벽 파괴, 내구도4→3. 최초 단발 검사는 여러 타격이 필요한 벽을 즉시 파괴로 가정해 실패하여 벽의 RequiredHits를 따르도록 수정했다.
- InputSettings 종료 오류 원인 정정: InputManager.settings setter가 HideAndDontSave 원본을 즉시 DestroyImmediate한다. UnloadUnusedAssets 추정은 오진. 원본 flags를 보관→DontSave로 잠시 변경→설정 복제/사용→원본과 flags 복구로 수정, 실제 F3회 검증의 정리 완료.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 타격 묶음 검사: Unity recompile up_to_date, 현재 consoleErrors0/compilationFailed=false. check-work Safety/Structure/Knowledge/ThirdParty OK, 기존 Task-079 log.md 138줄 제한 초과로 전체 FAIL(자동 후속 컴파일/테스트 SKIPPED). 실제 검증144포즈+F3회 통과. 시험 씬 초기화 후 타격 벽 앞으로 배치. commit/push 없음.
- 관절 피드백: 이전 144포즈/접촉 PASS는 쥠·도구 궤적·바닥 검사였고 팔꿈치/손목 정상 자세를 보장하지 못했다. 사용자 후면 화면에서 비정상 꺾임 확인. 원본 IK 팔꿈치 pole + 독립 wrist_axis 강제 목표를 폐기한다.
- 재개 동기화: Unity 저장/종료 성공, fetch/ff-only f4712c04→0fafe124 반영, 기존 변경 보존, 동일 프로젝트 reopen. 최신 dev의 이동/카메라 변경 포함.
- 내장 imagegen으로 정면/후면/좌측/우측×준비/들어올리기/정점/접촉/복귀 20포즈 원화 선제작: Art/strike-four-view-concept-v2.png. 프롬프트 핵심: anatomical right-hand overhead club strike, forward elbow hinge, neutral wrist, consistent four orthographic directions. 이후 Blender build-anatomical-strike.py로 4부상 클립 재작성(1.2초/접촉.66초 유지). 이전 build-overhead-strike.py는 폐기된 시도이며 전체 재생성 마지막에 build-anatomical-strike.py를 실행한다.
- Unity PreviewScene 연속 렌더가 이전 프레임 skin 버퍼를 재사용해 몸이 정지하고 기물만 이동한 캡처 오류 발견. 포즈별 BakeMesh 스냅샷을 렌더하도록 검토 도구 수정. 실제 게임 검증은 별도 실시간 프레임으로 한다.
- Cut2/3의 낮은 뒤따름 자세에서 Grounding 손바닥 바닥 보정이 손목을 재굴곡시켜, 타격/뒤따름 높이를 조정했다. 최종 anatomical-strike-validation.json: 4캐릭터×4부상×25시점 400포즈 실패0, 팔꿈치18.33~71.61도·손목0.40~2.88도. 단일축 굴곡 방향, 쥠·바닥 검사 포함.
- 최종 anatomical-live-validation.json: 실제 Player 16조건/1,056프레임 실패0, 팔꿈치18.01~72.04도·손목0.40~2.88도. strike-live-four-view-rabbit-0/3-v2.png의 실제 재생 전후좌우 캡처 확인. 별도 4캐릭터×정상/최대부상 비교판 8장도 보관했다.
- 새 클립으로 실제 F 입력 검증 재실행: overhead-contact-validation.json 3회 모두 접촉 약0.683초, 말뚝 끝 준비→접촉 높이차 약1.04m, 최종 벽 파괴·내구도4→3. 가상 입력 장치/설정 정리 후 Play 종료. 관절 검사와 화면 검토를 함께 수행했으며 최종 모션 감상은 사용자 검토로 남긴다.
- 관절 수정 묶음 검사: Unity recompile up_to_date, consoleErrors/Warnings0·compilationFailed=false. check-work Safety/Structure/Knowledge/ThirdParty OK, 기존 Task-079 log.md 138줄(hard130) 때문에 전체 FAIL, 후속 자동 컴파일 SKIPPED. 위 별도 실제 재생/입력 검사 결과와 구분한다. commit/push 없음.
- 열쇠 규칙 정정(사용자): 열쇠는 구매 불가·탐색 획득·슬롯 소지/선택/사용 가능한 도구. 이전 공유 획득 상태만 남기는 구현을 대체했다. Unity 저장·종료 확인→fetch/ff-only(최신)→동일 프로젝트 reopen 완료. doctor는 같은 Task에서 재실행하지 않음.
- ItemCatalog에 6종 열쇠 도구(price0/내구도 없음) 추가. 첫 열쇠 route-key, 나머지는 기존 경로 TargetId로 구분하고 기존 열쇠 모델·슬롯 아이콘을 공유. ExplorationState의 호스트 판정에서 슬롯 수용 성공 후에만 획득 이력을 기록한다. 가득 차면 원래 열쇠 유지. 문은 실제 선택한 열쇠를 확인하며 협력 문은 서로 다른 열쇠를 요구한다. 디버그 열쇠도 실제 슬롯 지급. 기존 공용 파일 연결 수정, 씬/YAML 변경 없음.
- 실제 시험 씬의 Give와 갤러리 Add에서 열쇠 전용 제스처 우회를 제거. 일반 도구처럼 지급·선택·들기·넣기·드랍/재획득된다. key-inventory-validation.json 17항목 실패0: 실제 F/숫자키, 가득 찬 슬롯, 획득/아이콘/들기, 넣은 상태 거절, 드랍 후 이력만으로 사용 거절, 다른 열쇠 거절, 동일 UID 재획득, 문 열기/사용 애니메이션/사용 후 소지, 시험 버튼과 카탈로그 확인.
- 구매 장부에 6종 열쇠를 직접 요청해 전부 UnknownItem/배송0/결제호출없음 확인. Unity recompile_status completed/failed=false. 기존 공유 열쇠 이력만 가정한 광역 경로 테스트와 온라인 협력 문 동시 입력은 이번 묶음에서 실행하지 않음. 커밋/push 없음.
- 열쇠 묶음 check-work: Safety/Structure/Knowledge/ThirdParty OK, 기존 Task-079 log.md 138줄(hard130)로 전체 FAIL·자동 후속 컴파일 SKIPPED. 별도 Unity 컴파일 정상·consoleErrors/Warnings0, 실제 입력17항목 통과. 시험 씬 재시작 후 열쇠 앞/한 칸 빈 슬롯으로 준비.
- 상호작용 이동 피드백: Unity 종료 확인 후 dev fetch/ff-only(최신), 재개. 상호작용 요청부터 동작 시간 동안 이동·점프를 차단하고 종료 뒤 입력을 다시 적용. 일반 들기/넣기·이동 중 도구 소지는 차단하지 않는다. 실제 Player에서 W를 유지한 검사 interaction-move-validation.json: 동작 중 이동 0m, 종료 뒤 1.85m, 잠금 해제 확인. Unity recompile up_to_date. 온라인 동기화 실제 다중 클라이언트 검증은 미실행.
- 이번 check-work는 기존 Task-079 log.md 138줄(hard130)로 문서/규칙 FAIL, 후속 자동 컴파일 생략. 별도 Unity 컴파일과 위 실제 재생 검사를 확인했다.
- 저장·PR 준비: 사용자 요청으로 Unity 종료 후 origin/dev fetch·ff-only 확인(최신, 충돌 없음). 기존 Task-079 로그의 완료된 8단계 원문을 History/006으로 보관하고 링크해 검사 차단을 해소했다.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-01 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/256 (feat/item-art-interaction → dev)
