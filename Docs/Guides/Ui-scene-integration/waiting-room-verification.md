# 대기실 HUD 검증
2026-10-20 · Unity 6000.3.25f1 · feat/ui-scene-integration · dev4d75f7e/병합981fe7f 위의 변경.

## 결과
| 검사 | 결과 | 근거 |
|---|---|---|
| 변경 전 대기실 F 입력 | PASS 1/1 | PhysicalRoom_NightLocksAndReadyStartsMorningWithoutReload. Input System의 가상 F 누름/해제 → 스케줄표 열기, 준비 완료 후 다음 아침. 인계의 F 실패는 이번 환경에서 미재현. |
| 변경 전 공간 전환 | PASS 6/6 | SpaceLinkPlayTests: 실제 대기실 출구/부분·전원 집결/밤·아침, Single/Additive, 실제 씬 설정 매핑. |
| 컴파일 | PASS | 최종 verify-unity -Compile 완료. |
| UI PlayMode | PASS 23/23 | 기존 공연 UI 21 + 신규 WaitingRoomHudPlayTests 2. 최종 전체 필터 실행 결과. |
| HUD 적용 후 공간 전환 | PASS 6/6 | SpaceLinkPlayTests 동일 필터 재실행. |
| Game 화면 | PASS, 합성 상태 | 1920×1080 실제 Game View 캡처에서 시계/잔액/기물 슬롯/소집 배너/방송 자막, 한글 정상 표시. 프레임1306→4020→9966 증가 확인. |
| 실행 오류 | 신규 오류·경고 0 | 이번 화면 검사 console cursor2255 이후 entries0, Editor consoleErrors0/consoleWarnings0. 과거 세션 로그는 결과에서 분리했다. |
| 종합 check-work | FAIL | 구역420건: 병합 이력/다른 Task 문서·기존 구역 중첩. 이후 단계는 종합 SKIPPED. 이전 사용자 구역 예외를 유지하고 개별 검증 결과를 별도 기록. |
| 개별 verify-fast / diff-check | PASS | Fast OK(기존 문서 크기 경고10, hard0), git diff --check 통과. |
| 저장된 씬 참조 | PASS | 공간 연결1개, 원본 Assets 프리팹3종 참조, 공통 UI Missing Script0, 저장 표시 시간4초. 에디터 대기실+공통 UI 열림/Play 정지, W1 시계 편집 미리보기. |

## 신규 테스트가 확인한 것
- 실제 WaitingRoomBase + 별도 UI 씬에서 W1/C4 값 유효, 자금 원천 일치, WaitingForExit의 멈춤 → 출구 신호 뒤 탐색 표시.
- 실제 PropCarry.Receive/RequestDrop으로 슬롯 표시, 배낭 한도 증가, 시간 경과 숨김, 내려놓기 후 재표시. 숫자키 선택은 구현/검증하지 않는다.
- 로컬 위치를 집결 영역으로 이동해 실제 집계1/2를 표시. 밤 진입 후 시계/집결 숨김, 자금 유지.
- Announcer 이벤트 → 방송 자막·경보, 기존 AudioSource 존재. 공간 등록 해제 → UI 씬 해제 → 재등록 때 HUD1개만 생성.
- 구형 ExplorationTimerCanvas 생성 없음, SharedFundsView 제거, PropCarry 구형 HUD 꺼짐.

## 수정 중 발견 및 처리
- 초기 신규 테스트 실패는 Reflection 오버로드/공개 필드 접근, 집결 라벨 문자열, 비동기 unload 직후의 오래된 객체 대기 조건 등 검증 코드 문제였다. 수정 후23/23 통과.
- 오프라인 여러 테스트 캐릭터 중 로컬 식별은 PlayerInputReader를 우선하도록 연결 코드 보완. 네트워크에서는 NetworkPlayer.Local만 사용한다.
- 화면 검사는 오프라인 런 API 및 방송 호출로 상태를 만든 합성 검증이다. Start에서 실제 플레이하여 완주한 결과로 보고하지 않는다. 캡처를 위해 표시 시간을 런타임에서15초로 늘렸으며, 저장 씬 값은4초를 유지했다.
- 기존 개발 모드 배지/음성 상태 IMGUI가 상단 HUD와 겹칠 수 있다. 해당 기능의 UI를 이번에 대체한 것은 아니므로 유지했다. 정식 화면 검토에서 배지/음성 UI 배치를 함께 조정해야 한다.

## 미실행
- 사람의 실제 키보드·마우스 조작, 실제2~4인 호스트/원격 클라이언트, 늦은 참가·재접속·네트워크 지연.
- 현재 변경으로 Start부터 두 공연을 실제 완주하는 전체 플레이, 다른 해상도/플레이어 빌드.
- 종소리 청취 및 손맛. AudioSource/경보 이벤트 연결만 확인했다.
- PR194의 별도 공연/보상 테스트에서 기록된 나머지 실패3종은 이번 필터 대상이 아니므로 재검증하지 않았다. 이번 SpaceLink6 통과를 전체 프로젝트 테스트 통과로 해석하지 않는다.

## 로컬 결과 파일
Git에 포함하지 않는 `.harness/local/`: waiting-input-baseline.json, waiting-space-baseline.json, waiting-space-after-hud.json, waiting-ui-final-23.json, waiting-runtime-console.json, waiting-check-work.txt, waiting-fast.txt, waiting-hud-rest.png, waiting-hud-announcement.png.
초기 실패 이력은 waiting-hud-first-failures.json/waiting-ui-23-first.json, 개별 수정 통과는 waiting-hud-state-pass.json에 남긴다.
