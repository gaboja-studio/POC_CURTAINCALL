# 추격자 최종 기술 검사 — 2026-11-13

기준 DEV는 `5c7754ae`, 작업 브랜치는 `feat/exit-route/pursuer`다. 저장·제출 직전에 Unity를 정상 종료하고 다시 fetch/ff했다. private 파생 표면 에셋은 `0f7585c8`이며 원본 에셋/충돌 형상은 유지한다.

## 실행

Unity 메뉴 **Tools → CurtainCall → 탈출로 → 추격자 첫 등장 테스트 씬 열기**에서 열고 Play한다. 테스트 씬은 `Assets/Scenes/Tests/EscapeRoute/Pursuer/UnderstagePursuerPlaytest.unity`이며 최신 탈출로 A 전체 복사본이다. 기본 로컬 1인으로 움직일 수 있다.

F6으로 같은 씬에서 동작 모드를 켜고 끈다. 1 벽 파괴 / 2 걷기 / 3 돌아서 추격 / 4 숙이기 / 5 달리기 / 6 점프·착지 / 7 팔 공격·회피 / 8 포획·투척. R은 선택 동작 재생이다. 실제 난이도 검사는 F6을 끈 일반 추격에서 한다.

## 결과

| 검사 | 결과와 범위 |
|---|---|
| 강제 종료 복구 | 미저장 작업572파일 백업, 빈 파일·전체0·텍스트NUL 손상0, 양 저장소 Git 연결성 정상. 복구 씬은 별도 보존 |
| 저장 씬 참조 | A/test 각각5879오브젝트, 누락 스크립트0·프리팹0. 74경로·8절단부위·4발사대·149패널 확인 |
| 최신 컴파일/공용 설정 | check-work PASS. 자동 테스트는 이 명령에서 선택하지 않음 |
| 탈출로 EditMode | 복구 후256/256 PASS, 실패0·생략0. 새 패널 교차 검사6개 포함 |
| 공용 회귀 | 제한시간 제거 묶음에서686/686 PASS. 이후 변경은 벽 위치·경로 외장·표면과 패널 검사이며 공용 테스트를 다시 실행하지 않음 |
| 정확한 첫 등장 | 첫 Understage 벽(15,1,-1), 파괴 모듈(15,0,-1)/Y180°. 실제 호스트에서 파괴→약6m 보행→달리기→포획 상태 확인 |
| 가림·공격 | 실제 호스트를 구간별 배치한18개 압박점에서 패널 개방 후 공격선 막힘0. 재시작 시 열린 패널 초기화 |
| 대포 통과 | 4대포 기본 LaunchVelocity/공통 Gravity를 적용한 캡슐 탄도에서 새 외장 충돌0 |
| 연속 경로 | 1671개 몸통 표본의 외장 접촉은 사전 개방 출입/머리핀 셔터에 한정. 초기 NavMesh3경로 완료 |
| 제한시간 | 카운트다운 비표시, 만료 리타이어 경로 제거, 내부 연출 시각/재시작 유지. 실제8분 대기 대신 경계값과 코드 경로 검사 |
| 최종 전투 | 앞선 실제 호스트 검사에서 점화·전방 도약·8회 절단·문은 배선으로만 개방·재시작·발사대 사용 중 포획 확인 |

측정 원본은 `.harness/local/task100-complete/`의 `concealment-play.log`, `right-wall-play.log`, `cannon-casing.txt`, `concealment-audit.txt`, `final-reference.txt`, `after-recovery-escape-256.json`, `concealment-final-check-work.txt`에 보존했다. 수치 요약은 [Review/completion-verification.json](Review/completion-verification.json)에 저장한다.

## 남은 사람 확인

- 전 구간 2~4명 동시 완주, 원격 화면의 재등장·포획·최종 전투 체감은 미확인이다.
- 공포감, 발소리 거리감, 첫 시야, 난이도, 실제 플레이 카메라에서의 표면 품질은 사용자가 확인한다. 기술 검사가 재미·연출의 최종 승인을 뜻하지 않는다.
- doctor의 기존 중복 Task ID10개 및 문서 길이 경고는 이번 추격자 변경과 별개다. 최종 문서 검사의 hard 오류는0이다.
- Windows Kernel-Power41/EventLog6008은 비정상 종료를 확인할 뿐 원인을 특정하지 않는다. 복구 후 Unity 컴파일 오류/손상 참조는 발견되지 않았다.
