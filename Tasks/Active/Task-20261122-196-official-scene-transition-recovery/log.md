# Log

- 최신 origin/dev 8f95c4e5에서 새 브랜치를 시작했다. 직전 정산·절단기 수정은 PR #478에 제출했다.
- EditorBuildSettings와 BaseGameSettings를 확인했다. Start·Lobby·WaitingRoom, 정식 묘기 HumanCannon/Tightrope, 공식 escape_route가 빌드에 활성화되어 있고 기본 전환은 Single이다.
- 묘기 결산이나 탈출로 A 결과 확인 뒤 NGO가 SceneEventInProgress를 반환하면, 기존 전환에서 이 상태를 곧바로 실패 처리했다. 이미 다음 캠페인 상태로 올라가 나중에 재요청을 발행할 상태 변경이 남아 있지 않았다. 현재 요청이 유효한 동안만 잠시 기다려 같은 NGO 이벤트 시작을 재시도한다. 성공한 이벤트는 중복 발행하지 않는다.
- 전환의 커튼 확인이 처음에 타임아웃하면 기존 요청이 사라졌다. 같은 캠페인 상태이면 커튼 확인을 한 번 다시 한다. 새 요청이 들어오거나 캠페인 상태가 달라지면 이전 요청은 폐기한다.
- 묘기에서 대기실로 돌아가거나 엔딩 이후 이동 중 NGO가 접속 종료자를 타임아웃으로 보고하면 기존의 정식 묘기 진입 전용 필터를 타지 않았다. 정식 후속 전환에도 해당 필터를 적용한다. 호스트 및 아직 연결된 참가자의 실제 타임아웃은 계속 실패로 기록한다.
- 에셋 불일치와 기존 Task ID 중복은 기존 Doctor 경고·실패다. Assets/_ThirdParty 사용자 작업은 유지했다.
- 동일 요청의 NGO 시작 대기 한도는 최소 30초이며 실제 NGO LoadSceneTimeOut보다 1초 더 둔다. 긴 씬 로드의 30초 이후 재시도도 검증했다.
- 최종 EditMode 8/8 및 컴파일 통과(.harness/local/official-handoff-complete.xml). 시뮬레이션한 NGO 응답으로 일시 거절·새 요청 발생·기한 초과·검증 거절을 확인했으며 정상 캠페인 상태 진행으로 묘기 종료와 A 성공 엔딩의 다음 씬 선택을 확인했다. 실제 Start 멀티플레이와 렌더 커튼은 확인 범위 밖이다.
- check-work 정적 안전성·문서/규칙 통과. 컴파일과 EditMode는 별도 Unity 실행으로 확인했다. git diff --check 통과. 새 수정은 fix/official-scene-transition-recovery 로컬 작업이다.

- 2026-11-22 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/479 (fix/official-scene-transition-recovery → dev)
