# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.
# 2026-11-02 착수

- Confluence 추격 시스템 live doc 32473089 v1을 조회하여 plan에 요구사항 정리.
- origin/dev fetch 성공, HEAD...origin/dev = 0/0. 병합/체크아웃 불필요하여 열린 에디터를 종료하지 않음.
- doctor 1회: FAIL. 샌드박스에서 _ThirdParty/Unity 설치 탐색 실패. 권한을 넓힌 읽기 전용 확인 결과 private submodule 149108fa와 지정 FBX 존재, 현재 worktree Unity 6000.3.25f1 ready 확인. CLI 최신 beta.15.
- 기존 Task ID 중복 6종, 미등록 원격 브랜치, skill-links 없음, gh 미로그인은 기존 환경 상태. 이번 기능 구현/Task 생성에는 직접 영향 없음.
- 기존 변경 3개(item-interactions.blend, reference.webp, asset-helper-mac.zip)를 보존한다. LFS 필터/서브모듈 상태 조회는 제한된 샌드박스에서 실패하므로 상태 확인 시 주의.
- scaffold-task로 Task-20261102-094 생성. start-work 스크립트는 기존 중첩 브랜치 feat/waiting-room/pursuit-system를 정규식으로 거부함. new-task -UseCurrentBranch는 허용하며 실제 현재 브랜치/Task 일치 확인; 브랜치 이름 변경 없이 계속한다.
- 기존 RunState.Tick은 0초에 EndRest, RestTimeoutCatch는 즉시 일괄 포획한다. 실제 추격 상태로 바꾸는 연결이 필요하다.
- 사용자 확정: 추격 후 안전 구역도 위험. 생존자 전원 공연장/탈출로 집결로 장소 전환할 때만 종료, 잡힌 인원도 선택 목적지에 외팔로 이동.
- 추가 확정: 1~3일차 전원 포획 시 연출 후 다음 공연으로 이동. 4일차 아침도 탐색/추격/탈출 가능하며, 4일차 전원 포획 시 즉시 런 실패.
- Unity eval_file 첫 조회/씬 준비 명령이 5초 main-thread 응답 제한을 초과했다. 이후 ready 및 실제 결과를 읽기 전용 확인; 준비 스크립트는 완료되어 있었음. 원본/복제 SHA256 C63F43D893A896B96C9955468499E425C50A5049904E3F99DC12F22975D310F7 동일. 원본 GUID 0ce9037bd7c44c8b9d57785c26620388, 복제 GUID 9d8832c4cb77f9343a74c3dc35c1d3a3.
- 복제 씬이 active/clean, 런 설정·음성 허용 목록·전역 Build Settings 연결 확인. EditorBuildSettings API 변경은 메모리에만 남았으므로 AssetDatabase.SaveAssets 후 디스크 내용까지 확인했다.
- 마네킹 10종 모두 SkinnedMeshRenderer=0, Transform=1. 리깅이 필요하며 파생 에셋은 _ThirdParty에 둔다.
- 초기 제한된 git status에서 보인 LFS 파일 3개 수정은 권한을 넓힌 정상 LFS 상태 조회에서는 사라졌다. 해당 파일을 수정/되돌리는 작업을 하지 않았다.
- 남은 질문: 구출 여부/남길 팔/기존 부상 처리, 초기 배치·탐지 수치·1.2배 기준 속도. 해당 결정 전 게임플레이/모션 구현은 시작하지 않음.

## 규칙 확정 후 구현
- 사용자: M2 포획 구출 불가, 제안한 초기 수치 승인·단계별 설정 요구. 기존 공통 절단 순서/오른팔 외팔 정의 재사용.
- 재개 시 origin/dev fetch 성공, HEAD와 0/0. 열린 에디터 종료/동기화 변경 불필요. 이전 doctor는 반복하지 않음.
- RunState에 선택적 추격 만료 흐름 및 전송 enum 끝값 Pursuit=5 추가. 기존 모드는 유지, 실제 CampaignRun은 WaitingRoomSettings.Pursuit.Enabled를 읽는다. 추격 만료는 0초에서 RestTime을 유지하고 기존 즉시 포획을 생략한다. 4일차 데모 조기 종료는 추격 모드에서 사용하지 않는다.
- 호스트 Director/Actor AI, 시야·엄폐·순찰/추격, 단계별 수치/알림, NGO 주기 스냅샷(늦은 입장 포함), 상하체 방향·포획·절단·방 조명·관전·목적지 복귀 연결. 단계 알림은 호스트 문구/단계를 전송한다.
- Blender 5.2.3에서 별도 씬에 비파괴 파생 리그10개/각15bone 제작. _ThirdParty/curtaincall/Pursuit에 FBX10, 모션70, 프리팹10, NavMesh, Blender/스크립트 원본/검토 PNG 보관. Synty 원본 변경 없음.
- 복제 씬에 26개 방/연결부 영역·조명128개·탐색 영역11곳 각1체. 초기 NavMesh는 문/셔터/파괴벽과 단차로 단절됐으므로 동적 장애물 분리, 작은 체형 베이크, 벽/바닥 검증 단차 링크28개를 추가. 분리된 발판의 초기 스폰을 보정하여 열린 상태 기준 11곳 모두 로비 경로 완전 확인.
- 일반 C# ??가 Unity 내장 컴포넌트의 fake null을 통과해 NavMeshObstacle.Awake 오류를 냈다. 명시적 Unity null 검사로 수정, 실제 LAN 호스트 재검증에서 재발 없음.
- 탈출 집결은 잡힌 인원을 제외하지만 이동 명부에는 포함한다. 포획 인원의 먼 방 좌표는 목적지에 넘기지 않고 입구 내부 합류 위치/방향으로 대체한다.
- 테스트 초기 fixture에서 개발 시작 API의 공연 이름/세대 증가를 잘못 사용해 4개 실패; fixture 수정 후 최종 추격6/6 PASS. 기존 RestTimeRules 2/2 PASS, RunTimeLimit 16/16 PASS. 결과 JSON을 Task에 보관.
- 오프라인 실제 Player 프리팹 포획: 조작 잠금/첫 절단/RestTime-Pursuit 유지, 마지막 잃은 팔다리3개/공연 전환 확인.
- 별도 LAN 포트7797의 실제 NGO 호스트 플레이어(slot0) 검증: B1_Office 추격자가 약2.716m 이동해 포획, 첫 절단·조작 잠금, 연출 완료 뒤 State=Normal/LostCount=3/IsSpawned=true로 공연 복귀. host-*.json 보관. 검증 호스트 종료, 기존 Dev Mode 개인 설정 복원.
- 다인 수신 화면·동시 포획·늦은 접속·포획 후 실제 탈출 씬 이동은 아직 실기 확인하지 않았다. manual-verification.md에 수동 확인 항목/조작 경로 기록.
- 착수 이후 escape-labels.asset과 Vivox Settings.json 변경이 보였다. 이 기능의 편집 대상으로 삼거나 되돌리지 않고 보존한다. commit/push 없음.
- 구현 묶음 최종 check-work: 안전성/문서·규칙/컴파일 PASS, 에디터 ready·열린 씬1개·미저장0. 이 명령의 자동 테스트는 선택하지 않았으며, 별도 선택 실행한 24개 결과는 모두 PASS. 기존 문서 길이 경고43건/hard0, private 에셋 미커밋 경고는 기록하고 저장·push하지 않음. 원본 WaitingRoomBase SHA256은 착수 시와 동일.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 모션/복제 씬 UI 플레이 피드백 수정
- 현재 작업의 기존 doctor는 반복하지 않음. origin/dev fetch 후 이 워크트리 에디터의 미저장 0 확인·종료, 4개 커밋을 fast-forward(37fa8fc4)하고 _ThirdParty도 해당 포인터로 갱신. 기존 변경을 보존하고 이 프로젝트만 다시 열었다.
- 애니메이션을 수정하기 전에 Display/Twitch/Awaken/Walk/Run/Grab/Tear의 정면·후면·좌·우 × 4키 원화 7장 제작. 포획 원화의 잘못 중복된 측면을 보정. 모든 원화/프롬프트/리그/FBX/클립/검토 이미지는 _ThirdParty/curtaincall/Pursuit 아래 보관.
- Blender MCP: 모델별 전시 자세를 기준으로 관절 위치 재배치, 결손 부위 제외. surface heat 가중치에 고신뢰 시드·메시 연결 거리 기반 손/전완과 몸통 분리를 추가했다. 단순 최근접/희소 시드와 heat 단독은 손이 허리에 가까운 모델에서 몸통까지 당기는 문제가 있어 폐기했다. Source/rebuild-rigs-v2.py와 pursuit-rigs-v2.blend가 최종 원본.
- PursuitMotionBuilder: 수입 뼈 local Euler 가정 제거, 캐릭터 공간 회전·발 목표/무릎 앞 방향 IK·골반 높이·각성 local 보간. 70클립을 GUID 유지 갱신. Actor는 포획 시 피해자 방향을 바라보고 상체 추적은 캐릭터 up 축으로 제한한다.
- MotionReview: 10종×7클립×61시점(4,270 자세)에서 뼈 길이 오차 최대 0.000001m, 무릎 앞 방향 최소 +0.011465m. Unity BakeMesh 결과로 각 모션 4방향×4키 및 10종 비교 시트를 렌더/육안 확인했다. BakeMesh 결과에 renderer scale을 이중 적용하던 검토 도구도 수정했다.
- UI 원인: 고정 WaitingRoomBase 이름/경로 판정. CommonMenuUI/InventoryUI가 CurrentWaitingRoom binding의 실제 씬을 사용하도록 보완.
- 실제 Start → LAN 호스트 → Dev Start 2일차 아침 및 동일 지점 재진입: ESC 열기/닫기, 입력 잠금/해제, 슬롯 2개, 메뉴 인스턴스 1개 확인. ui-play-report/ui-reentry-report와 _ThirdParty Review/ui-*.png 보관. 개인 Dev Mode 설정 복원.
- UI 자동 테스트의 초기 실행은 잘못된 Lobby 대기, 중복 Runner 상태, 실행 중 재컴파일로 완료 결과를 얻지 못했다. Lobby 대기 제거 후 컴파일이 끝난 정지 상태에서 단독 재실행: **1/1 PASS, 36.71초**, ui-tests-final.json. 실패/중단 시도를 PASS로 계산하지 않는다.
- 재컴파일/종료 과정에서 발견된 PursuitRoom 조명 배열과 밝기 캐시 길이 불일치는 포획 시 조명 참조 스냅샷/NonSerialized 캐시로 보완했다.
- 원본 WaitingRoomBase와 원본 Synty 모델은 보존. 작업 시작 때 보인 Pretendard 폰트·escape-labels·Vivox 개인/기존 변경 보존. commit/push 없음. 다인 실기와 사람의 최종 모션/공포감 평가는 기존 미완료 항목.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 피드백 수정 최종 check-work 1회: 안전성/문서·규칙/컴파일 PASS, 자동 테스트 옵션 SKIPPED(별도 UI 회귀 1/1 PASS). 원본 씬 SHA256 동일. 조명 목록 변경 후 종료 시 원래 조명 밝기 복구/새 조명 보존을 별도 검증. 에디터 Start 정지·미저장 0·Dev 개인 설정 복원. 기존 ThirdParty SeasideStudios 셰이더 2개 변경도 보존했다.

