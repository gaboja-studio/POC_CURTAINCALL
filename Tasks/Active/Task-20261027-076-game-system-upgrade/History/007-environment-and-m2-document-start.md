# 환경 준비·M2 문서 반영 초기 기록

`log.md` 길이 제한에 따라 2026-10-28 원문을 이동했다. 이후 동기화 완료·현재 미해결 내용은 현재 log를 따른다.

## 2026-10-27 환경 준비
- 시작 doctor(-SkipEditor): FAIL 1(task-ids 061·062·070), WARN 2(unity-cli 문서 버전 차이·skill-links 없음).
- task-ids: 원인은 병합 완료된 `integration/task-document-closeout` worktree의 `Tasks/Active`에 남은 빈 폴더 3개였다(Done 이동 후 Git이 빈 폴더를 남김, 파일 0개). 빈 폴더만 삭제했다.
- Unity CLI `1.0.0-beta.13` → `beta.14` self-update. 변경 사항을 `Harness/Engine/Unity/Facts/unity-cli.md`에 기록했다. `verify-unity.ps1`은 폐기 예정인 status 행 `project` 대신 `projectPath`를 먼저 읽게 했다. 에디터를 연결한 상태로 확인하지는 않았다.
- 재실행 doctor(-SkipEditor): FAIL 0, WARN 1(skill-links 없음, 로컬 선택 설정).
- `setup-links.ps1` 실패: Windows 심볼릭 링크에 관리자 권한 또는 개발자 모드가 필요하다(개발자 모드 꺼짐). 실패로 남은 빈 `.claude/skills` 폴더는 지웠다. 사용자가 개발자 모드를 켠 뒤 다시 실행한다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-27 M2 변경점 문서 반영
- 기준: Confluence 27721807 "M2 게임 시스템 변경점 정리" + 사용자 결정(쉬는 시간 A/B 삭제·시작 즉시 시계·안전지대 없음, 난이도는 세트리스트에서 클리어 단계+1까지 선택·단계별 1인 호응 상한 증가, 신체 복구 가격 팀 전체·런마다 초기화, 외줄 복귀는 F 입력 후 2초 동작).
- Confluence 반영: 게임 흐름 기획(15564804, v15), 부상 기획서(14254330), 장소 별 구조 및 역할(10454669), 조작키 역할 구성(11633076). 주형락 제안 페이지(10551649)는 대상 아님. 논의 기록(13861046)은 폴더 밖이라 미반영.
- 하네스 반영: Decisions 22개, Facts 5개, `Docs/Domains/common-game-systems.md`(M2 미반영 구현 목록 추가).
- 추가 결정(H1~H9) 반영: 방은 런마다 섞고 진행도는 런 동안 유지, "물려줄 선반" → 가판대, 밤 정비는 전력 차단·철창과 침대 수면으로 종료, 외줄 매달림 7초·동료 F 2초 끌어올리기·목마 낙하 한 줄에 한 명, 환호 포인트 수치는 보상 시스템 기획(27459631), 시설 서비스 삭제(서비스는 신체 복구만). 범위: Facts 3개(game-flow·overview·run-state), Decisions 13개(대기실 6·외줄 3·control-keys·injury-rules·인간 대포 injury-hazards·index), 도메인 지도 M2 미반영 구현(시설 서비스·밤 준비 완료·외줄 매달림·랜덤 방).
- 문제: Confluence MCP 갱신은 본문 전체를 한 번에 보내야 해서 큰 페이지는 막힌다. HTML의 `data-local-id`를 빼면 크기가 절반 가까이 준다(인라인 코멘트가 없을 때만 안전).
- 추가 결정 게시 확인: 부상 기획서 v14는 게시 후 본문·이미지·목차 보존 확인. 장소·조작키는 담당 에이전트가 API 사용 한도로 종료됐지만, 직접 다시 읽어 가판대·철창·침대·동료 끌어올리기 반영을 확인했다. 게임 흐름 페이지는 API 사용 한도로 중단되고 직접 게시도 권한 검사에서 거부됐으나, 사용자 명시 승인 후 v16 게시 완료. 게시 후 HTML 재조회로 추가 결정과 첨부 이미지 2개 보존을 확인했다. 새 보상 결정(토큰·호응 환산·완주 보너스·사망 시 절반 인정 등)은 별도 논의 중이며 이번 게시에는 포함하지 않았다.
- 문서 검사(-SkipUnity -NoRecord): hard 0, FAIL 0, 기존 길이 경고 10개로 종료 코드 2. 컴파일·테스트는 생략. `git diff --check` 공백 오류 없음.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
