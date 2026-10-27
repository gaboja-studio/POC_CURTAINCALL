# Log

- 2026-10-27 작업 세팅: 사용자 지정 기존 브랜치 `feat/waiting-room/design-rooms`에 Task-075를 연결했다. 담당자는 팀 사실 문서의 계정 대응에 따라 @gyumin9048-ctrl(표시 이름 mingyu)로 기록했다. 실제 씬·에셋 수정은 하지 않았다. 다음 할 일은 현재 대기실 조사와 방별 구조·배치·디자인 계획 구체화다.
- 초기 doctor(-SkipEditor): FAIL — 현 브랜치에 대응하는 Active Task 없음. 이번 Task 생성으로 누락을 보완했다. unity-cli 문서 버전 차이 경고는 실제 CLI 사용 시 확인한다. sandbox에서 나온 gh 로그인 경고는 자격 증명 접근 가능 환경에서 MoHoDu 로그인 정상으로 확인했다. 동일 작업의 doctor는 반복하지 않았다.
- 사용자 요청에 따라 문서 검사 후 현 브랜치를 커밋하고 origin에 푸시한다. dev 반영은 별도 PR 검토다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 조감도 A 계획 요청: 기존 맵 삭제 금지·비활성화 보존, 사용자 지정 7개 공간 복제, 나머지 신규 제작, 단계별 진행을 plan/todo에 반영했다. 실제 씬 텍스트에서 복제 후보 7개 이름을 확인했다. Editor에서 계층·치수·기능 참조는 아직 확인하지 않았다. 이미지의 조감도 표현과 신규 기능 요구를 구분했다. 현재 요청에 따라 계획만 작성했으며 씬·에셋은 변경하지 않았다.
- 이번 작업 doctor(-SkipEditor): FAIL — unity CLI 없음. skill-links 없음·gh 로그인 필요 경고. 문서 계획에는 영향 없음. 실제 Editor 작업 시 사용 가능한 조작 경로 확인 필요. 동일 작업에서 반복 실행하지 않는다.
- 다음 작업: plan의 1단계 원본·기능 조사 및 보존 준비부터 진행. 6·7번 백스테이지 경계 유지, 복제 참조·단일 시스템 중복 주의. commit/push하지 않음.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 Jira 업로드 요청: CC-132(8공간 그레이박스와 공연 왕복)의 양식을 확인하고 새 CC-163에 전체 9단계, 현재 1단계 체크리스트, 완료 기준, 공동 확인 3개, 결과물·이슈를 등록했다. 기존 완료 카드는 변경하지 않았다. 담당 MG seo, 상태 할 일. https://hrjoo122770.atlassian.net/browse/CC-163
- Unity 연결: PATH의 unity가 없어도 Hub 번들 CLI C:/Program Files/Unity Hub/resources/unity.exe(1.0.0-beta.12) 사용 가능. 일반 권한 자동 탐색은 프로세스 확인 경고, 승인된 확장 권한 status도 STATUS_NO_INSTANCES였지만 프로젝트 지정 editor_status / list_open_scenes 명령은 정상 응답했다. 설치 Pipeline 0.8.0-exp.1은 editor_status처럼 밑줄 명령을 사용한다. 최신 Skill의 editor status·--pid 형식은 이 버전과 맞지 않아 읽기 호출 실패 후 설치 버전 형식으로 수정했다.
- 실제 Editor 응답: C:/GD38/POC_CURTAINCALL / Unity 6000.3.25f1 / ready / compiling=false / domainReloadInProgress=false / playMode=stopped. 열린 씬 Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity, isDirty=false. 맵 씬 열기·편집·저장·플레이 진입은 수행하지 않았다.
- CLI beta.14 업데이트 알림 확인. 업데이트 명령은 사용자 선택이 필요하므로 실행하지 않았다. 이번 요청 범위의 연결 확인은 완료. doctor는 같은 작업에서 반복하지 않았다. commit/push하지 않음.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 사용자 정정: 실행·계획 기준 카드는 CC-159다. 본문을 CC-132 양식으로 갱신하고 진행 중으로 전환했다. CC-163은 초기 등록 참고/이관 안내만 남겼으며 별도 구현 작업으로 쓰지 않는다. CC-159는 KAN board의 활성 보드 조회에서 확인했다.
- 사용자 정정: 복제 원본은 WaitingRoomBase.unity의 WaitingRoomGreybox다. 이전 Levels/waiting_room 프로토타입을 잘못 선택해 보존 준비했으나 Editor에서 제 변경만 복원·저장했고 해당 파일 Git diff 0을 확인했다. 원본 대상으로 다시 조사·준비했다.
- 1단계 실제 완료: WaitingRoomGreybox 루트 이름·계층·원본 ID를 유지하고 루트만 비활성화. WaitingRoom_LayoutA/LayoutOrigin 신규 생성. 원본 6,035개, Renderer 4,332개, Collider 579개. 변경 전후 모든 원본 오브젝트의 ID·이름·하위 활성 설정·월드 변환·컴포넌트 직렬화 문자열 동일(의도한 루트 activeSelf 제외). 공용 바깥 루트 활성값 전부 동일. 원본 활성화 시 Collider 354개 복원, 다시 비활성화 후 활성 계층 Collider 0개 확인.
- 7공간은 S01_Lobby, S02_Dormitory, S02_Dormitory/Restroom, S03_Shop, S04_PropRoom, S06_StageCorridor_Temp, S07_StageEntrance_Temp. Mood/공용 Walls와 기능용 WaitingRoomGameplay/NightSchedule/ExplorationTimer에 별도 배치가 있어 단계별 복제·참조 갱신 필요. 상세는 stage-01-report.md.
- 이번 단계는 새 바닥·스폰 전의 배치 준비다. Play Mode/공동 플레이·공연 왕복은 미실행. 원본 공간 종속 기능은 원본과 함께 비활성 상태로 보존되며 루트 밖 서비스·UI·카메라 활성 설정은 유지했다. 다음은 2단계 로비 및 관련 Mood/Walls 복제·배치다.
- start-work 실행은 기존 지정 브랜치 feat/waiting-room/design-rooms가 단일 경로 전용 정규식에 맞지 않아 실패했다. 현재 브랜치와 plan Branch가 일치하고 이 브랜치 사용이 이미 명시돼 있어 브랜치 변경·스크립트 수정 없이 작업했다.
- eval 뒤 SaveScene을 코드 내부에서 호출해도 명령 종료 과정에 씬이 다시 dirty가 될 수 있었다. 실제 최종 저장은 별도 save_scene 명령으로 수행했고 list_open_scenes에서 WaitingRoomBase isDirty=false 확인. 씬 YAML 직접 편집 없음. commit/push 없음.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 1단계 검사 마무리: 첫 검사에서 todo EOF 빈 줄로 문서 검사 실패, 공백 정리 후 재검사 안전성·문서/규칙 PASS. C# 변경이 없는 씬 준비이므로 -SkipUnity로 컴파일 생략, 자동 테스트 선택 안 함. Editor 원본 보존·복원·저장 확인은 별도로 완료했다. CC-159 체크리스트와 결과물에 실제 결과 반영. 플레이 가능/전체 맵 완성을 뜻하지 않는다.

## 2026-10-27 — 2단계 로비 복제 완료
- WaitingRoomGreybox 기반 기존 로비와 분산 아트·벽·조명·스폰·일정판/전용 카메라를 복제했다. 새 로비 668개, 출입 예정 위치 9곳, 휴게실은 위치만 예약. 상세 stage-02-report.md와 복제 목록 참고.
- 최초 실행은 일정판의 원본 ScheduleCloseUp 참조를 탐지해 자동 롤백했다. 해당 카메라까지 복제한 후 성공. 원본 내용·ID 보존, 저장 파일의 기존 객체 삭제 0개 확인.
- Play Mode 실제 캐릭터 크기의 이동 충돌 검사: 25/25 경유점, 1,433프레임, 낙하/막힘 없음. 스폰 4곳·차단 9곳 확인. 컴파일된 Validate 실행 성공, compilationFailed=false, 실행 콘솔 오류 0개.
- 공용 UI의 Pretendard 한글 누락 경고 존재. 캡처로 자동 확장된 SemiBold 글꼴 아틀라스는 Editor에서 기존 1×1 상태로 되돌려 파일 차이 0 확인. 실제 네트워크 접속·카메라 추적·일정판 상호작용·4인 검증은 미실행.
- 다음: 3단계 첫 공간 상점 복제와 구매/배송 연결. commit/push 하지 않음.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- CC-159에 2단계 체크리스트·검증 결과·미검증 항목·다음 상점 작업 반영 확인. 최종 WaitingRoomBase isDirty=false. 원본 및 별도 프로토타입 보존.

## 2026-10-27 — 1·2단계 고도화와 3단계 상점
- 사용자 요청에 따라 2단계 결과를 커밋했다(d04bab5, push 안 함).
- 1차 고도화(23px=1m, 체크 타일, 원통형 상점 부스)는 사용자 피드백으로 폐기: 공간이 좁고 균일함, 바닥은 녹슨·깨진 콘크리트, 상점은 원본 기둥 그대로. Task-060 Layout/global.md·lobby.md를 톤앤매너 정본으로 확인했다.
- 기준 좌표를 18px=1m로 바꿨다(layout-a-plan.md). 로비를 29.4×28m·천장 13m 폐공장 홀로 다시 만들었다. 손상 콘크리트 바닥과 녹·균열·웅덩이·슬래브 이음매·깨진 슬래브, 처진 천막·전구 줄, 캣워크·트러스·배관·내부 기둥을 비대칭으로 배치했고 조명은 15개다. 상세: stage-02-report.md.
- 도구 WaitingRoomLayoutA.cs로 교체하고 WaitingRoomLayoutAStage02.cs를 삭제했다. 생성 에셋은 Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/에 있고 1차 시도 에셋(체크 타일·부스 메시)은 삭제했다.
- 3단계 상점: 원본 S03_Shop 기둥을 그대로 복제해 (-10.8,-6)에 180° 돌려 배치했다. 수화기 위 램프 1개, WorldLabels.delivery를 재연결했다. 상세: stage-03-report.md.
- 발견·수정: 비활성 루트의 GetComponentsInChildren()이 꺼진 원본을 반환해 WaitingRoomShopUI·WaitingRoomScheduleUI가 원본 수화기·일정판에 묶였다. 활성 컴포넌트만 고르게 수정했다(UI 공용 코드 겹침 알림 필요). 함정 기록: Harness/Project/Pitfalls/inactive-root-component-scan.md.
- 데칼 함정: URP Lit 투명의 Preserve Specular가 켜져 있으면 투명 영역에 반사가 남아 사각형으로 보인다. 도구에서 끄도록 했다.
- 검증: BuildLobby/BuildShop/Validate PASS(원본 6,035개 직렬화 동일, 스폰·출입구 9곳·수화기·배송구 앞 여유와 도달 거리). Play Mode CharacterController(r0.3,h1.5) 이동 30/30, 2,308프레임, 발 높이 0.080~0.100, 막힘·낙하 없음. Play 중 콘솔 오류 0개. 씬 저장 isDirty=false.
- Play Mode 중 Pretendard-SemiBold SDF.asset이 동적 폰트로 저절로 바뀌었다(알려진 함정). 작업 결과가 아니므로 커밋 대상에서 제외한다.
- 미검증: 구매·일정판 UI 실제 상호작용, 배송 낙하, 네트워크 카메라 추적, 4인 접속, 공연 왕복, 성능 계측. 다음: 3단계 소품실(04) → 숙소·화장실(02). 최종 공간감·톤은 담당자가 확인한다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-27 — 3단계 소품실(04)
- 원본 소품실(16×8.5m)은 조감도 자리(실내 11.2×9.6m)에 통째로 들어가지 않는다. 그래서 의상·4단 선반·분장 세 구역을 묶음 단위로 복제해 재배치했다(`BuildPropRoom`, 495개, 조명 9개). 천장은 5.6m로 낮춰 로비 홀과 대비를 줬다. 상세: stage-03-proproom-report.md.
- 기능 연결: 물려줄 4단 선반(HandoverShelfView)과 전용 스폿을 함께 이동했다. UI/WorldLabels.shelf를 재연결하고, Zone_PropRoom 카메라 영역을 새 방 크기로 맞췄다. 로비 Portal_04의 임시 커튼·셔터를 제거했고, 로비를 재빌드해도 열린 상태가 유지된다.
- 수정 1: 2.4m 벽 띠가 문 높이보다 낮아 문을 가로질렀다. 문 위치에서 끊었다.
- 수정 2: 로비 바닥(X 6.4까지)과 소품실 바닥(X 6.8부터) 사이 문 아래에 0.4m 충돌체 틈이 있었다. 첫 이동 검사에서 발 높이가 0.010까지 내려가 발견했다. 소품실 바닥 충돌체를 X 6.3까지 늘리고, 검증에 문 아래 바닥 레이캐스트를 추가했다. 이후 방에서도 문 두께만큼 바닥을 겹쳐야 한다.
- 검증: BuildPropRoom/Validate PASS(원본 직렬화 동일, 활성 선반 1개, WorldLabels 연결, 입구 개방, 입구·문 아래·선반 양쪽·의상/동쪽 통로 여유). Play Mode 이동 41/41(소품실 순환 + 기존 로비 경로), 2,974프레임, 발 높이 0.080~0.100, 게임 콘솔 오류 0개. 씬 저장 isDirty=false.
- 콘솔의 `editor status` 명령 없음 오류는 check-work가 에디터를 옛 명령 형식으로 호출해 생긴 브리지 로그다(pipeline-command-drift). 게임 오류가 아니다.
- 미검증: 4단 선반에 기물 올리기·세대 교체 유지, 방 카메라 전환 체감, 네트워크. 다음: 숙소·화장실(02).

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-27 — 3단계 숙소·화장실(02)
- 소품실 작업 커밋(d27f232, push 안 함). 숙소·화장실을 `BuildDormitory`로 생성(336개, 조명 11개). 상세: stage-03-dormitory-report.md.
- 조감도 02 영역(깊이 6.7m)은 원본 침대·칸막이가 들어가지 않아 북쪽으로 깊이를 8m로 늘렸다(실내 X 3.6~17.8, Z 14.5~22.5, 천장 4.6m). 칸막이로 숙소/화장실을 나누고, 화장실은 숙소 안 칸막이 문으로 연결했다.
- 원본 내용(20×8.7m)은 X 0.72배 압축으로 배치했다. 소품 자체는 수정하지 않았다. 청록 타일 텍스처를 새로 만들었고(LA_TealTile), 원본 물웅덩이 메시는 검은 판이라 웅덩이 데칼로 바꿨다.
- 범용 `RoomShell`(직사각 방 셸, 얇은 칸막이 충돌)을 추가했다. 이후 방은 이 함수를 쓴다. 문 아래 바닥 충돌을 함께 덮는다.
- 검증: BuildDormitory/Validate PASS(원본 직렬화 동일, 02 입구 개방, 문 아래 바닥, 입구·침대 통로·칸막이 문 양쪽·화장실 가운데 여유). Play Mode 이동 52/52(숙소·화장실 순환 + 소품실 + 로비 경로), 3,439프레임, 발 높이 0.080~0.100. 씬 저장.
- 미검증: 방 카메라 전환 체감, 밤 셔터·출입 제한과의 관계, 네트워크. 3단계 완료. 다음: 4단계 무대 통로(06)·스테이지 입구(07).

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-27 — 4단계 무대 통로(06)·스테이지 입구(07)
- `BuildStageRoute`로 원본 백스테이지 터널 42m를 4구간 강체 이동해 06 통로(X 15.2~36.3, 21×8m)와 07 입구(X 36.5~42.8, 6.3×10m)를 만들었다. 커튼방 4번째 쌍은 뺐다. 상세: stage-04-report.md.
- 기능 연결: 집결 구역(StageEntranceGather)을 07 커튼 앞으로 옮기고 UI 연결 재지정. 밤 셔터(NightDoorLock)를 5m 06 문으로 넓혀 옮겼다. 그동안 새 맵에 없던 대기실 퇴장 감시(탐색 시작)를 로비·숙소·소품실 기준으로 복제했다.
- 캡처 확인 후 수정: 원본 웅덩이 메시 검은 판 → 데칼, 07 벽 소품이 벽에서 1m 떠 있던 것 → 넓은 벽으로 이동.
- 이동 검사 중 발견: 로비 2단계 `SM_MetalPlate1`이 눕지 않고 3.5×4m 판으로 서서 06 길목을 막았다. 도구 `LayPlate`로 고치고 씬은 철판 하나만 같은 값으로 변경.
- 검증: BuildStageRoute/Validate PASS(원본 동일, 기능 각 1개, 닫힌 셔터가 06 문 봉쇄, 퇴장 판정 안/밖, 서 있기 9지점). Play Mode 이동 25/25, 1,222프레임, 발 높이 0.020~0.074. 씬 저장.
- 커밋 제외: Pretendard-SemiBold SDF.asset(Play 자동 변경 함정), GB_Dark.mat(소수점 재저장), _ThirdParty V4_Clown_Nose.mat(이번 작업 아님). 되돌리기는 하지 않았다.
- 미검증: 실제 집결→공연 전환·첫 퇴장 타이머·밤 셔터 동작, 4인 네트워크, 카메라 전환 체감. 확인 필요: "무대 →" 화살표 방향, 5단계 이후 새 방의 대기실/백스테이지 구분.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
