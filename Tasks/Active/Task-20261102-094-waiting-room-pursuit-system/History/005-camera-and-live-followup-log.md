# Log

## 2026-11-03 Play 배치 창 경량화·플레이어 추적
- 기존 PreparePreview가 Play의 Repaint마다 전체 씬 Camera.Render를 호출했고 RenderTexture/팔레트 AssetPreview/SerializedObject UI도 유지했다. Play 전용2D Live/LiveMap으로 분리, Play 진입 중에도 PreparePreview 즉시 반환. 카메라·RT·복제 모델 해제, 숨긴 탭 Tick 중단, 보이는 창10Hz·방/층/플레이어 검색1초 캐시.
- 고정 벽/기둥 BoxCollider 윤곽은 씬마다1회 읽어2D 사각형으로 표시한다. 동적 문·가구는 추가하지 않음. 플레이어는 초록 마커, 선택 Actor에만 시야·경로·추격 연결선 표시. 오른쪽 행동/대상/렌더 관찰 목록과 전체 이름 옵션 제공.
- 기본 내 플레이어 추적, 목록으로 대상 선택. 위치/층/중심 방 자동 추적, 드래그·방/층 선택·F는 자유 모드로 전환하며 중심 유지. 대상 소멸 시 자동 로컬 복귀, 대기실 없음은 대기 표시. 편집 모드3D 기능 보존.
- Play 회귀1/1 PASS(12.42초): 실제 호스트·로비/기계실 층 이동·미리보기 Camera/RT/복제0·고정 벽 캐시·중클릭 팬 후 자유 모드 유지·관찰 대상 교체/소멸·숨김 Tick 중단. 다른 플레이어 선택은 Transform fixture이며 실제 다인 수신 실기는 미검증. warm TickLive100회 평균 .152ms/회(리플렉션 포함), 전체게임FPS/OnGUI draw GPU 비교 수치는 아님. designer-live-play-tests.json.
- 편집 층 회귀3/3 PASS(.23초, designer-live-floor-tests.json): 이동/삭제 후 층 안정, 층/마커 필터, 방 전환/카메라 위아래 절단. 새 Runtime 게임 코드·씬 데이터 수정 없음.
- 재개 당시 Play 정지/씬 clean 확인 후 정상 종료·fetch 성공. 기존 카메라 수정 포함6파일로 ff-only 중단·변경 보존, PID5764 재개방. 동일Task doctor는 반복하지 않음. 길어진 완료 계획/todo/log는 History002~004에 원문 비교 후 보존, 현재 결정·다인 미검증 유지.

- 실제 창 검토: 프레임2498→7673 진행, 고정 윤곽489개 캐시. 추적 위치 일치·Actor 선택 후 행동/시야/3점 경로 확인. 빈 지도 드래그 후 follow=false·중심과 플레이어 분리, 전용 Camera/RT 각각0(designer-live-ui-review.json). 실제 FPS/전체 메모리 전후 비교는 미측정. Play 종료·창 최대화 해제·추적 기본 복원, Pursuit 씬 재개방 clean/24체 보존.

## 2026-11-03 카메라 긴급 수정·추격 조정
- 시작 Play 정지/추격 씬 clean, 정상 종료 후 origin/dev fetch 성공·기존5파일 때문에 ff-only 중단(변경 보존). 같은 워크트리 PID23168 재개방. 동일 Task doctor 재실행 없음.
- 원인1: UsesWallCollision이 WaitingRoomBase만 인정해 복제 씬 스프링 암/고정 구조물 fade 제외가 꺼짐. Pursuit 씬 추가. 수정 전 실제 씬+호스트 회귀에서 벽 앞 카메라 거리5.172m로 실패(pursuit-camera-before-tests.json). 수정 후 원본/추격 씬 벽·천장·장애물 제거 후 점진 복귀2/2 PASS(16.93초).
- 원인2: 실제 숙소·기계실·복도 천장의 렌더 메시만 있고 Collider 없음. 추격 씬 평면 천장/지붕40곳에 Mesh.bounds 기반 CameraCollisionSurface OBB 추가, 스프링 암 반경/패딩 확장 후 거리 제한. 물리/NavMesh Collider 추가0, 원본 씬 수정 없음. 실제 숙소 천장+벽/천장 회귀2/2 PASS(17.13초, pursuit-ceiling-play-tests.json). 사용자24체 위치/회전 before JSON과 동일, 씬 clean.
- 단계 설정에 현재 온전한 플레이어 걷기3/달리기6m/s 표시, 공통 시야 상실 유예 노출. 단계별 PatrolLeashDistance(0=기존 무제한) 추가: 닫힌 순찰 경로의 선분까지3D 최단 거리, 경로 없으면 최초 배치점. 추격자/타겟 이탈 시 Return, 복귀 중 소리/재탐지 금지, 도착 후 재개. AlwaysNearest도 제한 준수. 사용자 단계값은 변경하지 않음.
- Behavior PlayMode3/3 PASS(43.79초): 기존 청각/수색/가림/포획 유혈 및 신규 경계 이탈·최인접 예외·복귀·재추격·0해제(patrol-leash-play-tests.json). 경로 중간/층 높이/빈 경로 fallback/단계 독립 Edit1/1 PASS(.02초). 실제 설정 창에서 두 속도와 이탈 설정 가독성 확인. 다인 원격 실기 및 전체 맵 수동 카메라 주행은 미검증.
- 카메라 전용 OBB는 직육면체 구조물에만 적용하며 구멍/곡면 메시 전체를 박스로 대체하지 않는다. Characters/기존 부상 수정은 보존. commit/push 없음.
- OBB 회전0°/37°·비균일 크기·반경·시작점 겹침·빗나감 Edit2/2 PASS(.4초, camera-surface-edit-tests.json). 이번 묶음 고유 회귀9개 통과, verify-knowledge OK.

## 2026-11-03 부상 후 공연에서 부유
- 접수 당시 Play 정지/추격 씬 clean. 정상 종료 후 origin/dev fetch 성공, 기존5파일 변경 보호로 ff-only 중단. 동일 워크트리 PID46776 재개방. doctor는 같은 Task에서 이미 실행하여 반복하지 않음.
- 회귀로 재현: 포획 후 Tightrope에서 rootY=.08/hipsY=.96, ItemCrawl_Cut3 요청이나 transition=true·Animator.speed=.0001. 기존 메시 검사는 통과했지만 새 정지 자세 검사가 실패(수정 전1/2, capture-grounding-before-tests.json).
- 원인: 정지용 애니메이터 속도가 CrossFade 진행도 늦춰 서 있던 자세가 남음. HumanoidModel.Play에서 .001 이하 속도의 새 모션은 즉시 Play+Update(0)로 샘플링. 포획 해제는 Animator 복원 후 현재 부상 Apply(spawn=false)로 높이 복원. 토끼 offset0이라 직접 원인은 전환이며 -.05 모델 높이도 추가 회귀로 보호.
- PlayMode 최종2/2 PASS(37.81초, capture-grounding-after-tests.json). 각 시나리오2회 반복: 높이·메시/가림 유지, 호스트 포획→공연에서 부상13/Normal·Pose 제거·기어가기 전환 종료·접지·허리 높이 검사. 수정 후 동일rootY=.08/hipsY=.28/transition=false. 다인 원격 수신 실기는 이번 범위 미검증.
- 기존 에셋/씬 배치 수정·commit/push 없음. Characters 기존 포획 함정 문서에 정지 전환/높이 복원 원인 추가, verify-knowledge OK.

## 2026-11-03 드래그 층 이탈·추격 전 아웃라인
- 사용자 상태 재현: 숙소 마네킹 Y1.915를 층으로 사용해 숙소가2m 층으로 변하며0m 필터에서 방/프리뷰/마커가 사라짐. F8_Cargo도Y1.073으로1m 층이 됨. 고정 방 순찰 중앙값→스폰→볼륨 하단으로 층/지면 기준 통일, 가변 배치 높이/가구 ray 결과로 층을 추정하지 않도록 수정.
- 단일 스폰도 연습실의 .666m 가구 위일 수 있어 여러 공용 순찰 지점 중앙값을 우선한다. 마네킹을 움직이거나 전부 지워도 방의 층은 유지. 사용자22체를 시작 스냅샷(drag-floor-before.json)으로 기록했으며 작업 중 사용자가 추가/이동한 현재24체도 되돌리지 않고 보존한다.
- 시작 시 scene clean/Play 정지 확인 후 에디터 정상 종료, origin/dev fetch 성공·기존5파일 변경으로 ff-only 중단. 동일 워크트리 재개방 중 Pipeline 로딩 대기를 확인한 뒤 ready PID31916에서 작업. 강제 종료/commit/push 없음.
- 배치 회귀3/3 PASS(.23초): 높은 배치 이동/삭제/프리뷰 재준비 후 층·표시 유지, 층 전환/선택/아래층 렌더 제외. 실제 숙소 UI에서0m/방 선택/마커/Inspector 복구, previewExists=true 확인.
- 후속 사용자 지시: 추격 전에는 장식으로 보여야 함. PursuitFeedback의 공통 표시 조건에 Elapsed>=0 추가하여 근접 아웃라인도 추격 활성 중에만 표시, 철회 시 제거. 실제 LAN호스트 PlayMode1/1 PASS(14.08초): 가까이 있어도 시작 전0→시작 후1→철회 후0, 로컬/다른 플레이어 테두리·거리 이탈·효과 정리( outline-pursuit-only-tests.json ).
- 공용 순찰 중앙값 우선 변경 후 Authoring4/4 PASS(.41초): 단일 높은 스폰에도 층 유지·로비 지면·아래층 거부·기존 배치/순찰 Undo. 최종 check-work1회 PASS(144파일/문서/컴파일 up_to_date, 경고45/hard0). 별도 회귀 합계8개 통과. 최종 scene clean/Play 정지/사용자24체·층0,-5,-10 확인, 검토용 창 최대화 해제.

- [초기 구현·모션/UI 검증 원문](History/001-initial-implementation-and-motion.md)
- 남은 검증: 다인 수신·동시 포획·늦은 접속 및 최종 모션/공포감 실기. 기존 사용자 변경 보존, commit/push 없음.

- [완료한 포획·배치/층·치트 검증 원문](History/004-capture-authoring-followup-log.md)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
