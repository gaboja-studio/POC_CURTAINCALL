# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-12 원인(정적 분석, 에디터 실측 전): ① 포획이 발끝 3D 거리 1.15m라 트램펄린 표면(0.5m)·반동 공중 플레이어 불가 ② 시야각이 3D라 머리 위 플레이어를 시야 밖으로 판단 ③ 쫓는 대상만 포획 → 탑승자를 쫓는 동안 운반자 무시 ④ 원격 접촉은 Run 동작 필수 → 막힌 길 끝 노려보기 중 거부 ⑤ 리허설 길안내 임시 NavMesh가 추격자와 같은 에이전트(0)로 남아 경로에 섞임. 트램펄린 콜라이더(높이 0.5m)는 시야선(1~1.5m)을 가리지 않음.
- 2026-11-12 1~5 구현: `CaptureBelow/Above`(0.5/0.75) 추가·수평 포획, 수평 시야각, 대상 아닌 플레이어 포획(`CaptureInReach`, 접촉 기록에서 대상 제거), 추격 판정 Chase 기준, 길안내 NavMesh 전용 에이전트.
- 2026-11-12 테스트: EditMode Pursuit 69 중 68 PASS. PlayMode Pursuit 23 중 17 PASS. 실패는 dev 기준(stash 후 재컴파일)과 동일 → 기존 실패: `UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences`(배치 size 0↔1), 깨어남·NavMesh 진입 대기 5건, 방향 신호 각도 1건.
- 2026-11-12 주의: 테스트 실행 중 에디터가 폰트 SDF 2개·escape-labels 캐시를 갱신함(이번 작업 아님, 손대지 않음). `stash@{0} task117-wip`에 같은 내용 보관.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-12 6 오르기 구현: `PursuitClimbLinks`(실행 중 NavMesh 가장자리 → 0.6m~`ClimbMaxHeight` 단차, 위 1.2m 여유·난간 넘기, 1.5m 간격, 걸어서 가까이 이어지면 제외)를 호스트가 한 번 `NavMesh.AddLink`(Jump 구역)로 추가. 씬·오브젝트·NavMesh 에셋 수정 없음. `ClimbFromStage`(3) 이상에서만 `agent.areaMask`에 포함. `PursuitActor.Climb`: 자동 통과 끄고 오르기(수직→넘기 1.1초)·뛰어내리기(0.5초) 직접 이동, 동작 `Climb`(전용 클립 없으면 Grab), 오르는 동안 포획 없음. 낮은 StepLink도 같은 경로로 걸어서 통과.
- 2026-11-12 실측(WaitingRoom.unity NavMesh, 삼각형 3518): 후보 291 → 링크 86, 생성 30ms(호스트, 휴식 시작 첫 틱 1회). 대상: 기계·드럼통 더미·책상·관람석 단·트렁크·컨테이너·연습 링 데크 등.
- 2026-11-12 복제본 수정: `motion >= Grab`을 포획으로 읽던 스냅샷 수신을 Grab/Tear만으로(Climb 추가 대비).
- 2026-11-12 테스트: EditMode Pursuit 72 중 71 PASS(신규 오르기 3·접촉 2 포함, 실패 1은 dev 기존). PlayMode Pursuit 23 중 17 PASS, 실패 6은 dev 기준과 동일. check-work 컴파일 PASS. 오르기 실제 이동·애니메이션은 사람 플레이 확인 필요(자동 PlayMode 헬퍼가 dev에서 깨져 있음).
- 다음: 사람 확인 체크리스트(todo). 아래 후속 요청에서 Grab 대체를 전용 애니메이션으로 교체.
- 2026-11-12 후속 요청: Blender MCP 우선, 실제 추격자 모델 기반 4/6방향 원화 선행 후 전종 오르기 애니메이션 제작. 에디터 미저장 씬 없음 확인 후 정상 종료, origin/dev fetch/반영 결과 Already up to date. 메인 fix 브랜치 유지, private 에셋은 `resource/pursuit-climb-animation` 생성(기존 detached HEAD 기준, 기존 파일 청결).
- 2026-11-12 Blender: 실제 프리팹 10종의 V7 메시·UV·가중치·재질을 추출하여 원본 V2 리그에 결합. 정적 키포즈 도해 원화 30장(오르기4/6방향+내려오기4방향 각10종)을 액션0개 상태에서 먼저 작성, manifest 해시 이후 30fps 본 액션20개 제작. 편집 원본은 private `Pursuit/Climbing/Source~/pursuit-climbing.blend`, 10종 검토용 복제본도 포함.
- 2026-11-12 Unity: 전용 importer로 모델별 Legacy 클립20개를 기존 프리팹10종에 추가. 원본 FBX/메시/재질/기존7클립 불변, 본 변환 최대오차 약0.000030m. bounds는 기존 prefab 직렬화 기준과 새 동작 합집합으로 보존. Animation.AddClip 별칭은 런타임 Initialize용이며 에디터 참조 저장은 AnimationUtility.SetAnimationClips 사용.
- 2026-11-12 런타임: enum 끝에 Drop 추가(기존 값 보존), Climb/Drop 시간은 GameSettings에 맞춰 speed 조절·ClampForever, Grab 폴백 제거. 오르내리는 중 포획과 Chest 시선 덧회전 차단. 기존 ClimbPose 경로/시간은 변경하지 않음.
- 2026-11-12 검토: Blender 프레임 시퀀스 및 Unity 실제 3종(M02/F05/F06) 클립+1.2m 검토 발판 합성 확인. PreviewScene에서 즉시 Render 시 스킨 갱신이 늦는 문제는 BakeMesh 임시 복제 렌더로 확인. 실제 AI/2인 네트워크 플레이 녹화가 아니며, 지형별 손발 접점 IK·최종 미술 평가는 미검증. 대기실 씬/배치/NavMesh 변경 없음.

- 2026-11-12 검사 1차: 안전성=FAIL(신규 테스트 .meta 미생성); 후속 검사 SKIPPED. Unity Refresh로 .meta 생성 확인.
- 2026-11-12 검사 2차: 안전성/문서=PASS; 컴파일=FAIL 기록이나 실제 원인은 HUD 테스트 씬 미저장 guard(컴파일 실행 전 차단). 씬을 저장한 것이 아니라 SaveScene(saveAsCopy) 사본과 원본 비교: 차이197개 모두 RectTransform 자동 구동 축의 0 직렬화, 비구동 변경0 확인 후 미저장 표시만 해제. 원본 씬 불변. 신규 테스트는 PrefabContents 격리 씬으로 변경.
- 2026-11-12 최종 검사: 안전성/문서·규칙/컴파일/공통 설정 PASS. 문서 길이 WARN63·hard0. 이어서 EditMode `PursuitClimbAnimationTests` 23/23 PASS, 실패/생략0(10종 바인딩·저장 클립 전체 샘플 본 좌표·호스트/원격 상태/시간·enum 호환). 실제 AI/2인 플레이 테스트가 아니며 기존 전체 Pursuit 실패7건을 재판정하지 않음.
- 2026-11-12 제출 전: 미저장 씬0·Assets/Scenes diff0 확인, Unity 정상 종료 후 origin/dev fetch/merge → Already up to date. 메인/private 변경 보존, commit/push 없음. 다음은 위 사람 확인 체크리스트와 높이별 손발 접점/연출 검토.

- 2026-11-12 IK 후속 요청: 정상 에디터 종료 후 origin/dev 재동기화(Already up to date), Unity 재실행. 같은 Task의 doctor 반복 안 함. 실제 표면을 탐색하는 PursuitClimbIk, Actor Update 복원/LateUpdate 적용/종료 해제, GameSettings 4값, 스냅샷 v6 통과 구간·진행률 동기화를 추가. 기존 지형·루트 경로·본 길이·원본 클립 불변.
- 2026-11-12 IK 초기 실패: EditMode Runner의 미저장 임시 씬에서 Additive NewScene 불가, SceneManager.CreateScene은 EditMode 불가 → Runner 임시 씬 내 fixture hierarchy만 생성/정리. 실제 검사15 중5 PASS/10 FAIL 원인은 손바닥 .025 로컬 보정이 FBX scale100 때문에2.5m가 된 것과 F03/F04 정상 왼발의 최대 가중치 .453을 결손으로 오판한 것. 월드 거리 환산·사지별 상대 임계값으로 수정 후15/15 PASS.
- 2026-11-12 임시 지형 PlayMode: 독립 fixture에서 실제 NavMesh/동적 링크 생성, 각성 후2단계 차단→3단계 AI 순찰 Climb/Drop→착지 복귀 및 호스트/복제본 IK 1/1 PASS. 실제 씬/배치 변경 및 실제2프로세스 네트워크 테스트는 아님.
- 2026-11-12 회귀 확대: 내려오기10종/지지체 비활성·파괴/도달 불가·복원 검사 추가. 첫52 중42 PASS/10 FAIL에서 착지 전 가중치를 낮추는 문제와 테스트의 이동 반대 방향 자세 확인. Drop 접점은 FinishClimb/Clear에서 해제하도록 수정하고 테스트도 실제 이동 방향으로 정렬. 리로드 중 verify-unity guard로 한 번 실행 차단된 것은 테스트 결과와 구분. 일부 정상 다리도 길이94%부터 가중치를 낮춰 완전 고정에서 빠짐(M01 착지 weight=.883 실측) → 실제 도달 한계99.5%~100%에서만 완화하도록 수정. 최종 EditMode PursuitClimb 52/52 PASS(애니메이션23+IK26+링크3), 실패/생략0.
- 2026-11-12 최종 회귀: 기존 PursuitSnapshotCodecTests 3/3 PASS, 변경 후 임시 지형 PlayMode 1/1 재PASS. 기존 전체 Pursuit 실패7건은 재실행/재판정하지 않음. private Review/unity-climb-ik-contact-sheet.png에 M02/F03/F05 Climb .15/.22/.8, Drop .9 정적 IK 검토 저장. 실제 콜라이더 fixture와 BakeMesh 사용, 검토 오브젝트 정리 후 원래 HUD 씬 dirty=false 확인. 손가락별 접지/전신 충돌/최종 미술 및 실2클라이언트 지연 검증은 남김.
- 2026-11-12 IK 제출 전: verify-knowledge PASS, check-work 안전성/문서·규칙/컴파일/공통 설정 PASS(자동 테스트는 위 별도56개 결과). 문서 길이 기존 WARN63·hard0, private 미커밋 변경 안내 유지. git diff --check 양 저장소 오류0, Assets/Scenes diff0. Unity 정상 종료 후 origin/dev fetch/merge --no-commit → Already up to date, Unity 재실행(PID47912). commit/push 없음, 기존 escape-labels 캐시 변경 보존.

- 2026-11-12 사용자 보고: 3·4단계에서도 드럼통 위 플레이어에게 못 올라옴. 1~4단계 언제든 오르도록 요청.
- 2026-11-12 원인(실제 WaitingRoom.unity 열람·저장 없음): 추격 NavMesh 반경 0.5m라 상자·드럼통·침대·책상 등 좁은 윗면에 NavMesh 자체가 없어 링크 0. NavMesh 있는 기계류(2.3m)는 1.5m 제한 초과. 스크린샷 드럼통은 Tunnel/WallDressing/SM_Barrel 1.94m. 앞선 임시 지형 검사는 반경 0.24m라 이 조건을 재현하지 못했다.
- 2026-11-12 수정: `PursuitDirectClimb`(충돌체 윗면·가장자리·머리 공간 검사, 막힌 쪽은 다른 방향 가장자리로 우회) + Actor 직접 오르기/윗면 추격(Chase 유지·포획 가능)/뛰어내려 NavMesh 복귀. 1.5m 초과는 오르는 시간 비례 증가. GameSettings ClimbFromStage 3→1, ClimbMaxHeight 1.5→2.5(에디터 API 저장). 씬·NavMesh 에셋 변경 없음.
- 2026-11-12 실제 대기실 측정: 링크 86→122, 서 있을 수 있는 윗면 2644지점 중 2312(87%) 도달(이전 링크만은 일부 높은 소품 제외 대부분 불가). 상자·침대·책상·사물함·트렁크·드럼통 전부 가능. 남은 실패는 벽·천장·지붕판·문틀·난간 윗면, 천장 근처 배관(머리 공간 없음). 첫 측정에서 우회 바닥을 윗면 높이에서 찾던 버그 발견·수정.
- 2026-11-12 테스트: EditMode Pursuit 149 중 148 PASS(신규 직접 오르기 7 포함). 실패 1은 dev 기존 `UnifiedWaitingRoom_PreservesPursuitAuthoringAndSceneReferences`(배치 size 0↔1). PlayMode 오르기 2/2 PASS: 1단계 링크 오르내리기·복제본 IK, NavMesh 없는 1.94m 윗면 직접 오르기→윗면 추격→뛰어내려 복귀. 첫 실행 실패는 테스트 배치가 1단계 시야(5m·100°) 밖이던 것으로 fixture 수정. 실제 2인 네트워크 플레이 아님.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-12 사용자 확인: 오르기·앉아 숨기 어느 정도 검증됨. 질문: 둥근 지형(지하실 넘어진 원기둥)을 오를 때 허공을 잡는 현상.
- 2026-11-12 원인 측정(WaitingRoom 열람만, 저장 없음): 해당 물체는 B1_MachineRoom/Plant/BoilerShell 2개(지름 2.3m, CapsuleCollider, 윗면 NavMesh 폭 1.5m). 실행 중 링크는 바닥(축에서 1.40m)→윗면(0.72m, 2.42m 높이). 손 접점은 콜라이더 곡면(축 0.87m, 법선 y 0.65)에 정상 생성. 콜라이더 문제는 아님. 오르기 경로가 몸을 바깥 자리에서 수직으로 윗면 높이까지(구간 60%) 올린 뒤 수평 이동하고, 손 고정 창은 0.32~0.52에 해제된다. 보일러는 0.17~0.31만 손이 닿고 0.34 이후 손이 표면에서 0.16→0.84m 떨어진 채 몸만 올라간다. 같은 높이 상자(벽 0.55m)도 0.38 이후 0.4→0.9m 떨어진다. 곡면은 잡는 점이 더 낮고 안쪽이라 닿는 구간이 짧아 더 잘 보인다. 수정 방향: 오르는 동안 몸을 가장자리 쪽으로 붙이고(곡면은 충돌 여유 검사), 몸이 윗면에 올라설 때까지 손 고정 유지(도달 거리 기준 해제).
- 2026-11-12 사용자 요청: 앉은 플레이어가 장애물에 몸 80%(설정) 이상 가려지면 추격자에게 걸리지 않음.
- 2026-11-12 구현: `PursuitDirector.HasVisualContact`에서 앉은 PlayerMover(원격은 소유자 동기화 앉음 상태·캡슐)를 3×5 지점 가림 비율(`OccludedFraction`)로 판정. GameSettings `CrouchHiddenCoverage` 0.8 추가·에디터 API 저장(0이면 끔). 서 있으면 기존 한 점 판정. 투시(SeeThroughWalls)·최인접 단계는 단계 규칙 우선이라 현재 설정에서는 1·2단계에서만 숨을 수 있다. 눈 표시는 호스트 추격 대상 기준이라 변경 불필요.
- 2026-11-12 테스트: PlayMode 앉아 숨기 1/1 PASS(가림 없음 보임, 전체 가림 숨음, 서면 보임, 부분 67% 보임→설정 60%면 숨음, 0이면 기존 판정, 투시 단계 보임). EditMode Pursuit 149 중 148(기존 1 실패 동일). PlayMode Pursuit 전체 32 중 24: 실제 대기실 기반 기존 실패 7(깨어남·NavMesh 대기 5, 방향 신호 각도 1, 원형 로비 카메라 천장 1)과 그 뒤 실행된 직접 오르기 1 실패. 직접 오르기 실패는 앞선 실패 테스트가 메모리 GameSettings 단계값(1단계 추격 0.1m/s 등)을 바꾼 채 남긴 오염 때문. 도메인 재로드로 디스크 값 복구 후 오르기 2/2·앉아 숨기 1/1 재PASS, 에셋 dirty=false·디스크 diff는 추가 필드만 확인.

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-12 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
