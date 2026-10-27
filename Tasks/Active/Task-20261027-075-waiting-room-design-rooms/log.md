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
