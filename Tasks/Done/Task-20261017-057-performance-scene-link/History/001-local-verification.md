# 실제 공연 연결 로컬 검증 (2026-10-17)

## 구현·자동 검증 범위
- 묘기별 설정 매핑과 기존 DummyPerformance fallback, TryFind의 두 실제 공연 씬 인식, 같은 Performance 공간의 다른 목적 씬 전환을 추가했다.
- 전환 요청은 목적 씬·세대·일차·공연 순번·묘기를 고정하며 오래된 요청을 폐기한다. NGO Single/Additive 전환·이전 씬 제거와 로딩 실패 경로를 처리한다.
- 런타임 진입 컴포넌트는 연결 공연에서만 설치한다. 늦은 참가자 준비를 기다리고, 부상을 보존하며, 임시 조작 정책과 구독을 해제한다. 인간 대포는 기존 공용 정산을 재사용한다.
- 줄타기는 결과 이벤트 이후 실제 상태 반영을 기다려 유효한 공연에 한 번만 정산한다. 공용 전멸 정산 선행·종료 이벤트 재진입 시 이전 결과를 폐기한다. 준비 전/외부 종료 뒤 피해·도착 판정도 막는다.
- 기존 69개 검사에는 신규 연결 11개, FinalCannon 9개, 기존 SpaceLink 및 DummyPerformance 회귀가 실제 포함되어 전부 통과했다. FinalCannon은 저장된 HumanCannon 씬에서 실제 1인 LAN·탑승·비행·착지를 검사하되 위치/입력 일부를 합성한다.
- 이후 디스크 설정 감사에서 씬 이름의 불필요한 따옴표를 발견해 Editor로 수정했다. 실제 저장 매핑과 빌드 등록을 검사하는 테스트 1개를 추가했으므로 최종 전체 검사를 다시 수행한다.
- 설정은 두 씬 매핑만 추가했고, 기존 빌드 목록 순서를 유지한 채 HumanCannon을 마지막에 등록했다. 원본 씬 YAML을 편집하지 않았으며 GUID를 보존했다.

## 실행 로그 (2026-10-17)
- 시작 메뉴의 LAN/방장 버튼으로 실제 방을 열고 Lobby로 진입했다. 기존 DevMode 인원 예외가 켜진 1인 LAN이며 설정을 변경하지 않았다.
- 명단·레버 앞 위치는 합성 배치하고 F 짧은 입력을 프레임 단위로 주입했다. 실제 ReadyCount=1/AllReady 확인 후 도입을 거쳐 `proto_0.0.2`에 도착했다.
- 줄타기: 세션 Playing, 공용 Performance/Tightrope, 외부 관리 Running, 플레이어 Normal 및 출발 위치·게임 화면을 확인했다. 도착 위치를 합성한 뒤 실제 정산으로 DummyWaitingRoom/RestTime에 전환됐고 세션은 Playing을 유지했다.
- 중간 진행은 LeaveWaitingRoom과 NotifyAllAtStageEntrance 공개 API로 합성했다. 기존 탐색 단계를 거쳐 HumanCannon에 진입했으며 기존 순서와 규칙은 바꾸지 않았다.
- 인간 대포 연결: 실제 HumanCannon 씬, Performance/HumanCannon, 공용 정산 열림, Normal·출발 위치·게임 화면을 확인했다. 목적지 위치 합성 뒤 실제 도착 판정으로 성공 정산 1회 및 DummyWaitingRoom/Night 전환을 확인했다. 방은 Playing을 유지했다.
- 인간 대포 직접 실행: 저장 씬을 열어 실제 1인 LAN을 만들었다. Waiting/공용 단계 None, 연결 컴포넌트 없음, 기존 HUD 활성 상태였다. 목적지 위치 합성으로 PracticeCompleted=true, 공용 결과 없음·방 Waiting 유지를 확인했다.
- 단독 재시작: 출발 위치를 원래 스폰으로 복원하고 RequestRestartAll API로 연습 결과 초기화를 확인했다. 합성 사망 후 PracticeFailed=true/Fallen, 재시작 API 후 false/Normal 및 원래 출발 위치·방 Waiting 유지를 확인했다. R 키 자동 입력은 반응을 확인하지 못했으므로 R 키 조작 성공으로 보고하지 않는다.
- 줄타기 직접 실행은 신규 저장 씬 회귀에서 독립 시작·도착·세션 종료·조건부 재도전과 중복 거절을 검사했다. 참가자 수·진행 상태 일부는 테스트 메모리에서 합성하고 복구하며 저장 설정을 변경하지 않는다.
- 연결 실행 후 Console의 현재 오류 0/컴파일 실패 false를 확인했다. 버퍼에 남은 이전 날짜의 Multiplayer PlayMode 패키지 예외와 현재 실행 결과를 구분했다. 독립 실행 후 Play를 종료했고 원본 HumanCannon 씬 dirty=false를 확인한 뒤 Start를 다시 열었다.
- 한계: 위 결과는 1인 LAN 및 일부 위치·상태·입력/API 합성 검증이다. 사람의 정상 조작 완주·실제 2인 동기화·전체 코스 플레이 확인을 대신하지 않는다.

## 공동 테스트 결과
- 일시: 미실행
- 참여자: 실제 2인 미확보
- 재현: plan.md의 합의된 3항목
- 예상: 양쪽 동일 씬·정상 조작, 공용 정산 1회와 상태 보존, 두 묘기 독립 실행 유지
- 실제: SKIPPED(실제 공동 확인 전). 자동 검사·1인 LAN 결과를 공동 PASS로 대체하지 않는다.

## 교훈 제안 (PM 통합 시 지식 기록)
- Unity MCP set_serialized_field로 문자열에 JSON 바깥 따옴표를 중복 전달하면 따옴표 자체가 저장될 수 있다. 실제 디스크 diff와 저장 매핑 회귀 검사로 검증한다. 수정은 Editor에서 수행했다.
- 현재 feat 권한으로 Harness 지식 폴더를 수정하지 않았다. PM 통합 시 record-knowledge 절차에 따라 Engine Pitfalls 기록으로 승격한다.

## 최종 검사
- 저장 매핑 추가 후 첫 검사는 handoff의 40줄 제한 초과로 문서 단계 FAIL, 컴파일·테스트 SKIPPED였다. 상세를 이 파일로 분리한 뒤 check-work를 다시 실행했다.
- `pwsh -NoProfile -File Scripts/check-work.ps1 -Mode PlayMode`: 구역·문서·컴파일 PASS, PlayMode 70/70 PASS(실패·생략 0, 103.64초). 실제 저장 매핑 회귀도 Passed를 확인했다.
- 최종 Console 오류 0, 컴파일 실패 false. 경고 1개는 기존 DummySpaceFlowPlayTests의 비호스트 EndEscape 거절 검사에서 발생한 메시지다.
- 종료 후 Editor ready/stopped, Start 씬 dirty=false 확인. 기능 변경 미커밋·미push, PR·온라인 검사·enhancement 전달·2인 공동 확인은 미실행이다.
