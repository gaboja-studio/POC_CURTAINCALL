# Handoff

## 지금 상태

- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/203 (dev 대상, 대기실 후속 수정 제출; 이전 #191과 구분)
- 제출 검증: 최신 dev 충돌 없이 병합·push. 전체 검사는900초 시간 초과 후 취소했고, 사용자 지시로 관련 PlayMode8/8 PASS만 검사해 제출했다. 전체 통과를 뜻하지 않는다. 구역·문서·컴파일 PASS, 원격 사람 공동 검증 미실행.
- PM 긴급 승인으로 @gyumin9048-ctrl 임시 담당, `fix/waiting-room-bugs-improvements` → dev. [exception.md](exception.md), 상세 변경·검증 이력 [waiting-room-followup.md](waiting-room-followup.md).
- 최신 추가 천: 사진은 CanvasOnCrate_4. 같은 원인의 CanvasOnCrate_0/1·FoldedCanvas까지4곳을 실제 벽에 연결된 줄에 걸었다. 전체 메시 상단 윤곽 지지·실내/보관실 재생성 연동, 장식 충돌체0. 씬 저장 완료.
- 숙소 천4개는 침대 위 이동을 폐기하고 빨랫줄에 걸었다. Rope/Rod 모두 실제 천장 면을 먼저 확인하고 양끝 연결선2개와 고정부2개를 생성한다. 가정한 WallAnchor 좌표 고정 방식은 폐기했다.
- 상점 Phone/DeliverySlot 위치·배출 방향·조명 복구, 트렁크3개를 받침 높이에 맞춤. 원본 에셋·사람이 조정한 의자/수화기 미세 위치·다른 기존 미커밋 변경은 보존.
- BUG-004: 대기실 내부 플레이어 간 이동 제한만 해제, 외부/공연/비활성 시 복구. 환경 충돌 유지. BUG-005: 집결 폭7.7m·길이4.65m, 복도 양벽 안쪽·커튼 뒤 끝벽까지 포함.
- 개인 재입장 보정 제거, 첫 개인 퇴장 팀 타이머·잔류자·중복 집계 방지 유지. 기존 야간 셔터·스케줄표 연결 유지. 실제 로비 스폰4개, 더미 선택 UI 비활성.
- 의자는 현 위치를 보존하고 메시 구멍 대신 메시 bounds의 BoxCollider 사용. 양다리 손실 초기 Crawl 포즈 즉시 적용.
- 카메라: 대기실 전용 로비 프리셋으로 복귀0.35초 보간. 좁은6구역 exitPadding=1m로 몸통이 문턱을 완전히 벗어난 후 전환. 기본1/3인칭 변경 보류.
- 이전10-20 제출: Task-064 6bd83ca·Task-065 e22c088, [PR #191](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/191) dev 대상 미병합. 10-21 후속 변경은 사용자 저장 요청으로 검증 후 현재 브랜치에 선택 커밋·push한다. 폰트·재질·UI·Vivox 기존 변경 제외.

## 다음 할 일

- PM은 후속 PR에서 Task-060 아트 도구·Task-062 PlayerMover/Humanoid/Camera 변경의 긴급 예외를 검토한다.
- 원격2프로세스 공동 검증 미실행(MPPM 구성0). 사람 공동 확인: 부활·관전 해제 / 퇴장·팀 타이머·잔류·집결 / 밤 잠금·F 스케줄·전원 준비. 실제 상점 구매·수령 및 모든 카메라 가림 구간도 미검증.
- 기존 Additive 씬 개수 실패 및 키보드 회귀 불안정은 남아 있다. PR 병합·커밋·push는 사용자 요청 시 진행.
- 교훈 제안: 메시 복제 시 모든 생성 경로에서 실제 지지물도 함께 검사한다. 숙소만 확인하면 상자 장식의 동일 원인을 놓친다. 상세 후속 기록 참고.

## Verification

- 최신 천4곳: 재적용 최대 위치차 .000002m 이하·회전0·구조 중복0, 벽 지지 누락 시 기존 구조 보존하며 생성 거부. 비대상 Transform 동일·렌더4곳 직접 확인, 씬 isDirty=false. Temp/CanvasOnCrate_4-supported.png 등 진단 화면 보관.
- 최신 Unity 컴파일 completed/failed=false/errors0. 구역 PASS(27파일, origin/dev, 긴급 예외 공개), 문서 PASS(hard0·기존 WARN12), diff 공백 PASS. 편집 배치 변경에 별도 PlayMode 테스트 추가 없음.
- 이전 출입 카메라 관련2/2 PASS: 문턱 유지→완전 진입 후0.35초 보간→경계 재진입 유지, 기본padding0 기존 회귀. 숙소 Rope/Rod 천장 지지·누락 거부·렌더 확인 PASS.
- SpaceLink 최신8/9 PASS: 플레이어 실제 통과·집결·의자 접촉/후퇴·재입장·밤 잠금 통과. Additive_KeepsHubAndSwapsSpaceScenes 예상2/실제3 실패 유지(현재486행).
- Humanoid 최신11/12 PASS: 초기 손실 Crawl 회귀 통과. KeyboardEventsReachReaderControllerAndHumanoidAnimation Walk 대신 JumpLand 실패, 단독 재검사1/1 PASS. 전체 PASS로 간주하지 않는다.
- 10-20 당시 검증: SpaceLink6/6·부활 가드4/4·실제 인간대포 사망→대기실 외팔 부활1/1·EditMode815/815. 당시 성공은 현재 전체 테스트 PASS를 뜻하지 않는다. 이전 별개 줄타기 시간 초과3건은 재실행하지 않았다.
