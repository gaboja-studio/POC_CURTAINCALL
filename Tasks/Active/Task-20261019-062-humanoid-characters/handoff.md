# Handoff

## 지금 상태

- 2026-10-20, Task working. 실제 Player 연결 구현, 사람 공동 플레이/다중 클라이언트 완료 아님. worktree D:/github/POC_CURTAINCALL-humanoid-characters, feat/humanoid-characters, 전용 Unity6000.3.25f1/PID44020/7801만 사용. 공개 저장소 커밋/push/PR 없음; private 저장 상태는 아래 참조.
- Player.prefab → private Task062/PlayerPresentation/Humanoid.prefab. HumanoidPresentation이 기존 Slot/Condition/Mover/공연 상태를 읽는다. root motion=false/squash=false, 기존 이동·입력·충돌·부상 판정 보존.
- 정상 Idle/Walk/Run/Crouch/CrouchWalk/JumpStart·Loop·Land/Fall, 외줄 Tightrope, 대포 Flying·Crouch, 소품·트램폴린 Carry 상체 레이어 연결. pickup/reach hand는 미리보기만. 기존 CatchPose(600) 팔/BalancePose(500) 부모 슬롯 소유권 유지.
- 한발은 정상 모션, 양다리/한팔은 army_crawl 임시 재사용(정지/공중 지지 포즈). hop/한팔 끌기/팔점프 완성 아님. CrawlHeightOffset=-.05, 정상 키 맞춤과 분리.
- mask0/4/12/13/15: 신규 손실 모두 현재 pose bake → 본체/캡 전환 → 단면 중심 Impulse. 독립 Mesh/Rigidbody, 부모·본·Joint 없음. 중력/충돌/수명/복구 정리, legacy 절단기 상호 배제.
- 초기 손실/교체/재활성은 현재 메시만 복원. IsApplyingInitialCondition 구간만 과거 비산 생략. detach는 Animator 정지·남은 스킨 bounds로 root BoxCollider 갱신, 기존 단일 몸체 래그돌 사용(전신 관절 래그돌 아님). 마지막 cut 시 월드 위치 보존, Restore는 새 외형/부상/색상 복원.
- Assets/Resources/Settings/GameSettings/BaseGameSettings.asset → Character → 플레이어 자리 색상 1~4(빨강/파랑/노랑/초록), 직렬화 저장 완료. 기존 서버 Slot0~3/빈 슬롯 재사용. 표면 MPB/cap·공유 재질 보존, 살아 있는 외형 live 변경·조각은 절단 당시 색상. 게임 내 편집UI/설정 네트워크 전송 없음.
- 원격 이동/달리기 owner-write 표시 bool, 기존 crouch/jump/lostParts 재사용. 같은 빌드/설정 전제. 실제 4슬롯/원격 수신은 아직 미검증.

## Verification

- 단계0~1 PASS: 최종 scope -Base origin/dev 81files/PM, verify-fast warn7/hard0(기존 문서 길이·private dirty222 경고), diff --check PASS. 기본 check-work는 없는 origin/integration/humanoid-characters 때문에 사용 불가; 승인 대안 유지. 최종 Editor play=false/compiling=false/dirtyScenes0, offset-.05/충돌체 저장 확인.
- 단계2 PASS: 최종 compile PASS. 단계3 EditMode13/13 PASS(기존11+자산2), production PlayMode11/11 PASS(경사 착지 포함). 전체 프로젝트 테스트는 미실시.
- PlayMode 통과: 실제 프리팹 누적4사지/비산/복구/legacy0, 초기·batch·교체·재활성, 4색/캡/공유재질/조각 스냅샷/live 설정, 초기 네트워크 flag 후 같은 프레임 cut, Mover 통합, 래그돌 충돌/바닥/마지막cut 위치, MPB fade/restore.
- 입력 통과: 합성 Keyboard 이벤트 → 실제 Reader → Controller → Mover 걷기/달리기/앉기/점프·애니메이션. 물리 키보드 사람 플레이와 구분. InputSettings는 기존 두 값만 임시 변경/finally 복원, MPB 지연 생성 및 float허용오차1e-5 적용.
- 실제 LAN 호스트 PlayMode 각1/1 PASS: 외줄 Intro/Performance 진입·이탈, 정상/한발/양다리/한팔 대포4회 비행·착지, 트램폴린 운반/해제/재시작 카메라. 기존 상태/내부 입력 주입 회귀이며 공연 애니메이션 접점/자연스러움 직접 판정은 아님. 별도 1인칭 렌더 숨김 회귀1/1 PASS.
- 포복 평지 mask12/13 각121표본: root/바닥 기준 최저점 .0013~.0522m(보정 전 .0513~.1022m). 10도 경사 Mover 이동·점프·착지 PASS, 점프 포함 스킨-평면 거리 .0879~.4103m. 유한 범위/실제 착지 검사이며 자연스러운 접지·미끄러짐 완성 PASS 아님.
- 증거: 세션 scratchpad humanoid-production-play-final.xml(11/11/포복 측정), humanoid-tightrope-regression.xml, humanoid-carry-camera-regression.xml은 대상 어셈블리/프로젝트 확인 후 보존. 각 실행 output은 세션 tasks에 남음. 이전 mixamo-* 증거는 production 증거로 대체하지 않는다.

## 남은 확인 / 보존

- SKIPPED: 다중 클라이언트/실제4슬롯·late join·퇴장 재접속·씬 전환, PropCarry 소품 운반 실연결, 사람 플레이, 전체 프로젝트 테스트, 순차50회. 초기 네트워크 flag 합성 검사를 실제 late join 완료로 취급하지 않는다.
- 사람 공동 테스트는 기능 완성 후 한 번에 ①4인 색상 ②부상·비산·복구/원격 ③이동·공연·카메라. Task done 처리하지 않음. 부상 전용 클립/손 접점/자연스러운 접지 후속 필요.
- 사용자8개 private Mixamo는 Humanoid/49본 기준 TRS 보정, SkeletonBone name/parentName 보존. GUID/localID/길이/60fps/원본 바이너리 유지. 개별 출처·배포 권리는 미확인. Quaternius Standard CC0/실제43클립. 외부 공개/구매/업로드 없음.
- dev/다른 worktree·LeartesStudios dirty·TightropeCourse.prefab 기존 위치4곳·.playwright-mcp 보존. 기존 검증 씬/검증 MISSING 정지 정책 유지. HumanoidPlayerBuilder.Build 재실행 금지(기존 출력 guard). CLI beta.6 named --caller/--skill 호환 예외 유지.
- 저장 요청(2026-10-20): private Task062 c10ca97e·main 6030fe10 저장/push 완료, 원격 SHA 일치. main 병합·push는 권한 차단 후 사용자가 직접 실행; 병합 후 compile PASS/dirty scene0. 원격 integration/humanoid-characters는 최신 origin/dev dcfbd22 기준 준비. 공개 선택 저장·최신 통합 합치기·PR 본문 확인은 진행 중. 지식: Harness/Project/Facts/humanoid-validation.md.
