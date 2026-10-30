# Log

## 2026-10-30 목마 도달·양방향 반출교·수평 발사 톱날
- 최신 dev88cbb574를 기존 미커밋 변경 위에 통합했다. 사전 사본/해시/인덱스는 `.git/task-session-backups/Task-20261027-073/four-coop-20261030/`. 일반 apply의 문서 문맥 검사는 실패했으나3way 재검사·적용은 충돌 없이 완료했다. pseudoref update-ref 거부 후 기존 값 확인·MERGE_HEAD 갱신, dev/origin/dev/MERGE_HEAD 모두88c. private26d5009e는 dev gitlink와 일치·clean, 새 대포/트램폴린 LFS를 받아 실제 A 외형4/1곳에 적용했다.
- 목마 전원/반출교 스위치2곳을3.6→4.4m로 높이고 표시등/전기함/권상선도 연결했다. 공통 점프 기준 혼자 최대 도달4.05m보다 높다. 기존 EscapeSwitchInteraction에서 아래 목마 탑승자의 다리 절단에 따른 도달 높이 손실도 보완했다. 팔 절단 시에도 유효하며 몸통/목마 물리는 유지한다.
- 상승 대포 상부 표시등을 실제 F 유지 버튼으로 연결하고 자동 착지 고정을 없앴다. 버튼 이후 하부 봉을 놓아도 대포가 유지된다. 마지막 반출교 건너편(324.2,1.5,78)에 반대 방향 봉/지지대를 추가하고 기존 봉 시스템16번째에 등록했다. 양쪽 봉이 같은 다리를 제어한다.
- 추격 화염의 전진 간격6→4초(1.5배), 시작 대기20초 유지. 기계/발사 날 지름 약1.1m·반두께.0465m로 통일하고 수평 축·평면을 맞췄다. 원본 기계 메시를 파생 발사 메시로 베이크, 회전16/16·기계4/4 수평 유지, 변경된 실제 날 판정으로 배선24/24 절단 경로 확인. missing0/broken0, A 저장·dirty=false·Play 중지.
- 트램폴린 손잡이를 world rotation/lossyScale만 복사하면 비균등 조상 배율 때문에 접촉 경계가 커졌다. 최초 보정 호출은 테스트 중 임시 씬이라 대상 없음으로 중단(변경 전), 테스트 종료 후 A에서 정확한 로컬 변환 체인을 복제해 수정했다. ContactBounds 차이2.86e-6, 중복 외형 제거. 지식 visual-contact-bounds에 기록·verify-knowledge OK.
- 명시적 컴파일 오류0 후 EditMode 협동22/22·스위치13/13·출구 톱23/23·추격 화염14/14·최신 기물 아트15/15, 합계87/87 PASS. 근거 `.harness/local/exit-door/four-coop-*.json`. 자동 Play/입력 미실행, 실제 협동 난도는 사용자 검토. 같은 Task의 doctor 결과 재사용. 사용자 후속 저장·제출 요청에 따라 검사 후 작업 브랜치 커밋/푸시 진행, dev 직접 반영 없음.
- 최종 check-work 최초 실행은 todo82줄 제한 초과로 문서 FAIL·컴파일 SKIPPED였다. 완료된 원문을 History/018로 옮긴 뒤 재검사: 안전성/문서·규칙/컴파일 PASS, hard0·기존 경고33, 자동 테스트 SKIPPED(별도87/87 결과 사용). 원격 작업 브랜치는 HEAD 조상 확인. 제출에는 요청한 공통 달리기2.0/문 설정을 포함하고, 기존 개인 외형 선택·MultiplayerManager 로컬 변경은 보존하여 제외한다.

## 2026-10-30 시작 전망창 숏컷·틈 너머 봉 잡기 차단
- 사용자 발견: 출발지에서 타이밍 점프로 첫 대포/하강을 건너뛰고 컨베이어 봉을 바로 잡는다. fetch 결과 dev b19189eb=기존 MERGE_HEAD, 추가 통합 없음. 같은 Task doctor 재사용, 사용자 삭제·편집과 기존 배치 보존.
- 원인은 열린 낮은 흉벽과 거리만 사용하는 봉 잡기였다. 유리는 어색하다는 사용자 정정으로 만들지 않았다.51_ObservationSafetyFrames에 금속 프레임만 연결했다. 첫0.38m 간격이 촘촘하다는 추가 정정 후 실제 CharacterController로0.44~0.59m 틈을 검사해 직경0.6m 몸통을 막는 최대약0.578m 틈·세로 두께0.06m로 넓혔다. 세로90개/중복 외곽1개 제거 후 총101기물, 유리0.
- 기존 HumanCannonPole에 requireClearGrabPath(기본false)를 추가하고 A의 활성15개에만 켰다. IsInReach/IsSeatInReach/지연된 순환 봉 위치 판정 모두 동일한 몸통 폭 SphereCast를 사용해 좁은 틈의 거리 잡기/호스트 승인 우회를 차단한다. 프레임에 몸을 밀착했을 때 cast 초기 겹침이 누락되는 것도 전방 OverlapSphere로 막는다. 자신/해당 봉/다른 플레이어/Trigger/무시 레이어는 제외하며 다른 씬 기본 판정은 유지했다. 역할/진행도 잠금이나 새 이동 스크립트는 없다.
- 실제 CC(직경0.6/skin0.08)의 서기1.5m·앉기0.9m로93틈×2,186/186 횡단 차단; 같은93틈 너머 잡기도93/93 차단. 정상 중립 접근15/15 허용. 대상별 시각 표본에서 기존 실제 경로14/14가 프레임 사이로 보임(frame-visibility.json). 근거 before-safety-frames/before-frame-spacing.unity, frame-audit.json.
- 기존 CooperativeUpgradeTests에 기본값/솔리드 프레임/Trigger/밀착 초기 겹침/UI와 호스트/정상 방향 접근을 추가해15/15, PoleCrew12/12·PoleLoopDirection8/8, 합계35/35 PASS. recompile 요청2회는 자동 reload 중 HTTP400이었고 ready 후 재요청은 오류0/up_to_date, 최신 추가 사례가 실행된 것을 확인했다. Play/실제 플레이어 자동 입력/commit/push 없음(별도 CC 오프라인 고정물 검사만 수행).
- 최초 check-work는 미저장 씬 선행 점검에서 중단됐다. 현재 A의 dirty=false/Play정지/프레임101개를 확인한 뒤 재검사 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(별도35/35 재사용), 기존 길이 경고34/hard0. 사용자 삭제한 기물은 복원하지 않았다.

## 2026-10-30 공중 이동 입력 해제 즉시 정지
- 최신 사용자 지시로 무입력 관성 유지를 대체했다. fetch 결과 origin/dev b19189eb=MERGE_HEAD, 추가 통합 없음. 같은 Task doctor 재사용·기존 변경 보존.
- 기존 PlayerMover의 일반 공중 조향에서 입력0이면 airVelocity를 즉시0으로 만든다. 낭떠러지 첫 프레임의 groundVelocity 인계도 같은 규칙을 따른다. 입력 유지 시 달리기 이륙 속력과 조향은 유지하고, 중력/점프 높이/외줄 옆점프/외부 묘기 비행은 변경하지 않았다. 새 스크립트/설정 없음, 공통 이동3·달리기2 유지.
- 기존 TightropeMovementFeedbackTests를 최신 의도에 맞춰 입력 해제/계속 누름·정지 후 재입력·낭떠러지 두 경우로 수정했다. 명시적 컴파일 오류0, 최신 회귀11/11 PASS(기존 외줄/높이/목마 포함). 근거 `.harness/local/exit-door/air-input-stop-tests.json`. Play/자동 입력 없음.
- 검사 중 사용자가 Unity 저장 대화상자를 보여주고 에셋 배치를 직접 삭제했다고 알렸다. 삭제를 복원하지 않으며 Save로 현재 씬 편집을 보존하도록 안내했다. 이번 수정은 이동 코드/기존 테스트/문서만 변경하며 씬 재개방·이전 배치 덮어쓰기를 하지 않는다. 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(별도11/11 재사용), 기존 길이 경고34/hard0. 에디터 ready·열린 씬1/dirty0 확인. commit/push 없음.

## 2026-10-30 스폰·첫 대포 복도 실제 경로 전망 적용
- 사용자 승인으로 제안 계획을 구현했다. fetch 결과 origin/dev b19189eb=기존 MERGE_HEAD, private26d5009e 유지, 새 통합 없음. 기존 변경/미커밋 통합·비행 복원·공통 이동3/달리기2를 보존했다. 같은 Task doctor 재사용.
- 4스폰을 X23/24.5·Y0.05·Z9.25/10.75의2×2로 옮기고70도 시선으로 실제 협동 발판·트램폴린을 보게 했다. 시작/복도 흉벽은0.6m로 낮추고 X55~58 깊이 전망, X72.5~77.5 협동 구간 벽, Z61~62.5 교차벽에 실제 시야 개구를 맞췄다. 기존 벽16개를36조각으로 다시 연결했으며 첫 대포·실제 기물 위치/원본 호스트 판정은 유지했다.
- 50_ActualRouteReveal에 씬 전용 메시를 만들고 월드 좌표 UV로 접합부를 맞췄다. 현재 공면 삼각형 기준으로 새 메시26개의 중복 면만 제거, 재검사 유효 중복0(0.001㎡ 미만 수치차 제외). 벽 수정으로 뜬 배관/플랜지/브래킷/누수24개와 옛 조명 지지13개 제거. 기존 조명15세트는 벽 직부, 실제 경로를 비추는 벽등2세트를 추가해17/17 벽 Raycast 부착 확인.
- 주변 기존 횡단 톱은 선반 위 고정 레일/기둥·MovingVisual의 축으로 연결하고 압착 가이드는 기존 상부 보에 닿도록 늘렸다. 하강 그네3개의 상부 보는 실제 천장 Raycast 접점까지 하중봉6개로 연결했다. 새 게임 스크립트/이동 방식은 만들지 않았다.
- 스폰4곳에서 실제 협동 발판/트램폴린, 복도2지점과 첫 대포에서 실제 그네/착지 외줄 등 시야14/14 clear. 스폰 바닥4/4·머리 공간4/4, 기존 입구를 지나는 접근6구간 CapsuleCast 통과. 최초 임의 대각 표본은 기존 입구 남측 벽 모서리에 걸려 실제 열린 입구를 따라 표본을 수정했으며 벽 자체는 유지했다.
- 전후 원본 기물 컴포넌트/위치 유실0·끊긴 참조0·누락 스크립트0. HUD는 화면 크기에 따른 RectTransform 월드 좌표 변화를 별도로 구분했다. 미저장60줄은 새 조명2개에 URP가 자동 부착한 AdditionalLightData뿐임을 사본 diff로 확인해 보존 저장했다. 실 플레이/자동 입력/commit/push 없음.
- 근거 `.harness/local/exit-door/`: before-reveal.unity, reveal-changes/fixtures/removed-pipes/swing-supports.txt, reveal-audit.json, reveal-entry-path.json, reveal-spawn/corridor/first-cannon.png. 스폰 검사는 과거 Z10 제한 대신 실제 시작방 바닥 범위를 사용하도록 기존 EscapeFinaleIntegration을 수정했다. 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(씬 배치 중심 수정), 기존 길이 경고34/hard0. 저장/재개방 후 시야14/14·부착17·스폰4/4·기물 보존 재확인.
- 기존 ValidateFinaleHazards 전체 검사는77통과/16실패였다. 변경 전 사본을 preview scene으로 열어 같은 검사를 비교한 결과 실패 목록과 결과 문자열이 전후 완전히 같았다(reveal-legacy-comparison.json). 오래된13함정/14cue 개수 가정과 기존 위상/활성 판정 실패를 이번 배치 PASS로 포장하지 않았으며 별도 Play 검증도 하지 않았다.
- 마지막 화면 확인 중 추가 편집이 감지됐다. 현재 에디터 사본(reveal-final-unsaved.unity)을 저장 파일과 비교하니 출구 배선 뱅크1곳의 local Z57.350002→58.7 한 줄뿐이었다. 이번 스폰 수정으로 되돌리지 않고 현재 에디터 값을 보존 저장했다. 앞선 기물 위치 보존 비교와 구분하며 이 추가 변경은 이번 작업에서 만든 변경이 아니다.

## 2026-10-30 원래1키 비행 복원
- 사용자 요청은 새1인 비행 제작에서 곧바로 '원래 있던 기능 다시 활성화'로 정정됐다. 원격 fetch 결과 dev b19189eb=기존 MERGE_HEAD, 추가 통합 없음. 같은 Task doctor 재사용·작업 보존.
- 기존 HEAD의 EscapeInspectionSession/Camera 두 파일과 원래.meta GUID, 기존 OriginalMechanicsTests의 삭제된 검증을 그대로 복원했다. EscapeExitSaw의 비행 점유/접근 예외도 복원하되 거대 양개문 변경은 유지했다. 원래 PlayerMover·공통 이동3/달리기2는 변경하지 않았고 Solo 대체 이동/배율/옛 Preview 비행은 복원하지 않았다.
- A Main Camera에 원래 Session/Camera를 활성 연결하고 기존 EscapeDevelopmentSetup의 연결/검사도 보수했다.1 토글, WASD/마우스, Space 상승·왼Ctrl 하강·왼Shift4배, Esc 커서 해제·우클릭 다시 고정. 비행 해제 시 벽 겹침을 검사하고 현재 위치에서 일반 조작으로 복귀한다. 새 게임 스크립트가 아니라 삭제했던 기존 기능 복원이다.
- 명시적 recompile 오류0. 기존 비행 복귀/재시작 위치 보존1/1·출구 회귀23/23, 합계 EditMode24/24 PASS. 근거 `.harness/local/exit-door/flight-restored-tests.json`, flight-exit-tests.json과 before-flight-restore.unity. A 저장 후 활성 비행/세션 각1·누락 스크립트0·dirty=false 확인. 컴파일 중 CLI 조회1회 통신 실패는 컴파일 완료 후 재조회로 정상 확인했다.
- 최초 check-work는 plan101줄로 문서 검사 FAIL·컴파일 SKIPPED였다. compact-docs로 이전 계획 원문을 History/017-plans-before-flight-restore.md에 보존 후 현재 계획을 압축했다. 재검사 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(별도24/24 재사용), 경고34/hard0. Play/자동 입력/commit/push 없음.

## 2026-10-30 출구 배선 바깥 벽·낭떠러지·문 아래 착지면
- 최신 fetch 결과 origin/dev b19189eb=기존 MERGE_HEAD, 추가 통합 없음. 같은 Task의 doctor 재사용·기존 변경 보존. 사용자 요청으로 출발 위치 계획보다 출구 배선 배치를 우선 수정했다.
- 가까운 배선 벽2개와40×40m 덧댄 바닥을 제거하고 기존14m 질주로(Z71~85)를 유지했다. 바깥 벽 내측 Z57/99에 배선6뱅크/24가닥을 부착했다. 뱅크 X379/382.5/386, 배선 Z57.35/98.65. 처음 한 가닥이 방벽에 가려져 실제 원판 궤적 검사 후 위치와 분배함 폭을 조정했다. 기존 분배함12개에 양 끝48곳 간격0 유지.
- 측면 정비대/그 위 소품/하부 받침도 함께 제거해 공중 잔여물을 남기지 않았다(안쪽 벽/덧댄 바닥 포함6루트 제거). 조명9세트와 상태 전기함은 새 바깥 벽에 맞추고 마지막28개 기존 화염 루트는 원래 바깥 벽 Z57.2/98.8로 정렬했다. 화염 에셋·타이밍/공통 이동·기물 코드는 변경하지 않았다.
- 사용자 정정대로 문 폭 전체 바닥은 유지한다. 문 바로 앞 X396~400에 양측 날개 바닥2개를 추가해 기존 중앙 바닥과 Z65~91의26m 폭 착지면을 구성하고, X400 이후 기존 실외 타일로 이어진다. 배선 구역의 양측 낭떠러지는 깊이48m, 폭 약14m이며 문 앞에서만 넓어진다. 스폰 변경 없음.
- 추가 정정: 문 밑으로 외부가 보이는 중앙 개구는 탈출문 하부 외벽(X396~400/Z66~90/Y-152~-3)으로 막고3m 두께 착지 바닥 밑에 연결했다. 하부5높이×5가로 위치의 외부 방향 Raycast25/25 차단. 바닥 위 여닫이 개구/양측 낭떠러지는 유지한다.
- 중앙 보행면에서 실제 ObstructionDistance/원판-배선 교차로 절단24/24, 분배함 끝점48/48, 낭떠러지/중앙 바닥21표본 확인. 문 전후 바닥28/28·누락 스크립트0. 근거 `.harness/local/exit-door/`의 before-outer-cables.unity, outer-cables-audit.json, door-floor-audit.json, outer-cables-removed.txt. Play/자동 입력/commit/push 없음. 하부 벽 추가 후 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(씬 배치만 수정), 기존 길이 경고35/hard0. 저장/재개방 후 하부 차단25/25도 재확인했다.

## 2026-10-30 마지막 구간 공중 작업등 재보수
- 사용자 재확인으로 마지막 질주 X140~출구의 매입 정비등39세트와 본체/발광 면을 다시 조사했다. 최신 fetch 결과 dev b19189eb는 기존 MERGE_HEAD와 같아 추가 통합은 없으며 기존 변경을 보존했다. 같은 Task의 doctor를 재사용했다.
- 이전 지지 검사는 일부 비볼록 MeshCollider.ClosestPoint가 멀리 있는 입력 좌표를 그대로 반환한 사례를 지지 거리0으로 오인했다. 이 때문에 기존114지지 연결 검사만으로 전체 조명 정리를 완료했다고 판단한 것은 불충분했다. 이번 검사는 현재 BoxCollider 벽면의 실제 Raycast 접점/법선을 사용했다.
- 28세트의 본체·발광 면·Light를 함께49_FinaleWallLights로 옮겨 현재 벽에 직접 부착했다. 잘린 기존 본체 메시를 큐브로 복구하고 벽면용 함체/발광 패널로 정렬했다. 지하/출구 바깥/적절한 벽이 없는11세트는 제거하고 기존 긴 지지3개도 제거했다. 상태 표시등·기물·배선/문·스폰은 유지했다. 새 게임 스크립트 없음.
- 저장/재개방 후 벽면 Raycast28/28·미부착0·missing scripts0·dirty=false, 화면 wall-lights.png 확인. 근거 `.harness/local/exit-door/`의 before-wall-lights.unity, wall-lights.txt, wall-lights-audit.json. 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(씬 배치만 수정), 기존 길이 경고35/hard0. Play/자동 입력/commit/push 없음.

## 2026-10-30 거대 양개 출구·겹침/공중 설비 정리
- 최신 dev b19189eb/private26d5009e 미커밋 통합 상태를 유지하고 fetch 시 추가 변경 없음을 확인했다. 기존 사용자 변경/백업을 보존했다. 다른 씬 보존 후 탈출로 전환은 사용자가 승인했으며 실제 전환 시 이미 A 편집 상태여서 다른 씬을 저장/폐기하지 않았다.
- A 기존 출구를 X400/Y0/Z78, 높이28m·폭24m 양개문으로 교체했다. 기존 EscapeExitSaw에서 양쪽105도/4초 개폐·리셋/늦은 참가 스냅을 처리하며 기존 EscapeRouteSettings Inspector에 각도/시간을 노출했다. 앞뒤 인방/방벽·지붕을 개구부에 맞추고 옛 셔터/주름을 제거, 실외 햇빛을 추가했다. 기존 화염은 같은 에셋/타이밍으로 벽 안쪽에 정렬했다.
- A 정적 직육면체1056개를 대조해 실제 같은 방향/평면의 겹친 삼각형만253개 메시에서 잘랐다. 새 씬 전용 overlap-cleanup 메시 라이브러리를 사용하며 공유 원본/충돌은 변경하지 않았다. 재검사 면적차0.0173㎡는 큰 면적의 float 분할 오차 수준이며 완전 무결한 전 시점 시각 검증을 뜻하지 않는다. 실외 타일과 새 바닥의 중복도 경계X400에서 제거했다.
- 옛 손잡이/교각·고정 암·연결처 없는 배관과 실제 받침 없는 바닥 흔적을 정리했다. 바닥 흔적 검증이 LandingZone 컴포넌트를 동적 물체로 오인한 것을 발견해 현재 충돌면 기준으로 재검사, 유효 표식은 원본 사본에서 복원하고 복원 중 중복48개도 제거했다. 최종 해당 잔여물 제거180개, 별도 장식 전력선/트레이148개 제거. 작업등/배관 지지114개는 배경1.25배를 상쇄해 실제 벽/천장/바닥에 끝점 연결을 확인했다.
- 출구 배선24가닥의 양 끝48곳은 벽 부착 분배함12개에 간격0으로 연결했다. 출구 상태 전기함도 북벽으로 옮겼다. 절단 접근24/24·닫힌 문 차단11/11·열린 문 통과11/11, missing scripts0·끊긴 직렬화 참조0·기물 컴포넌트 유실0. 최신 ExitSawTests23/23 PASS(양개 개폐/리셋 및 기존 발사/절단 포함). 편집 미리보기 후 닫힌 초기 상태로 저장했다.
- 근거 `.harness/local/exit-door/`: 원본/단계별 씬 사본, cleanup-changes.txt, placement/removed-power 목록, final-audit.json, open/closed-after-cleanup.json, tests.json, clean-open.png. Play/자동 입력/commit/push 없음. 출발 위치 검토는 후속 지시로 보류하며4스폰 좌표13.5~16.5/Y0.05/Z5 유지. 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(별도23/23 재사용), 문서 길이 경고35/hard0. verify-knowledge도13폴더 OK. 저장/재개방 후 지지114곳·기물 보존·누락 참조0 재확인, 실제 플레이 체감과 전 시점의 미세 깜빡임은 미검증이다.

## 2026-10-30 최신 dev 반영·달리기 점프 관성 보존
- 사용자 요청으로 fetch한 새 dev b19189eb(#240, 삐에로/소녀 모델·기본 외형)와 private26d5009e를 반영했다. dev는 fast-forward, 작업 브랜치는 --no-commit --no-ff 통합 후 MERGE_HEAD=b19189eb로 유지한다. 새 commit/push는 없다. HEAD 조상 포함 완료와 구분하며 최종 저장 요청 전까지 미커밋 통합이다. 같은 작업의 doctor 결과를 재사용했다.
- Unity 자동 import를 잠시 보류하고 미커밋/미추적 변경을 사본과 stash로 보존한 후 통합했다. 기존 파일34개 SHA256 일치, 기존 삭제18개/History 사본 복원, 충돌0. log 줄바꿈이 stash 재적용 때 CRLF로 바뀐 것을 발견해 사본의 원래 바이트로 복원 후 import 재개했다. 백업 `.git/task-session-backups/Task-20261027-073/air-momentum-20261030/`와 stash를 유지한다. private clean·LFS 포인터 잔존0, 이동3/달리기2 설정 유지.
- implement-code 절차로 기존 PlayerMover만 수정했다. 이륙 속력을 공중 입력으로3m/s로 덮어쓰지 않고 방향만 바꾼다. 입력0이면 수평 속도를 보존하며, 제자리 점프에서 첫 이동은 기존 공통 속도·부상·자세 배율을 사용한다. 걸어서 낭떠러지를 벗어날 때도 이전 지상 속도를 인계한다. 외줄 옆점프 제외 조건과 착지/외부 인계 시 속도 초기화는 유지했다. 새 스크립트/설정 없음.
- 기존 TightropeMovementFeedbackTests에 달리기/이동 키 해제·공중 조향·낙하·외부 초기화·제자리 점프 후 이동·공중 달리기 추가 가속 방지 사례를 추가했다. 최신 명시적 컴파일 오류0, 별도 EditMode10/10(기존 외줄/높이/목마 포함) PASS. Play/자동 입력 없이 검사했다. 근거 `.harness/local/air-momentum-{compile,tests}.json`. 실제 손맛 확인은 사용자에게 남는다.
- 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(별도10/10 재사용). A 씬·최신 UI/톱/이펙트·스위치 높이/절단 판정과 원래 사용자 변경을 보존했다.

## 2026-10-30 목마 스위치 높이·절단 상태 조작
- 작업 전 origin dev/작업 브랜치를 fetch해 최신 dev cf8bcab4 포함을 재확인했다. 같은 작업의 doctor 결과를 재사용했다. 대상은 A의 기존 shoulderControl 전원 버튼/반출교 레버2곳이며 일반 복귀 버튼은 목마용이 아니므로 배치를 유지했다.
- 실제 지지줄 상면(-18.5/0)에서2.45→3.6m로 올렸다. 전원 버튼(46,-14.9,-74), 반출교(171.5,3.6,78). 자식 레버·동적 안내 중심은 함께 이동하며 표시등/전기함도1.15m 올리고 권상선은 기존 상부 연결점을 유지해 길이를 줄였다. 부상 없는2~4인 상부/8방향1m 접근과 지상 비도달50조건 확인. 저장/재개방 missing0·dirty=false, 시각 캡처 확인.
- 사용자 후속 요청으로 EscapeSwitchInteraction에서 팔 도달 배율을 제외하고, 다리가 절단된 사람의 낮은 몸통 중심을 공통 정상 높이까지 보완했다. 기존2.5m 구형 경계/비활성 거절/로컬·호스트 공용 판정과 생존/잠금 검증 유지. 실제 캡슐·목마 자세·다른 기물의 부상 규칙은 바꾸지 않았다.
- 최초 테스트는 구 어셈블리10건이어서 최신 검증으로 세지 않았다. 명시적 recompile 후 오류0/기존 경고34(엄격 모드 종료1)를 확인하고 최신 스위치13/13(1~3부위 절단·짧아진 양쪽 목마·팔 도달 배율 포함) 및 호스트 협동13/13, 합계26/26 PASS. Play/자동 입력/commit/push 없음. 근거 `.harness/local/shoulder-switches/`의 result/tests/host-tests.json, raised.png, before.unity.
- 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 테스트 SKIPPED(별도26/26 재사용), 기존 길이 경고35/hard0. 공통 이동3·달리기2.0, 기존 화염·최신 아트와 이전 변경/백업은 보존했다.

## 2026-10-30 dev 재사용 에셋 최신화·상시 시작 원칙
- 사용자 요청: 매 작업 전 dev에서 탈출로가 사용하는 기능/UI/에셋 변경을 대조하고 실제 연결·배치·크기까지 맞춘다. 원격 fetch 결과 dev cf8bcab4는 이미 HEAD5cc0f058에 포함, 같은 작업의 doctor 결과를 재사용했다. plan·todo와 조작 기준에 상시 원칙을 기록했다.
- A의 구형 균형 HUD 인스턴스를 최신 PerformanceHud 프리팹으로 다시 배치하고 기존 어댑터를 연결했다. dev의 BalanceRiskBlink·BalanceZoneColors와 HudLayout 배치를 사용한다. 원본 어댑터/제작 도구도 같은 기존 공통 표시 기능을 사용하도록 보수했다. 새 이동 스크립트는 없다.
- A의 기존 톱 외형을 제거하고 최신 TightropeArtKit/HorizontalSaw로 함정9개·휴대 날2개·공유 발사 템플릿1개를 교체했다. 가로 톱 원본의 XZ면을 기존 수학 판정 XY면으로 회전하고 크기를 맞췄다. 최초 크기 검증에서 회전 후 비균등 축 배율 오류를 발견해12개 모두 수정, 재개방9개 전부 지름2.2m/두께0.28m 판정과 일치했다.
- WalkingRope 정본의 메시/재질로 로프44모듈을 갱신하고 기존 모듈의 중심·외곽 크기를 보존했다. 최신 dev의 기물·캐릭터·공용 시스템 참조는 이미 포함된 상태이며 공유 원본 자산을 지우지 않고 교체된 A 씬의 구형 외형만 정리했다. B/C 제작은 재개하지 않았다.
- 사용자 후속 지시대로 화염은 현재 씬의 FX_Flame_Thrower_01을 유지했다. 14개 분사 구역을 EditMode 입자 시뮬레이션으로 측정, 깊이가3.58m로 피해구역5m보다 짧은2곳만 약1.4배 늘렸다. 나머지12곳은 충분한 표시 범위여서 크기를 유지했다. 화염 추격/피해 시각·구역 로직은 바꾸지 않았다.
- 추가 이펙트 요청: 최신 톱 프리팹에 포함된 Sparks12개와 SawSpark 정본을 함께 적용했다. 거리당9개 방출·자동 시작/반복 설정을 보존하고 커진 외형에 맞춰10곳 입자 크기를 늘렸다. 기존 발사2·배선 절단24·접촉1 이펙트도 같은 정본 재질을 쓰며 launchFlash/cutSparks 참조를 확인했다. 총39개 금속 이펙트 연결 확인, 이벤트 이펙트27개는 자동 시작하지 않으며 원래 발사/절단 호출을 따른다. 화염 에셋은 교체하지 않았다. 근거 effects.json.
- 저장 후 재개방: missing scripts0·dirty=false, 최신 HUD 참조/공통 표시 기능 각각1개, 최신 로프44·톱9 크기 일치. 원본 기믹 직렬화92개와 BoxCollider672개가 변경 전 사본과 동일하다. 기존 이동3m/s·달리기2.0 유지. 별도 EditMode HUD 배치3/3·톱 외형7/7, 합계10/10 PASS. Play/자동 입력/commit/push 없음.
- 근거는 `.harness/local/dev-assets/`의 before.unity, applied.json, verified.json, gameplay-preserved.json, scene-saw.png. 최초 UI 활성화 시도는 CUA 시간 초과/창 미노출로 실패했고 Pipeline scene screenshot으로 실제 씬 톱 외형을 확인했다. 직접 카메라/PreviewRenderUtility의 빈 미리보기는 증거로 사용하지 않았다. 원래 사용자 변경과 이전 백업/stash를 보존했다.
- 최종 check-work(-Base HEAD -NoRecord) 종료0: 안전성/문서·규칙/컴파일 PASS, 기존 문서 길이 경고35/hard0, 자체 테스트는 중복 선택하지 않아 SKIPPED(별도10/10 결과 사용). 추가 이펙트 보정 후 check-work도 종료0: 안전성/문서·규칙/컴파일 PASS, 자체 테스트 SKIPPED(기존10/10 재사용). 실제 플레이 손맛·전체 성능은 미검증이며 A 편집 모드로 유지한다.

이전 작업 기록은 [History/016-before-dev-reuse-refresh.md](History/016-before-dev-reuse-refresh.md)에 원문 보존했다. 이동 전용 코드 제거와 최신 공통 이동3/달리기2 설정, Play 중지 지시는 계속 유효하다.
