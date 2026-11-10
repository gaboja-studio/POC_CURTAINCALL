# Log

- 2026-11-10: doctor 1회 실행. 초기 sandbox에서 Unity 설치/프로세스·private submodule이 가려져 실패로 보였다. 권한 확장 후 현재 프로젝트 Unity 6000.3.25f1 ready/stopped 및 ThirdParty OK 확인. 기존 Task ID 중복은 별도 문제.
- origin/dev fetch/ff-only 반영: 806c8dab, Already up to date. 재개 때 미저장 씬 없음 확인 → Unity 정상 종료 → 다시 fetch/ff-only 최신 확인 → 동일 에디터 재개.
- 구현: 모든 휴식·정비 300초 및 시간대별 탐색 잠금 기본 OFF. Unity SerializedObject로 BaseGameSettings만 저장하고 GUID 83e0f3bf71fe246479b4e65d31320546 보존.
- 일정: 확정/빈 칸 제거, 미진행 칸은 휴식·정비 중 즉시 변경·동기화. 해금+리허설 이수한 인간대포는 직접 선택하지 않은 미래 칸의 기본값. 첫날 안내·첫 공연/첫날 단계·해금/리허설 조건 유지.
- 야간 추격: 만료 후 Night/Pursuit 유지, 호스트 준비 상태로 수면자 공격 제외. 포획 완료자는 생존 수면 인원에서 제외, 아침에 외팔 부상으로 대기실 출발점 복귀. 밤 공연장 집결은 다음날 첫 공연(마지막 공연일 밤 제외), 탈출 집결도 연결.
- 안전지대: Tools/Curtain Call/Waiting Room/Safety Zones. 기존 ExplorationTimerZones 콜라이더의 안전 여부를 층별 지도/목록에서 편집한다. 선택·다중 선택·전체/층 지정 및 해제·구역 밖 안전·Undo·씬 저장. 전원 안전 시 밤/마지막 아침도 타이머 정지, 이미 시작한 추격은 계속.
- 겹치는 경로: 대기실 NightSchedule/ExplorationTimer/Pursuit/UI와 공통 Campaign/Survival/NetworkPlayer. 대기실 담당에게 알릴 변경 요약은 위 항목. 외부 메시지·commit·push 없음.
- 재개 때 별도 escape-labels.asset 및 동적 폰트 변경이 관찰되어 보존했다. 이 Task의 수정 결과로 간주하지 않는다.
- Unity 컴파일 PASS. 집중 EditMode BreakFlow: 35/35 PASS. 최초 7실패는 기존 가이드 대기 상태 미완료/RefreshNight 반환값을 단계 전환으로 오해한 검사 준비 문제였으며 수정 후 전부 통과.
- check-work 1회: 범위/구조/지식/ThirdParty PASS. 기존 Task-20261102-097-rehearsal-flow/todo.md 102줄(hard 80) 때문에 verify-context FAIL, 컴파일/테스트는 선행 실패로 SKIPPED. 별도 Unity 컴파일/집중 검사를 사용한다.
- PlayMode 집중 검사: 7건 중 6 PASS, 실제 포인터 1건은 클로즈업 전환 대기 누락으로 실패. 카메라 전환 완료 조건을 추가하고 해당 1건 재검사 PASS. 최종 고유 7/7 PASS(야간 수면 보호/아침 외팔 부활·기본 개방/수면·확정 없는 일정/난이도·첫 인간대포 선택/이수).
- 실제 통합 대기실의 안전지대 창 확인: 26개 구역, -10m 4개/-5m 4개/0m 20개(계단 중복 표시). 큰 지하 판정 상자가 지상에 섞이던 표시는 추격자 도구의 방 바닥 기준으로 보정했다. 최종 컴파일 PASS, 원본 씬 dirty=false. 창을 열어 두었다.
- BaseGameSettings 에디터 저장 시 기존 코드의 bodyDamage 기본값 3개(접촉1초/낙하3m·5m)도 직렬화되었다. 해당 규칙을 새로 변경한 것은 아니다.
- 확인 한계: 실제 두 PC 동시 수면/추격은 미검증. 범위 밖 Task-097 문서 초과는 수정하지 않았다. 코드/설정/창 구현과 이번 집중 검사를 완료했다. commit/push 없음.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-10 후속 수정
- 사용자 화면의 미확정은 private 원본 ScheduleCardVisual이 별도로 그리는 도장이었다. private 원본은 보존하고 실게임 어댑터에서 도장 부모를 숨겼다. 카드 가독성·단계 숫자 표시와 실제 안내 파일(N01/N05)의 확정 문구도 수정했다.
- 모든 휴식/정비에서 미진행 칸의 호스트 편집을 허용한다. 첫날 안내 대기 중 시간 제한도 제거했다. 완료 공연 기록은 유지한다.
- 실제 CampaignRun은 모든 휴식/정비 진입 시 전원 가이드 ACK를 기다린다. 마지막 카메라 복귀·블렌드와 잠금 안내 대기열 완료를 확인한다. 시간대 식별값까지 ACK에 넣어 지연 패킷을 거절하고, 나간 인원은 집계에서 뺀다. 순수 RunState의 기존 도구 생성자는 호환 유지한다.
- 정비 치트 화면의 RestTime 전용 표시 조건을 제거했다. 정비에서도 타이머 가감·초기화·만료·추격 단계 조작이 가능하다.
- WaitingRoomSettings의 restPresentation/maintenancePresentation에 60/30/10초 알림 제목·본문, 추격 단계 알림 제목/본문과 팀 임무 제목/본문/목표별 줄을 별도로 저장했다. 실제 배너가 고정 본문으로 덮어쓰던 경로도 제거했다. Unity API로 에셋을 저장했다.
- 공연 해금 이벤트가 호스트 리허설 이동 팝업을 예약한다. 실제 해금 직후 안내하며 아니오는 탐색으로 돌아간다. 선택 시 재안내와 기존 리허설 이수 조건은 유지한다.
- 착수 전 미저장 씬 없음/Play stopped 확인 → Unity 정상 종료 → origin/dev fetch/ff-only Already up to date → 에디터 재개. 사용자 WaitingRoom.unity 변경과 기존 미저장 파일은 보존했다.
- 후속 check-work: 안전/구조/지식/ThirdParty PASS, 기존 Task-097 todo 102줄 초과로 전체 검사 FAIL. 별도 verify-unity -Compile 및 Common settings PASS.
- 후속 집중 EditMode 25/25 PASS. 실제 PlayMode는 가이드 종료·미확정 도장 제거·해금 팝업 3건 PASS. 정비 알림 검사는 치트 창 미생성이라는 테스트 준비 오류를 보완했고 재검사 PASS, 추가 낮 휴식 편집도 PASS(최종 고유 5/5). 실제 두 PC는 미검증.
- 최종 화면에서 단계 버튼의 rich-text 태그 노출/생략을 숫자 표시로 정리하고 카드 보상 줄 자동 크기를 적용했다. 해당 실제 공연표 테스트 1건 재검사 PASS, 스크린샷 `.harness/local/break-followup-schedule.png` 확인. 최종 컴파일 PASS. WaitingRoomSettings를 Inspector에 선택하고 두 문구 설정 그룹을 펼쳐 두었다. commit/push 없음.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-10 사용자 저장·dev PR 요청
- 관련 변경을 MoHoDu 계정으로 `6f0c7ea6`에 저장했다. 별도 escape-labels 머티리얼 변경은 미포함·보존했다. Unity 종료 뒤 origin/dev PR #319(64ccd465)를 `906fd5ac`에 통합했다.
- CampaignRun·NightBed·인간대포 PlayMode 테스트의 충돌 3곳을 해결했다. dev의 공통 상호작용 제한과 수면 인원 집계를 유지하며 수면 보호·포획 제외·가이드 장벽을 결합했다. 씬/프리팹 충돌은 없었다.
- 통합 check-work: 안전성 PASS, 문서/규칙 PASS(기존 Task097 길이 문제는 최신 dev에서 해소), Unity 컴파일 PASS, Common settings PASS. 자동 테스트는 아래 집중 결과를 사용한다.
- 통합 집중 EditMode: BreakFlow 41/41, PursuitQuestText 2/2 PASS. RehearsalLifecycle 최초 27/29에서 ACK 식별 키 변경과 리허설 중 완료 보고 문제를 확인했다. 현행 시간대 키로 테스트를 갱신하고 일반 안내 ACK를 리허설 동안 보류한 뒤 29/29 PASS. 재컴파일 PASS.
- 통합 PlayMode BreakFlow 7/7 PASS. 후속 가이드·알림·편집 PlayMode 5/5 PASS(전체 고유 10건). 실제 두 PC 동시 플레이는 미검증이다.

- 증거: `.harness/local/break-submit-editmode-{flow,text,lifecycle-retry}.json`, `break-submit-playmode-{flow,followup}.json`. 최종 EditMode 고유 72건, PlayMode 고유 10건 모두 통과했다.
- submit-work new-pr DryRun으로 dev 방향·Squash·본문을 확인했다. 무관한 머티리얼 변경을 보존해야 하므로 전체 dirty 차단 도구 대신 명시 경로 commit/push와 gh --body-file로 PR을 등록한다.
- 마지막 ACK 수정 뒤 check-work 재검사: 안전성·문서/규칙·컴파일·Common settings PASS. Unity 정상 종료 후 제출 직전 origin/dev 재fetch/통합 결과 64ccd465, Already up to date. 종료 CLI의 잘못된 옵션 호출 1회는 파일 변경 없이 실패했으며 올바른 위치 인자로 정상 종료 후 동기화했다.
- 제출 완료: [PR #320](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/320), `fix/waiting-room/break-time` → `dev`. 기능 저장 6f0c7ea6, dev 통합 906fd5ac, ACK 후속 저장 d5eec5d1을 push했다. dev 병합·리뷰어 추측 배정은 하지 않았다.
- push 성공. GitHub의 기존 WaitingRoom.unity 75.48MB 권장 크기 경고만 발생했다. 별도 escape-labels.asset 변경은 로컬에 남기고 PR 파일에서 제외했다.
