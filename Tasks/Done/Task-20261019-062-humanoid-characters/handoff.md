# Handoff
## 지금 상태

- 2026-10-22 PM 요청으로 done(dev 반영 완료 기록 마감). #188 Squash `111edb2e` → #189 dev 병합 `f9e0e0a4` 완료. 사람 공동 플레이/다중 클라이언트 완료는 아니다.
- 전용 worktree 제거 완료. 로컬 프리팹·미추적 파일·private 서브모듈 변경은 `D:/github/Worktree-backups/curtaincall-20261022-174457`에 백업·해시 검증 후 보존했다.
- 이번 검사: 구역 SKIPPED(PM), 문서 PASS(verify-fast, WARN18/HARD0), 컴파일 ENV_BLOCKED(연결된 에디터 없음), 테스트 SKIPPED(문서 마감). 아래 과거 검증과 구분한다.
- Player.prefab → private Task062/PlayerPresentation/Humanoid.prefab. HumanoidPresentation이 기존 Slot/Condition/Mover/공연 상태를 읽는다. root motion=false/squash=false, 기존 이동·입력·충돌·부상 판정 보존.
- 정상 Idle/Walk/Run/Crouch/CrouchWalk/JumpStart·Loop·Land/Fall, 외줄 Tightrope, 대포 Flying·Crouch, 소품·트램폴린 Carry 상체 레이어 연결. pickup/reach hand는 미리보기만. 기존 CatchPose(600) 팔/BalancePose(500) 부모 슬롯 소유권 유지.
- 한발은 정상 모션, 양다리/한팔은 army_crawl 임시 재사용(정지/공중 지지 포즈). hop/한팔 끌기/팔점프 완성 아님. CrawlHeightOffset=-.05, 정상 키 맞춤과 분리.
- mask0/4/12/13/15: 신규 손실 모두 현재 pose bake → 본체/캡 전환 → 단면 중심 Impulse. 독립 Mesh/Rigidbody, 부모·본·Joint 없음. 중력/충돌/수명/복구 정리, legacy 절단기 상호 배제.
- 초기 손실/교체/재활성은 현재 메시만 복원. IsApplyingInitialCondition 구간만 과거 비산 생략. detach는 Animator 정지·남은 스킨 bounds로 root BoxCollider 갱신, 기존 단일 몸체 래그돌 사용(전신 관절 래그돌 아님). 마지막 cut 시 월드 위치 보존, Restore는 새 외형/부상/색상 복원.
- Assets/Resources/Settings/GameSettings/BaseGameSettings.asset → Character → 플레이어 자리 색상 1~4(빨강/파랑/노랑/초록), 직렬화 저장 완료. 기존 서버 Slot0~3/빈 슬롯 재사용. 표면 MPB/cap·공유 재질 보존, 살아 있는 외형 live 변경·조각은 절단 당시 색상. 게임 내 편집UI/설정 네트워크 전송 없음.
- 원격 이동/달리기 owner-write 표시 bool, 기존 crouch/jump/lostParts 재사용. 같은 빌드/설정 전제. 실제 4슬롯/원격 수신은 아직 미검증.
## Verification

- 구역 검사: PASS (10-20, origin/integration/humanoid-characters 기준). 문서/규칙 PASS(warn7/hard0), diff --check PASS.
- 2 컴파일: PASS (10-20, 최신 통합 후, 전용 Editor/dirty scene0).
- 3 테스트: 최신 통합 후 전체 EditMode799/799·휴머노이드 PlayMode11/11 PASS. 첫 EditMode796/799(외줄 fixture Awake 누락3건) → NetworkPlayer.Awake 초기화1줄 보완 → fixture16/16·전체 재검사 통과.
- PlayMode 통과: 실제 프리팹 누적4사지/비산/복구/legacy0, 초기·batch·교체·재활성, 4색/캡/공유재질/조각 스냅샷/live 설정, 초기 네트워크 flag 후 같은 프레임 cut, Mover 통합, 래그돌 충돌/바닥/마지막cut 위치, MPB fade/restore.
- 입력 통과: 합성 Keyboard 이벤트 → 실제 Reader → Controller → Mover 걷기/달리기/앉기/점프·애니메이션. 물리 키보드 사람 플레이와 구분. InputSettings는 기존 두 값만 임시 변경/finally 복원, MPB 지연 생성 및 float허용오차1e-5 적용.
- 최신 통합 후 실제 LAN 호스트 PlayMode 각1/1 PASS: 외줄 Intro/Performance 진입·이탈, 정상/한발/양다리/한팔 대포4회 비행·착지, 트램폴린 운반/해제/재시작 카메라. 기존 상태/내부 입력 주입 회귀이며 공연 애니메이션 접점/자연스러움 직접 판정은 아님. 별도 1인칭 렌더 숨김 회귀1/1 PASS.
- 포복 평지 mask12/13 각121표본: root/바닥 기준 최저점 .0013~.0522m(보정 전 .0513~.1022m). 10도 경사 Mover 이동·점프·착지 PASS, 점프 포함 스킨-평면 거리 .0879~.4103m. 유한 범위/실제 착지 검사이며 자연스러운 접지·미끄러짐 완성 PASS 아님.
- 증거: 세션 scratchpad humanoid-integrated-latest.xml(휴머노이드11/11, 대상 assembly 확인), humanoid-integrated-editmode.json 및 tasks/betp8sm5x.output(전체799/799), humanoid-full-edit-failed.xml(첫 실패) 보존. 통합 전 포복/호스트 증거도 별도 유지.
## 남은 확인 / 보존

- SKIPPED: 다중 클라이언트/실제4슬롯·late join·퇴장 재접속·씬 전환, PropCarry 소품 운반 실연결, 사람 플레이, 전체 프로젝트 PlayMode, 순차50회. 초기 네트워크 flag 합성 검사를 실제 late join 완료로 취급하지 않는다.
- 사람 공동 테스트는 기능 완성 후 한 번에 ①4인 색상 ②부상·비산·복구/원격 ③이동·공연·카메라. 기존 사람 확인 전 done 보류 조건은 2026-10-22 PM의 dev 완료 기록 마감 요청으로 대체하되, 공동 검증 미실시는 그대로 유지. 부상 전용 클립/손 접점/자연스러운 접지 후속 필요.
- 사용자8개 private Mixamo는 Humanoid/49본 기준 TRS 보정, SkeletonBone name/parentName 보존. GUID/localID/길이/60fps/원본 바이너리 유지. 개별 출처·배포 권리는 미확인. Quaternius Standard CC0/실제43클립. 외부 공개/구매/업로드 없음.
- dev/다른 worktree·LeartesStudios dirty·TightropeCourse.prefab 기존 위치4곳·.playwright-mcp 보존. 기존 검증 씬/검증 MISSING 정지 정책 유지. HumanoidPlayerBuilder.Build 재실행 금지(기존 출력 guard). CLI beta.6 named --caller/--skill 호환 예외 유지.
- 저장: private Task062 c10ca97e/main6030fe10·공개 b230924 push 완료. private main 반영은 사용자 직접 실행. 최신 dev dcfbd22 기반 원격 integration 준비 및 작업 브랜치 merge5b3a85c 완료. 초기화 보완·최종 검증72ec843 및 CC-152 연결e100d68 push 완료. 지식: Harness/Project/Facts/humanoid-validation.md, Harness/Engine/Unity/Pitfalls/Initialization/editmode-awake-fixture.md.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/188 — CC-152, 본문 승인 후 등록·병합 완료, dev 반영은 #189.
