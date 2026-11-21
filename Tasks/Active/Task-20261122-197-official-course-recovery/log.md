# Log

- DEV 8f95c4e5. 직전 전환 3건은 PR #479로 제출했다.
- EditorBuildSettings와 SpaceTransition을 기준으로 Start·Lobby·WaitingRoom에서 이어지는 HumanCannon, Tightrope, escape_route를 확인했다.
- 인간대포. 실제 쉼터 승강기는 1인 운반자가 조건을 유지해야 자동으로 움직인다. 호스트에서 보간된 위치로 인원을 세면 내려간 참가자가 발판에 남거나 탑승한 참가자가 빠져 대기 시간이 초기화된다. 호스트의 최신 발 위치로 탑승/하차·목마·트램펄린 운반자 접촉을 판단하고 출발 좌표도 같은 위치를 쓴다.
- 외줄. 후반 목마 관문의 출발선 승인과 도약 계산에는 보이는 복제 캐릭터의 위치 대신 최신 발 위치를 사용한다. 동일한 목마 층수, 외줄 지지, 허용 거리 및 착지 조건을 적용한다.
- 탈출로 A. 사라지는 외줄 아래 작업 구역에서 가운데 수직 레이만 빗나가도 실제로 지지된 참가자가 작업자·초과 인원 처리에서 빠졌다. 실제 캡슐이 받치는 단차를 보조 검사하며 동일 씬, 물리 충돌, 접지 높이 조건을 적용한다.
- 비공개 에셋 HEAD 불일치와 기존 과거 Task ID 중복은 유지했다.
- 신규 EditMode 5/5 및 Unity 컴파일 통과(.harness/local/official-course-progress.xml). 실제 인간대포 씬의 쉼터 승강기로 참가자의 최신 탑승·하차 (2건), 외줄 목마 관문에서 늦은 원격 모델 (1건), 탈출로 A 하부 작업 구역과 동일한 조건에서 발판 모서리의 실물·트리거 구분 (2건)을 확인했다.
- check-work 정적 안전성·문서/규칙 검사와 git diff --check 통과. 실제 실시간 다중 클라이언트 플레이는 수행하지 않았다. 이번 후속 수정은 fix/official-course-recovery 로컬 작업으로 유지한다.

- 2026-11-22 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/480 (fix/official-course-recovery → dev)
