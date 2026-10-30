# Log

이전 전체 기록: [보관본](History/005-before-stair-access-log.md).

## 전체8단계 — 흐름 검증
- 기준46708851 이후 검사. 수평 거리만으로 층/벽 너머 F가 가능한 문제를 ExplorationReach 공통 검사로 수정. 바닥 기물은 표시/호스트가 같은 실제 위치·범위를 검사하도록 연결.
- 임시 Collider NavMesh: 야간 철창 저장 상태와 트리거를 낮 이동 조건에서 분리. 닫힌 퍼즐39/48·개방 기하48/48, 실제 씬 dirty변화 없음. 지하 막힘을 맵 결함으로 임의 수정하지 않음.
- 새4사례 PASS: 5종 거리/가림, 실제 F+호스트 획득 거절/성공, Morning2 실제 입력 도보로 작업실→화장실6타 통과, 실제 씬 재진입/재지급 방지. 이전과 달리 주요 구간은 순간이동 대신 이동 입력으로 검증.
- 사람 직접 플레이/전체 맵 도보/실제2인과 구분. 19사례 회귀를 새 코드로 실행 중. 초기 테스트 JSON 라이브러리 참조 오류는 기본 JsonUtility 형식으로 수정, 컴파일 재확인.

- 회귀에서 실제 배치 문제 발견: 변기 안 열쇠1, 옷장/의자와 겹친 캐비넷1/2·지도, 작업실 머리핀2를 같은 구역의 접근 가능 위치로 조정.4개 문의 막음도 방 회전90도를 상속하던 것을 월드 축0으로 맞춰 옆 우회 방지.
- 배치 테스트는 Night 강제 좌표 대신 Morning(RestTime)의 충돌 없는 접근 위치를 사용. 수정 후 실제 배치4/4 통과, 잔여 회귀 진행.

- 최종 현재 변경25/25 PASS(기존19+신규6). 개방 fixture48곳 실제 입력 도보·네 문 폭 막음/개방 확인. 실제 열쇠/암호/협력 전체 완주·사람 직접 플레이와 구분.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 최종 낮 경로망: 문 폭 수정 뒤 닫힌32/48·열린48/48(이전39/48은 수정 전). 최종 흐름 첫 시작 정체는 취소 기록 후 재실행5/5 PASS. check-work 안전성/규칙/컴파일 PASS, 씬dirty0.
- Confluence 작업노트v4·CC-181 최신 결과 반영. 7단계46708851 푸시됨, 이번8단계 미커밋.
## 9단계 실제2인 접속
- MPPM 별도PID2개·LAN Host/Client 양쪽count2·같은씬 Waiting 확인. 이후 도구 메인스레드5초 시간초과 반복, UI 그래픽링버퍼 경고. 협력/재수신/이탈 미검증 유지. 상세 stage-09-two-player.md. 기존 브랜치 이름을 start-work가 거절해 승인된 공유 브랜치 보존.

## 소품실·디버그·배치도 추가 요청
- 소품실 출입구 그림 바닥0.5m 빈 구간만 보완, 기존 충돌체 유지. 로비에 디버그3종각4개(UID61~72)·Editor/Development 전용 추가. 기존 내구도/정식 발견품 유지.
- 실제 씬 좌표로 지상/지하 탑다운PNG·번호/좌표표 추출. 신규2+기존배치4=6/6 PASS. 초기 획득검사 실패는 시험이 내려놓은 도구를 다시 줍는 배치 때문이라 시험만 수정. 상세 lobby-debug-and-doorway.md.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 배치 보정 — 숙소 막힘·부유·낙서 벽·패드
- 원인: 7단계 Tool() 경첩이 월드 오프셋이라 회전된 방에서 캐비넷 문짝이 옆으로 비틀려 숙소 통로를 막음. 원래 숙소 가구는 이동 없음. 경첩 로컬 앞면으로 복구(캐비넷5), 숙소 캐비넷1·2는 남쪽 빈 벽으로.
- 낙서·UV·벽 단서 11개 실제 벽면 3mm 밀착(작업실3 문틀 매몰, 파이프4·화물3·암호 앞자리는 같은 방 실제 벽으로 이동). 모니터는 기존 CRT 화면, 공연 기록은 책상 위.
- 열쇠6·신문·노트·문2를 실제 면으로 내림. 계단/탈출문 판은 기존 문짝 안쪽으로 숨김. 비밀번호 패드는 기존 키패드 받침판으로.
- StructureAPlacementFix(재실행 동일)로 적용. 사용자 미저장 2층침대 x+0.5 이동 3개 함께 저장. 상세 placement-fix.md.
- 내구도: 사용 1회 차감·0이면 소멸 코드 정상. 발견품 말뚝뽑개/UV1회 카탈로그 값과 디버그 보급품이 발견품 수치를 쓰는 것이 원인. 수치 변경은 사용자 확인 대기.
- 검사: 배치 PlayMode4/4·거리/가림1/1 PASS. check-work 안전성/규칙/컴파일 PASS, 씬dirty0. 검사 중 GripYellow·LA_DeadScreenGlass 재질 소수점 직렬화 변화 발생(되돌리기는 권한 거부로 남김).

## 디버그 내구도·선반 칸 이름표·금 간 벽 외곽선·명칭
- 사용자 결정: 로비 디버그 보급품만 새 제품 내구도(말뚝뽑개4·UV3·머리핀5). 정식 발견품 수치 유지.
- 선반 칸 이름표(1~4칸)가 소품실 옛 선반 자리(z55)에 남음. 선반이 z102로 90도 돌아 옮겨진 뒤 앞면을 +z로 고정 계산하던 탓. 매 프레임 실제 판자·선반 앞면(VisualRoot.forward)에 맞춤. 선반 기물 이름표도 같은 계산 사용.
- 금 간 벽 외곽선이 조각40개·철제 띠마다 그려져 격자로 보임. 벽면과 같은 면은 뒤집힌 껍질 외곽선이 깊이에 가려 점선이 된다. 깊이만 쓰는 판(OutlineProxyDepth)을 타일 높이 앞 6mm에 두고 외곽선 기준으로 지정, 무너지면 숨김. 계단·탈출문도 문 앞 기준판으로 교체(문짝 안 숨긴 판은 외곽선까지 가려짐).
- 금 간 벽 안내 "빠루" → 기물 목록 이름 "천막 말뚝 뽑개", 조사 이/가 자동.
- 컴파일 PASS, 기준판 화면에 보이지 않음 확인. 실제 플레이 외곽선 모양·디버그 내구도 PlayMode는 이번 묶음에서 미실행.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 검사(e3b091d3 이후): 리뷰2/2·배치4/4 PASS. 화장실 벽6타 검사가 바닥의 마지막 말뚝뽑개를 골라 로비 디버그(새 제품4회)를 집고 실패 → 작업실(z>100) 정식 발견품만 고르게 시험 수정, 재실행1/1 PASS. 게임 수치 변경 없음.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 사용자 지시: 현재 결과는 기능 구현·임의 배치이며 확정 아님. 오브젝트 설명/내용·외형·위치는 상세 기획 후 재반영. 기획 필요 항목을 Confluence 작업노트 하위 페이지로 정리: https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/31293441

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 탈출로 숨겨진 문·열쇠 구멍·계단 보 (CC-182·183)
- 키패드 숨김 수정, 열림 시 문짝·자물쇠만 숨김, 계단 위층 비행을 가로지른 지붕 보 절단. 기획 반영: 벽 판이 정답 뒤 옆으로 밀리고 문 양옆 열쇠 구멍 2개에서 두 명 동시 F. 상세 escape-door-keyslots.md.
- M2_ PlayMode 27/30. 실패3 확인: ContainerRoute·ToolsChain은 HEAD에서도 같은 실패(기존 문제). InputWalk는 HEAD 2/2·코드만 1/1·전체 변경 재실행 2/2 PASS, 앞선 2회 실패는 4초 정체 기준 경계(4.01초)의 불안정으로 판단. 실제2인 열쇠 동시 돌리기 미검증. 미커밋.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 탈출로 2차 변경 — 배관함 키패드·벽 같은 숨은 문·투입구
- 배관함(말뚝 뽑개 3타, BreakableWall 재사용) 안으로 키패드 이동, 무너진 뒤 패드 F. 오답 경고음+입력 비움·재시도. 숨은 문은 옆 벽 UV를 이은 1cm 판. 투입구 크게(램프 포함). 상세 escape-door-keyslots.md.
- M2_ 28/30(실패2 기존), Reach 1/1, EditMode Exploration 24/24. 실제2인·경고음 청취 미검증. 미커밋.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 탈출로 3차 변경 — 숨은 문은 기계실 문
- 사용자 지적: 숨은 문은 기계실 문이며 탈출로 문 자리 배치는 열 문 너머에 키패드가 있는 오류. 벽 판·배관함(뚜껑)·키패드를 기계실 문 앞으로 옮기고, 기계실 문은 키패드 정답으로 함께 열림(openedBy, 열쇠2 제외). 테두리 기둥·인방 숨김, 강철 띠 이음, 문턱 돌출 정리. 탈출로 문은 처음부터 표시.
- 최종 M2_ 전체 23/30(기존2+키보드 입력5), 입력5는 단독 재실행 PASS. Reach1/1·EditMode WaitingRoom 88/88. 실제2인 미검증. 미커밋. 상세 escape-door-keyslots.md.
- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## dev 병합 (b19189eb까지 6커밋, 미커밋)
- 충돌 17개 중 실제 양쪽 변경은 CommonInteractionUI.cs 1개(dev: 탈출로 협동 버튼·봉 우선순위, 우리: 외곽선 2개 제한 해제) → 둘 다 유지. 나머지 16개는 dev가 #235로 우리 작업을 스쿼시 반영하며 생긴 가짜 add/add → 실제 분기점 c4e6ea1d 기준 3-way로 다시 합쳐 우리 버전 확정. 전체 1,738파일 대조 불일치 0.
- _ThirdParty 서브모듈 98904299→26d5009e(삐에로·곰·소녀 캐릭터 등). 컴파일 PASS, 씬 missing script 0, 숨은 문·배관함 유지.
- 사용자 지시로 충돌 확인까지만. 실행한 것: EditMode 1226/1228(실패2 StartConnection은 단독 5/5), DevelopmentSession 1/1, HumanoidPlayer 20/20. LobbyHud·M2 PlayMode는 미실행.

## 확정 기능 구현안 적용 (2026-10-30, 미커밋)
- 타이머 구역 재생/멈춤(호스트·RunState Stopped↔Running), UV 장비형 우클릭 손전등·형광 낙서·최초 발견 연출, 로비 UV 4개 교체, 배관함 철창, 거울 6장(PlanarMirror), 연습실 서커스 조명·벽면 모니터, 배치물 외형 31개. 상세 confirmed-features.md. 기획서 Confluence(작업노트 하위).
- EditMode ZoneRule 3/3, PlayMode M2_Confirmed 타이머·배치 PASS, UV 손전등 테스트 재실행. 사용자 지시로 기존 회귀 생략. 영향 테스트 3파일 새 규칙으로 수정.
- UV 손전등 PlayMode 검사: 수정 반복(마우스 시선/고정 카메라 분기, 플레이어 너머 조준점, 원형 표시 1m 판정, 벽면 가림 여유 25cm) 후에도 자동 검사는 미통과. 사용자 지시로 검증 중단·직접 확인 예정. 진단: 시선을 벽으로 돌리면 조준점이 낙서 0.88m 안에 닿음(onRing 조건), 남은 의심은 벽 타일 가림 판정.
- 기존 테스트 회귀 확인 생략(사용자 지시). 간헐 오류: 시작 시 "[Curtain] 커튼 닫힘 확인 실패"가 테스트를 실패 처리한 적 1회.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 사용자 피드백 반영: 숨겨진 낙서는 비춘 뒤 5초 유지 후 사라짐(다시 비추면 다시 드러남, UvMark.holdSeconds), UV는 캐릭터 정면 ±75°로만(UvFlashlight.frontLimit), 보라색 네모(원형 표시 쿼드) 제거. 키맵: 입력 에셋에는 UvLight(<Mouse>/rightButton) 있으나 설정 화면 목록은 키보드 키만 보여 미표시. 검증은 사용자.
- 키맵: InputBindingPreferences가 마우스 버튼(왼쪽 제외)을 허용하고 설정 화면이 우/중/앞/뒤 버튼 입력을 받아 UV 등 조작키를 재설정할 수 있게 함. 왼쪽 버튼은 UI 클릭용으로 예약.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 사용자 후속 4건(상점 기물·의료 서비스·인간대포/트램펄린 발견 해금·UV 동기화)을 코드와 교차 확인해 followup-plan-20261030.md에 8단계로 정리. Confluence는 커넥터 미연결·브라우저 로그인 대기로 원문 대조 전.

## M2 후속 4건 구현 (2026-10-30, 미커밋)
- 원문 대조 후 사용자 추천안 결정: 의료=승강기+안내, 공연용 4종은 효과 연결 후 판매, 미발견 인간대포는 1일차 포함 외줄타기로 대체, 다른 사람 UV 빛도 낙서 드러냄(발견 요청은 비춘 본인).
- origin/dev 6커밋 병합(--no-commit, 충돌 없음).
- UV: NetworkPlayer Owner 쓰기 uvOn/Origin/Forward(10cm·2도 임계) → UvFlashlight가 원격 빛 그림·낙서 Illuminate.
- 의료: 상점 서비스 한 항목 [N개 예약]·[수량 N/최대], 예약자 화면 위 승강기 거리 안내, "전문가 대기실" 문구 → "신체 복구 승강기".
- 해금: CampaignSnapshot.CannonUnlocked(런 범위, 전송 끝에 추가), RunState.UnlockTrick, 미발견 공연은 TrickFor에서 외줄타기 대체, CampaignRun.DiscoverTrick(손님→Unlock 메시지). 개발 시작 지점이 인간대포면 해금 상태로 시작.
- 스케줄표: ScheduleRefusal.Locked, 카드 순환에서 미발견 공연 건너뜀.
- 발견: PerformancePropDiscovery(StructureAExploration/HumanCannonDiscovery). S11 무대 장치 보관실 HumanCannon+CarryTrampoline×2를 보면(14m·화면 안·가림 없음) 해금, 해금 시 보관실 기물 숨김·연습실 PracticeLanes/HumanCannon·PracticeTrampoline 표시. 씬 저장.
- 검사: 컴파일 PASS. EditMode DifficultySelection 5/5(신규 1 포함), DevStartPoint 15/15. 같은 어셈블리 전체 105 중 실패 2(이름 미확인, 이전 StartConnection 기존 실패 추정). PlayMode W3_ 3/3, W2_BodyRestore 2/2. UV 원격 빛·실제 2인·발견 PlayMode는 미실행.
- 사용자 결정: 인간 대포 해금은 보기가 아니라 F 획득. PerformancePropDiscovery에서 시야 판정 제거, 세트 기물 3개(HumanCannon·CarryTrampoline×2)에 PerformancePropPickup(IShopTarget·IShopPresentation, 공통 외곽선·[F] 안내, 거리 3m) → DiscoverTrick. 씬 저장. 호스트는 손님 요청의 거리는 다시 확인하지 않음(POC 한계).
- 검사: 컴파일 PASS, PlayMode W3_HumanCannonSet_FAcquireUnlocksAndMovesToPractice 1/1(획득 전 보관실/연습실 숨김 → F → 해금·보관실 숨김·연습실 표시).
- 사용자 결정: 세트 나란히. S11 CarryTrampoline×2를 (-31.25,34.0) → (-55.4,38.5)로 이동(대포 북쪽 0.7m, 다른 렌더러·충돌체와 겹침 없음 확인). 씬 저장. 주의: WaitingRoomLayoutA 생성기를 다시 돌리면 원래 자리로 돌아갈 수 있다(생성기 미수정).
- 검사: W3_HumanCannonSet 재실행 1/1 PASS. 배치 PlayMode 묶음은 미실행.

## 밤 정비·연습실·안내 UI 수정 (2026-10-30, 미커밋)
- 화물 적재 철문(CargoGate): 통로 북쪽 벽 끝(x -18.5)보다 1m 앞(x -17.4)에 있어 틈으로 통과 → x -18.75·폭 3.8로 이동(씬·생성기 WaitingRoomLayoutA.Night 동시 수정). 0.3m 캡슐로 틈 없음 확인.
- 밤 종료: RunState.NightEndGate(호스트) = ScheduleBoard.NightCanEnd(오늘 밤 고칠 일차가 있으면 호스트 확정 필요). 전원 침대 + 확정 시 다음 날. 침대 안내에 "호스트의 스케줄표 확정 대기" 표시.
- 연습실: 통로(링 기준 -40°) 쪽에 남쪽 계단을 복제한 RingAccessStep_Entry·충돌체 추가, 난간 기둥1·상단 레일2·깃발2 숨김(약 4m 틈). 생성기 Ring40StairAngles에 입구 각도 추가(생성기 재실행 시 입구 계단 복제 필요).
- 안내 패널: WaitingRoomGuidePanel(BasicPanels Menu_C13 판, 입력 막지 않는 상단 알림). 인간 대포 획득(모든 화면, 8초)·신체 복구 예약(예약자, 상점 창 닫힌 뒤 승강기 쪽으로 카메라/캐릭터 회전 + 8초).
- 날짜 제목: DemoRunEnding.DayTitle → Day 1~3, 공연하는 날 수 다음 날 D-Day.
- 최적화: waiting-room-optimization.md(렌더러 14,334·그림자 11,276·실시간 조명 146·오클루전 없음 등 측정과 단계별 방안). PerformancePropPickup 안내 위치 캐시.
- 검사: 컴파일 PASS. PlayMode M2_NightPowerGatesAndBedWake 1/1(미확정이면 밤 유지 → 확정 후 Day 2), W3_ 4/4, ScheduleClose_DawnTitle 1/1(D-Day), W2_BodyRestore 2/2. 안내 패널 화면 모양·카메라 회전·연습실 계단 실제 걷기는 사람 확인 필요. 연습실 인간 대포 "연습" 기능은 아직 없음(아트만).
- 최적화 1단계(씬 전용): 작은 소품 그림자 끔(그림자 렌더러 11,276→8,634), 오클루전 정적 표시·굽기, Main Camera far 5000→150. URP 공용 설정은 미적용(확인 대기). PlayMode W3_ 4/4 PASS. 실제 프레임 개선 수치와 잘못 가려지는 곳은 사람 확인 필요. 테스트 실행 중 HumanCannon 재질 2개(pole-gate-black, section-sign) 직렬화 변화 발생(직접 수정 아님).
- 사용자 결정(2026-10-30): 최적화는 대기실 씬만. 공용 URP 에셋(PC_RPAsset) 설정은 변경하지 않는다.

## 숨겨진 낙서·숨은 문 띠 수정 (2026-10-30, 미커밋)
- 화장실 UV 낙서: X 표시가 낙서 자리에서 1.75m 떨어진 부서지는 벽 앞면 안쪽(z62.56 < 벽 앞 62.71)에 묻혀 있었음 → 화살표·X를 얼룩 판(0.6×0.45) 안으로(씬·생성기 M2.cs). 비출 때 그림 있는 재질(화살표·X)을 노란 사각형 재질로 덮어쓰던 것 → 그림을 지키는 노란 형광 사본(UvGlow_*).
- 손전등 판정 기준을 낙서 위 30cm(Point)에서 그림 자리(Center)로, 함께 드러나는 단서판 충돌체를 가림에서 제외(UvMark.Owns). 이게 M2_Confirmed_UvFlashlight 장기 실패 원인. 테스트를 발견 직후 입력 막힘·5초 유지 규칙에 맞춤 → 1/1 PASS.
- 기계실 숨은 문: 열려도 문 자리를 가로지르던 강철 띠(HiddenDoorLedger)를 hideOnOpen에 추가(씬·StructureADoorVisualFix).
- 검사: M2_ExitRoute 3/3, M2_Confirmed_UvFlashlight 1/1. M2_ClueRoute_NotesUvMonitorAndPendingHint는 UvMark.Interact(10-30 F 제거) 호출하는 옛 테스트라 실패(기존 문제, 이번 변경 무관).
- 2인 테스트: MPPM Player 2 비활성(Library/VP/SystemData.json). 활성화는 Window > Multiplayer Play Mode에서 사람 조작 필요(내부 API 직접 실행 금지 함정).

## 실제 2인 확인 (2026-10-30, MPPM Player 2)
- 방법: `MultiplayerPlaymode.PlayerTwo.Activate`(MPPM 창과 같은 공식 경로)로 복제 에디터 실행 → 메인 HostLan, 복제본 JoinLan(127.0.0.1) → 호스트 2명 확인 → DevStartLauncher 1일차 밤. 복제본 조작은 `unity command --project-path Library/VP/mppm1b0aebbd eval`(느림·재시도)과 `WaitingRoomMppmReview`(에디터 전용, EditorPrefs 키 켤 때만 복제본에서 동작, 명령 파일로 입력·상태 기록).
- PASS 밤 종료: 두 명 모두 침대 → 준비 2/2여도 Night 유지(복제본 침대 안내 "호스트의 스케줄표 확정 대기"). 호스트가 인간 대포 편성 시도 → Locked. 외줄타기로 확정 → 즉시 2일차 아침.
- PASS UV 동기화: 복제본(참가자)에게 UV 등 지급·화장실 낙서 앞에서 우클릭 유지 → 호스트 화면 원격 빛 1개, 호스트에서도 낙서 드러남·발견 기록. 참가자 화면도 드러남.
- PASS 인간 대포: 참가자가 무대 장치 보관실에서 F → 양쪽 해금, 보관실 기물 숨김·연습실 표시, 양쪽 "인간 대포 획득!" 안내 패널, 스케줄 해금.
- PASS 신체 복구: 참가자 예약 → Booked, 카메라가 승강기 방향(45°)으로 회전, 안내 패널 "신체 복구 예약 완료" 문구.
- 미실행: 탈출로 열쇠 두 개 동시 돌리기·경고음 청취, 화물 철문 실제 걷기 통과, 늦은 접속·이탈. 사람 플레이 확인은 별도.
- 정리: 플레이 종료, Player 2 Deactivate, EditorPrefs 키 삭제, 씬 dirty 0.
- 2026-10-31 origin/dev 3커밋(인간대포 포신 애니메이션 #249) 병합 e59d59af: 충돌·겹침 없음. 에디터 재시작 후 컴파일 PASS, PlayMode W3_ 4/4·M2_NightPowerGatesAndBedWake 1/1·M2_Confirmed_UvFlashlight 1/1·M2_ExitRoute 3/3·W2_BodyRestore 2/2. 병합 커밋 미푸시.
- 2026-10-31 최적화 2~4단계(점조명 그림자5 끔·작은 물체 거리 컬링 12,505·거울/UV/상호작용 표시 코드). 굽기는 정전 회로 때문에 제외. PlayMode 10/10 PASS. 미커밋.
