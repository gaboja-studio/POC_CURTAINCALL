# 공통 상호작용·셔터 검증
2026-10-20 · Unity 6000.3.25f1 · 현재 checkout의 연결된 Editor/Unity CLI에서 실행.

## 실행 결과
| 검사 | 결과 | 범위 |
|---|---|---|
| Compile | PASS | 최종 컴파일 오류 없음 |
| SceneIntegration PlayMode | **25/25 PASS** | 기존 공연/대기실 UI 회귀 + 신규 상호작용2개 |
| SpaceLink PlayMode | **6/6 PASS** | 실제 통로 Z70의 퇴장/타이머·밤 셔터 충돌·아침 해제·F 스케줄표/준비/재진입 |
| Game 화면 | 확인 | 1920×1080, 전화기 C6/외곽선, 낮/밤 셔터 정렬, 인간대포 근접 외곽선 |
| 인간대포 1인 LAN 호스트 | 확인 | 실제 NetworkPlayer를 배치: Cannon+DepartureCannon3 → 반대편 이동 시 Cannon+DepartureCannon2 → 카메라 반전 시0개 |
| check-work | **Scope FAIL425** | 병합 이력/타 Task 중첩 및 구역 밖 변경. 뒤 단계 SKIPPED. 이전 FAIL420을 PASS로 취급하지 않음 |
| 개별 verify-fast / diff-check | OK / PASS | 문서 경고10/hard0. 컴파일/게임 테스트 통과와 별도 |

신규 자동 검사는 3개 사물 중 가까운2개, 대상 이동/삭제, 범위 이탈, 카메라 반전, 벽 가림, 원본 재질 유지, 실제 전화기 C6/F/ESC/입력 잠금, 공통 UI 재등록/중복 방지를 확인한다.

초기 실패도 조사했다. 신규 테스트의 Reflection 오버로드 지정과 테스트 종료 시 서비스 재생성 문제를 수정한 뒤 UI25개 전체를 다시 통과했다. 통로 Z70 회귀에서 내부 영역이 복도와 겹치는 실제 버그를 찾아 씬의 영역을 수정한 뒤 공간6개 전체를 다시 통과했다.

## 화면/로그 근거 (로컬, Git 제외)
- `.harness/local/interaction-ui-25.json`, `interaction-space-6.json`.
- `.harness/local/interaction-phone.png`, `interaction-door.png`, `interaction-door-night.png`, `interaction-cannon.png`.
- `.harness/local/interaction-fast.txt`, `interaction-check-work.txt`.
- 대기실 화면 검증: CLI console cursor2648 이후 신규 오류/경고0 확인.
- 인간대포 열기/실행 구간: 기존 `50_LabelTMP` 정본 Pretendard-Medium SDF의 누락 글리프 경고11개가 도구 기록에 남았다. 최종 Play console groundTruth 오류0/경고0, Game 화면의 구간 한글은 표시됨. 편집 시 폰트 경고까지 해결했다고 보고하지 않는다.
- 위치·카메라·밤 상태는 API로 주입한 합성 검사다. F/ESC는 가상 Input System Keyboard로 검사했다. 실제 사람이 전체 플레이한 검증과 구분한다.

## 보존한 별도 변경
아래4개에는 폰트 glyph/atlas 캐시 및 재질 부동소수점 직렬화 차이가 있다. 기능 패치에 필요하지 않다. HEAD 덮어쓰기 정리는 자동 승인 검토에서 **미승인 미커밋 변경 삭제 위험**으로 거부되어 실행하지 않았다. 작업 파일은 그대로 보존하고 `.harness/local/interaction-preserved-assets/`에 별도 복사했다. 다음 저장 때 명시적으로 판단하며 이번 UI 구현 파일과 섞지 않는다.
- `Assets/Resources/Fonts/CheerGauge/Pretendard-Bold SDF.asset`
- `Assets/Resources/Models/Materials/PerformanceArtKit/GripYellow.mat`
- `Assets/Scenes/Tests/MainStage/HumanCannon/Materials/pole-gate-black.mat`
- `Assets/Scenes/Tests/MainStage/HumanCannon/Materials/section-sign.mat`

## 미실행
- 실제 사람의 전체 키보드·마우스 플레이, 2~4클라이언트·재접속·원격 이동 중 외곽선/상점/스케줄.
- 인간대포 트램펄린/봉의 실제 잡기·놓기 협동 플레이, 로비 사물의 실제 다인 조작.
- 여러 해상도, 빌드, 전체 프로젝트 테스트 스위트, 성능 프로파일링.
- 신규 백스테이지·탈출로는 존재하지 않아 적용/실행하지 않음. 현재 외줄에는 F 대상 사물이 없어 장애물/불을 임의로 상호작용 대상으로 만들지 않음.

수정 파일/데이터와 사람 확인 순서는 [적용 가이드](interaction-highlights.md) 참조. 이번 후속은 미커밋·미푸시.
