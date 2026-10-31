# Log

- 이전 연결·침범 보수·원테이크 MPPM/성능 원문: [History/021](History/021-before-final-submission.md).

## 2026-10-31 전체 연속 플레이 재개 — 열쇠/암호 포함
- 사용자 최신 요청: 실제 Unity MPPM 주 에디터+3 클라이언트, 대기실의 도구 수집/열쇠/암호부터 마지막 A 탈출까지. 선행 퍼즐 준비 금지, 체크포인트/위치 강제 이동 금지.
- 작업 시작 doctor 1회: 기존 private 에셋 변경/중복 Task ID 실패 기록. Unity 종료 확인 후 origin/dev fetch, HEAD=origin/dev=7164c818, ff-only Already up to date. start-work helper는 기존 slash-depth 브랜치 이름을 거절했으나 fetch는 완료. 사용자 변경은 .harness/local/full-escape-20261031/before 및 before-private에 보존.
- 실제4에디터(PID34636/42260/27964/35984) LAN 연결. 처음 시나리오 API 직접 시작은 선택 상태 불일치로 취소; 공식 툴바 시나리오 재선택 후4인 Running. 보조 에디터의 EditorPrefs 캐시 때문에 기록 폴더를 각 프로세스에 다시 설정.
- run-2: 대기실 새 탐색에서 기존 씬의 로비 보급품을 F로 획득. 일반 이동/물리/F로 화장실 벽6타, 열쇠1→철제함→관리실열쇠, 머리핀→트렁크→계단열쇠, 캐비넷3→탈출열쇠1, 계단문까지 해제. 네 화면 공유 확인. 보급품은 씬에 이미 있는 Editor 전용 도구(새제품 내구도) 포함; 인벤토리/퍼즐 상태 직접 준비 없음.
- 실제 결함: 지하 관리실문 앞(-6.74,-4.92,101.35)에서 지상 물려줄 선반(-6.27,1.20,102.35)이 F 선택됨. HandoverShelfView가 수평 거리만 검사하고 PropCarry.UseShelf는 거리 검증 없음. 같은 ExplorationReach를 로컬/호스트 양쪽에 적용하고 높이/가림 회귀 검사 추가.
- run-2는 조사 시간 중 정상300초 만료→공연 복귀. A 완주 아님. UV 장비 입력은 정상이나 카메라 조준 방향 미조정으로 앞자리 미발견. 카메라 조준 입력으로 재시도 필요. 강제 진행/타이머 연장 없음.

## 2026-10-31 접합부부터 끝까지 검증 재개
- 사용자 범위 확정: 대기실 담당자 푸시 대기. 대기실 씬/내부 버그/UI/공용 시스템은 수정하지 않고 접합부 이후 실제 Unity MPPM4 테스트. 기존 모든 작업은 .harness/local/junction-to-exit-20261031-172556에 공개80/비공개181개 파일/패치/해시로 보존. doctor 기존 private dirty/중복 Task ID 실패2건. Unity 정상 종료 후 dev fetch, HEAD=origin/dev7164c818 Already up to date.
- 이전 전체 퍼즐 run-3는 실제4인/선반 회귀2통과, 관리실 열림 및 UV발견/수첩 열람까지. 사용자 Escape 중단으로 종료, 완주 아님. 그 과정 초기 실패 연결ID1을 정리한 사실도 기록. 이번은 별도 초기 접합부 배치/선행 조건을 명시해 대기실 퍼즐 검증을 제외한다.

- 접합부 run-1: 실제 주 에디터+3 가상 에디터에서 2인 F 문 열기, 최초300초/부분 이탈 유지/전원 이탈 초기화/3인 대기/4인 즉시 출발/개인별 계단 끝 FPS/4인 A입구를 확인했다. 이후 첫 대포→4인 공동 그네→연속 외줄/그네→컨베이어 탑승·하차→2인 목마로 독립 버튼 F까지 정상 입력으로 진행. 초기 접합부 배치 이후 체크포인트/위치 이동 없음.
- run-1 실제 결함: A 이동·회전 때 HumanCannonHazardCue의 저장 origin/destination이 원래 월드 좌표에 남아 일부 톱날/불 경로가339~825m 떨어져 있었다. 공식 Scenario Stop 후 연결 EscapeCampaignContent에 경로 프레임 추적·보정을 추가, A 씬만 저장.29개 보정/최대 오차0.000031m/재호출 불변. 신규 CampaignPlacementTests2개 포함 EscapeRoute EditMode145/145 PASS. 이름 필터 호출2회는0건이므로 검사 통과로 세지 않고 assembly 필터의 실제 결과로 확인했다.
- record-knowledge로 escape-additive-footprint.md에 좌표 누락 원인·예방 기록. run-2에서 수정된 실제 함정으로 접합부부터 재실행 중. 아직 탈출 전 구간 완주 아님.
- 접합부 run-2: 공식 MPPM4에서 합류·FPS·첫 대포·3개 공동 봉·컨베이어를 재검증했다.1명은 검사 조작이 외줄 위에서 목마를 해제해 낙사했고, 나머지3명은 톱을 지나 복귀 대포로 A05에 도착했다. 소실 외줄에서 실제 트램폴린 반동, 하부 운반자의4개 틈 점프, 상부 버튼/복귀문 개방을 확인했다. 운반자는 트램폴린을 잡은 채 계단을 시도하다 바깥쪽으로 물러나 시설 기초바닥에 추락했다. 나머지2명도 화염 두 분출구 사이를 안전 정지점으로 잘못 잡아 사망했다. 마지막 질주/배선/최종 탈출은 아직 미검증이며 run-2는 전원 리타이어 실패다.
- 실제 A 결함: 깊은 바닥Y-115.92에 떨어진 참가자가 Normal 상태로 남아 종료 집계를 막았다. 기존 EscapeFallVoid를 A 프레임에 하나 추가(local X -10..400/Z -90..130, Y<-50), 정상 기믹 바닥/대기실은 제외. 라이브 재현에서 해당 참가자가 정상 리타이어했고 A 씬만 저장했다. proof deep-void-save.json. W 씬 SHA256는 백업과 동일.
- run-2 종료 후 원격 플레이어의 다음 공연 배치 누락은 임시 테스트 부트스트랩이 클라이언트의 SpaceLinkDirector 생성을 빠뜨린 상태였다. 일반 게임은 SpaceLinkHub/DevStartLauncher가 각 클라이언트에서 생성한다. 공용 제품 코드를 수정하지 않고 임시 Connect에서 정상 초기화를 수행하도록 보완, 호스트 공용 UI 로드 후 접속을 시작하도록 했다. 다음 실행에서 실제 전환을 재확인한다.

## 2026-10-31 사용자 중지 유지 — 대포대 리타이어 원인 조사
- 최신 지시로 Play/MPPM 재실행 금지, 대기실/공용 시스템 수정 금지. 이전 run-3는 사용자가 직접 중지했으며 공식4인 전체 완주는 미완료다.
- run-3 공통시계418.899 호스트의 ID3는(-168.4993,-33.3835,104.0847)에서 Retired, 418.903 클라이언트는 실제 착지점(-168.5000,-37.9200,107.2146)에서 아직 Normal, 418.997에 Retired 복제. 위치차5.51145m가 공용 착지 허용5m를 초과한다. 안전 바닥 판정은true, 두 낙사 영역 포함은false였던 읽기 전용 결과와 대조했다.
- 유력 원인: 착지 요청과 원격 Transform 복제의 시차 때문에 safe=false→추락 부상2곳으로 처리되는 경로. 당시 수신 인자/착지 확정 이벤트/이전 부상 기록이 없어 최종 호출 경로와 누적4곳 손실은 확정하지 않았다. 게임 코드/씬 수정 및 Play 재실행 없음. 상세 landing-retirement-investigation.md.
- doctor 1회(-SkipEditor): 기존 private dirty와 중복 Task ID로 FAIL2, CLI버전/skill링크 WARN2. 파일 읽기 조사에는 영향 없음.
- dev 동기화 전 깨끗한 정지 에디터 종료를 요청했으나 PID28136가 남아 동기화 실행을 보류했다. Pipeline quit는 EditMode에서 DontDestroyOnLoad 오류, 직접 Exit 호출은 응답 형식 오류였다. 강제 종료/저장/병합은 하지 않았다. 이번 결과는 로컬 실행 당시 소스/증거 기준이며 새 dev 반영 완료로 기록하지 않는다.
- 동기화 후속 정정: 정상 종료 처리가 늦게 완료되어 Unity 프로세스가 사라진 것을 확인했다. 이후 origin/dev fetch 및 ff-only 병합 결과 Already up to date, HEAD=origin/dev7164c818(0/0). 대기실/공용 파일 변경 없이 최신 dev 포함 확인 완료. Play/MPPM은 재실행하지 않았다.
- 조사 마감 검사: 앞선 실행의 최종 출력 회수 불가로 check-work -SkipUnity -NoRecord를 출력 파일로 재확인했다. 안전성/문서·규칙 PASS, 컴파일·자동 테스트 SKIPPED(exit2). Unity/Play 실행 없음. 결과 .harness/local/landing-retirement-20261031/check-work.txt.
- 2026-10-31 구간별 MPPM4 완료: 사용자 승인 위치 준비/별도 실패 복구 후 실제 입력으로 대포대4안전 착지, 목마 반출교4통과, 권상교4인 점유/도약, 부상자 유지봉/다리, 오른팔만 남은 사용자와 양다리 손실 사용자의 절단기2대·배선24/24, 2도착+2화염리타이어 후 EscapeSucceeded 확인. 화염20초 지연/4초 간격. 상세 targeted-mppm-results.md. 화염 복도 통과 미완료, A기초바닥 y-63.92의 낙사 누락 확인. 제품/대기실/공용/씬 수정 없음. 준비점 오류와 실제 결함을 분리 기록. 공식 Scenario 종료·임시 opt-in 해제.

### 2026-10-31 집중 테스트 후 보수·민규 브랜치 통합
- 확인된 결함: A 전체 심부 낙사 판정의 frame 로컬 높이 -50(월드 -66)이 최하층 바닥 -64보다 낮아 추락자가 바닥에 살아남음. A 씬의 해당 판정 높이만 -40(월드 -56)으로 변경·저장. 정상 대포 착지대/대기실 제외 및 갇힌 좌표 포함을 저장 도구에서 확인.
- MPPM 낙사 재검증을 준비했으나 사용자의 “재검증 하지마 나중에 한번에” 지시로 공식 Scenario를 중지. 보조 에디터3개 종료, 검토 opt-in 해제, 임포트 작업자3/mip0/fps-1 복구. 실제 추락 재검증은 미실행이며 통과로 쓰지 않는다.
- 민규 최신 작업: origin/feat/waiting-room/tutorial 846ccd79. 대기실 씬의 실제 충돌을 사전 확인했고, 사용자가 “민규 형님 씬 기준 + 연결부만 재접합”으로 승인. 기존 대기실의 BasementEscapeConnection(120개 Transform)을 임시 prefab으로 보존. 씬 배치/튜토리얼/판정은 민규 기준, 연결 접합부만 복구 예정.

- 2026-10-31 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 완료: 846ccd79로 fast-forward, 기존 변경 stash7b73f5e 보존/복원. 대기실 씬은 민규 기준으로 연결부120 Transform만 재접합. 기존 객체 삭제0, 기존 변경4개(문 벽 Collider, 메시2, SceneRoots), 새 파생 개구부 메시2개 생성. 임시 전달 prefab은 unpack/저장 후 제거.
- PropCarry 충돌은 민규 문구 조회와 기존 로컬 선반 거리 검사 보존. CommonInventoryUI/HandoverShelfView 자동 병합. 기존 로컬 연결/소지품/메모리 변경은 유지, 신규 대기실 기능 보수 없음. 상세 mingyu-integration.md.
- 사용자 요청으로 재플레이 및 회귀 테스트 보류. 공식 Scenario 중지, 보조3개 종료, review opt-in false, mip0/fps-1/workers3 복구. Knowledge 검사 OK(15 folders). 민규 반영 후 Unity 자동 임포트·스크립트 컴파일은 에디터 열기 과정에서 실행됐으며 별도 플레이 통과로 보지 않음.
- 2026-10-31 사용자 요청: 현재 작업 저장 후 dev 전환. Unity 파일 저장·정상 종료, 공개/비공개 변경을 전체 사본과 각각의 stash로 보존하고 전환한다. 재검증·commit/push는 하지 않는다. 복원 정보는 .harness/local/exit-route-dev-switch.json에 기록.

- 2026-10-31 브랜치 복귀: dev Play 중지·씬 저장·Unity 종료. dev의 MultiplayerManager 설정 변경1건을 별도 stash로 보존(.harness/local/exit-route-resume.json). feat/exit-route/design-map에 origin/dev5176099c를 fast-forward한 뒤 공개 adddc7ac / 비공개5a3d4896 stash 적용, 충돌0. 기존 공개86/비공개181 파일을 백업과 비교: 공개83 완전 동일+3 줄바꿈 차이, 비공개181 완전 동일. stash/백업 유지, commit/push/새 구현/재플레이 없음.
- 2026-10-31 새 지시 구현: 합류 전용 타이머 삭제, 4인 집결→뒤 문 봉쇄→공용 커튼→NGO A 로드·상대 위치/방향 인계→W 해제. 지속 참가 명부와 A 계단 끝1인칭 분리. W19/A117 Transform 연결부 저장, A 프레임 -200으로 중첩 방지. 상세 loading-transition.md. 기존 대기실 내부 기능 신규 보수 없음.
- 구현 확인: 편집 모드 연결 참조 누락0/씬2개 clean/Play=false. verify-unity -Compile PASS(up_to_date), Tests SKIPPED. 사용자 지시대로 Play/MPPM 미실행. 새 흐름 실제4인 통과를 주장하지 않음. commit/push 없음.
- 문서 검사1차에서 내 plan/todo 및 다른 담당 Task079 log 분량 초과 확인. 실행 중 내 plan 문구 정정으로 fingerprint 검사도 중단됨. 내 과거 원문을 History/020으로 이동 후 검사 재실행; 타 담당 문서는 변경하지 않음.

- 2026-10-31 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 검사 남은 차단은 타 담당 Task079 log 138줄/130줄뿐. 내 plan/todo 한도 초과 해결. 독립 Compile PASS와 통합 검사의 컴파일 생략을 구분, Play/MPPM 미실행 유지.
- 2026-10-31 공식 MPPM4: 3인 대기→4인 출발/뒤 문 봉쇄/커튼 확인. 보조3개 로딩 정체 후 접속 이탈, 호스트만 A 도착·W 해제. 전원 이동 FAIL. mip2 재실행4인 접속 직후 사용자 직접 중지. 상세 loading-mppm-results.md, 게임 버그 신규 보수 없음.
- 최신 정정 구현: 고정4인 대신 현재 세션 PlayerCount 전원 집결, 분모 안내/명부 송수신도 가변 인원 적용. 연결부 안내 W 저장. 기존 테스트 소스 갱신만, Play 재실행 없음. opt-in/Dev 설정 복원·보조3종료 확인.

- 2026-10-31 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 유동 인원 변경 컴파일: 최초 Pipeline 통신 오류 후 정상 재시도 PASS(up_to_date), 씬 clean/Play stopped. check-work 안전성 PASS, 문서 FAIL(타 담당 Task079 log138/130줄). 자동·MPPM 미실행 유지, commit/push 없음.
- 2026-10-31 제출 준비: 씬/에셋 저장·Play중지·Unity정상종료 후 dev5176099c 재fetch 동일0/0. 공개93/private181개 해시 검증 백업 junction-to-exit-20261031-223912. PR본문/파일목록/잔여차단은 .harness/local/submission-preparation-20261031. 기존 Compile PASS 재사용. new-pr DryRun은 기존 다중 슬래시 브랜치 이름 거부. 실제4인 전환 FAIL/private미제출/타Task문서FAIL 명시. commit/push/PR 없음.
- 2026-10-31 사용자 명시 요청으로 현재 미완성 상태 commit/push 진행(PR 제외). Unity저장/종료·origin/dev5176099c 동일, 공개94/private181개 SHA256백업 junction-to-exit-20261031-224618, 안전성94개 PASS·기존 Compile PASS 재사용. 기존 문서FAIL/4인 전환FAIL/유동인원미검증 유지하며 다른 담당 문서 수정·검사훅 우회 없음. private5317cd0f를 resource/exit-route-loading-assets에 push, 공개에 포인터 포함. 기존 save-work 전체검사 차단은 알려진 타Task문서 실패이므로 현재 상태 저장이라는 사람 지시로 일반 git 저장을 사용.

## 2026-10-31 사용자 승인 제출
- private main의 README 자동 갱신1f51a4bd를 에셋 작업 브랜치와 충돌 없이 병합,149108fa를 private main/작업 브랜치에 push. 공개1673f9d7+포인터54c4885e를 feat/exit-route/design-map에 push, ThirdParty 검사 PASS.
- PR 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/254 (dev 대상, OPEN, 일반 PR). GitHub connector403으로 동일 RagSpirin 계정 gh 사용. PM팀 검토 자동 지정. 문서FAIL/4인 전환FAIL/유동인원미검증을 본문에 명시, 공개dev 병합/자동병합/Play 실행 없음. 이전 기록 원문만 History/021로 이동.
