# Log

- 2026-11-05: doctor 1회 실행: 외부 에셋 미초기화·기존 Task ID 충돌·Unity 미설치 실패 및 CLI 버전·gh 인증 경고. 외부 에셋과 Unity 검출은 샌드박스 제한 영향이며 권한 있는 읽기로 실제 에디터 6000.3.25f1 및 프로젝트 경로를 확인했다. 코드 수정·검사에 gh 인증은 필요 없다.
- 2026-11-05: origin/dev fetch 후 HEAD와 동일(e95b6d9b) 확인, fix/shop-quantity-key-scene 생성. 초기 추적 변경 없음. 샌드박스에 Unity 프로세스가 숨겨져 있었으나 작업 트리 내용의 동기화 변경은 없었다. 에디터 타임아웃은 기존 WaitingRoom 외부 변경 확인 대화상자 때문이며 Ignore로 메모리를 보존, dirty 0 확인.
- 2026-11-05: 서비스 Entry.Available이 모든 버튼을 막는 원인 확인. 예약·수량 조건을 분리하고 자금 부족 때만 예약을 막도록 수정. 예약/시간대/신체 상태 거절은 수량에도 유지. 외부 UI 키트 수정 없음.
- 2026-11-05: 열쇠 회귀 검사 수정 전 1/1 실패: 이전 활성 씬 언로드 때 비부모 heldItemVisual 파괴. 플레이어 자식으로 생성하고 표시 유실 복구를 선택 변경과 분리. 수정 후 열쇠 2/2 통과(씬 유지·손목 회전·쥠 시간 연속성 및 기존 걷기 몸통/왼팔).
- 2026-11-05: 여러 검사 이름을 |로 묶은 Pipeline 필터는 부분 문자열로 처리되어 total 0이었다(NO_PROJECT_TESTS, PASS 아님). 이름 접두사로 나누어 필요한 검사를 실행했다. 개별 결과는 .harness/local/task109-*-results.json에 보관한다.
- 2026-11-05: 서비스 검사 첫 실행 3건 중 신규 수량 검사·기존 다중 복구 검사는 통과. 기존 쉬는 시간 검사에서 ServerCut의 기본 인수 추가를 반영하지 않은 리플렉션 호출이 실패해, 의도한 단일 다리 손실을 ServerCutPart(LeftLeg)로 명시했다. 런타임 부상 규칙 수정 없음.
- 2026-11-05: 최종 PlayMode 회귀 검사 총 5/5 PASS: W2_BodyRestore 3/3(수량 2→3→1, 잔액 0, 예약 후 잠금, 다중 복구 단가, 쉬는 시간), HeldKey 2/2(씬 언로드, 유실 복구 시 동작 재시작 방지, 쥠 시간·손목 안정성, 걷기 왼팔/몸통). 수동 다인 플레이·사용자 육안 재현은 미실행.
- 2026-11-05: 검사에서 자동 변경된 두 TMP 폰트 아틀라스는 .harness/local/task109-generated-fonts에 백업 후 최초의 깨끗한 HEAD 내용으로 복원 완료. 원본 씬·프리팹·외부 에셋·폰트 변경은 핫픽스에 포함하지 않았다. commit/push는 하지 않았다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-05 제출 요청: 사용자가 커밋·푸시·dev PR 생성을 명시 승인. gh 로그인 MoHoDu 확인, origin/dev 재fetch 후 HEAD와 동일(b59e9ede), 기존 PR 없음. 기존 대상 회귀 검사 결과를 재사용하고 제출 전 변경 안전성·현재 파일 검사를 수행한다. 실제 지속 떨림/다인 플레이/브라우저 미검증은 PR에도 명시한다.
- 2026-11-05: check-work 1회 종료 코드 0. 위 테스트 SKIPPED는 전체 검사의 옵션이며 별도로 실행·기록한 대상 PlayMode 5/5 PASS를 대체하지 않는다. 기존 문서 길이 경고 55건(hard 0), private submodule 미저장 변경 2건은 이번 코드 작업과 별도로 보존했다. 최종 플레이 경험은 사람 확인 대상이며 핫픽스는 검토·커밋 대기 상태다.
- 2026-11-05 후속: 사용자 확인 — 열쇠 떨림은 자신/동료 모두, 씬 전환 없이 정지해도 발생. 쉬는 시간·밤 정비 타이머 및 방장 퇴장 시 참가자 종료 설문 누락을 같은 핫픽스 범위에 추가. doctor는 같은 Task에서 반복하지 않았다. origin/dev 재fetch 및 HEAD 동일 확인, 기존 변경 보존.
- 2026-11-05 후속: 기본 4캐릭터×4부상 정지·이동 진단에서 지속 떨림 자체는 미재현(.harness/local/task109-key-bone-measurements.txt). Animator.Rebind 후 실제 쥠 상태와 캐시가 달라지는 결함은 회귀 검사에서 수정 전 실패, 실제 현재/전환 상태 확인과 비활성 Animator 보정 중단 후 HeldKey 2/2 PASS. 기본 이동·달리기·앉기·점프 1/1 PASS. 사용자 화면과의 동일성/최종 육안은 미검증.
- 2026-11-05 후속: 타이머의 정지 전환은 SameStep이 Timer를 포함해 즉시 전송함을 확인, 최초 전송 제한 가설은 폐기. 미수신 재요청·늦은 메시지 바인딩 복구·정지/무제한 heartbeat·서버 전송 시각 지연 보정·클라이언트 실시간 기준 표시를 적용. RestTimerTests 16/16 PASS(실제 수신/Update, 쉬는 시간/밤 정비, 로컬 timeScale 0, 지연·정지·재개).
- 2026-11-05 후속: 설문은 Offline/세션 소멸 때 마지막 런/슬롯을 먼저 비우던 경로를 고쳐 콜백 누락 시에도 예약. 기존 M1 고정 테스트가 현재 M2 설정에 의존해 5/5 실패하여, 테스트 설정을 독립시키고 원본을 복원하도록 보완. SurveyBootstrapTests 5/5 PASS(참가자 1~3, 직접 나가기 없이 콜백 누락, 중복/런 없는 퇴장). 실제 브라우저/설문 전송·여러 PC 검사는 미실행.
- 2026-11-05 사용자 지시 우선: 타이머는 다른 작업자의 PR #307 병합본으로 통일. 자체 CampaignRun/RestTimerTests/구역 전환 테스트 실험은 .harness/local/task109-before-dev-sync에 보관 후 제거하고 관련 임시 지식도 제거했다. 실험의 16/16 통과는 최종 구현 검증으로 재사용하지 않는다. 구역 검사 첫 실행의 0.077초 차이는 멈춤 판정 전 전환 프레임 차이였으며 실험 수정은 채택하지 않았다.
- 2026-11-05: Unity stopped·dirty 0 확인 후 graceful 종료, origin/dev b59e9ede(PR #306~308)를 fix 브랜치에 fast-forward 반영하고 Unity 재실행. 별도 merge commit/작업 commit/push 없음. 열쇠·상점·설문 변경은 모두 보존했다. 최종 타이머 검사는 dev의 RestTimeRulesTests(미확정 공연 스케줄 공유 정지) 기준으로 수행한다.
- 2026-11-05 최신 dev 확인: RestTimeRulesTests 5/5 PASS. UnconfirmedScheduleSharesStoppedTimerUntilConfirmed에서 확정 전 Stopped 공유 및 확정 후 Running 복귀 확인. CampaignRun·RestTimerTests·구역 테스트에는 핫픽스 로컬 변경을 남기지 않았다.
- 2026-11-05 최신 dev 재검증: HeldKey 2/2, SurveyBootstrapTests 5/5 PASS(.harness/local/task109-dev-*-results.json). 타이머 포함 최종 12/12 PASS. Survey 테스트가 사용한 M1 설정·버전은 원래 M2 설정으로 복원된 것을 확인했고, 자동 생성된 폰트 변경도 별도 백업 후 복원했다. 사용자 지속 떨림 화면과의 동일성·여러 PC 실플레이·실제 브라우저 실행은 미검증.
- 2026-11-05 후속 최종 check-work 1회 종료 코드 0: 안전성·문서/규칙·Unity 컴파일 PASS, 별도 대상 회귀 검사 12/12 PASS. 기존 문서 길이 WARN 55/hard 0 및 private submodule 미저장 2건은 보존. 상점 기존 수정 포함 검토 가능한 상태이며 작업 커밋·push는 하지 않았다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
