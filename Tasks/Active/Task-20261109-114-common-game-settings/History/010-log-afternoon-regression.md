## 2026-11-11 오후 추가 보고
- Unity 종료·기존 변경 보존 후 origin/dev ec3e6e4c로 fast-forward. 인간대포 최신 씬 요청 때 다시 정상 종료·fetch·동일 HEAD 확인 후 에디터 재개.
- 카메라 자세 높이/자기 운반물, 한글 자판 키 표시, 대기실 두 씬의 물리 천장, 장치의 이동·회전 충돌, 발사기 공통 크기·간격과 수동 단발 입력 수정. 값은 Base/TrampolineSettings에 연결.
- 목마의 F 우선순위가 봉 선택을 가리던 코드, 위아래 원격 위치 보간 차이를 점프로 해석하던 코드, 대포 비행 바닥 밖 사망 시 LandingMessage 미전파 경로를 확인하고 수정. Revived에서도 비행 상태 제거. 같은 목마 위 사람을 앞뒤 장애물로 계산하지 않게 수정.
- 사용자 최신 지시로 자동/플레이 테스트 중단. 그 전에 추가한 4인 상호작용 fixture는 PlayerInputReader 누락으로 실패했고 준비 코드는 수정했으나 재실행하지 않음. 새 테스트 소스가 있다는 사실을 통과 결과로 쓰지 않는다. 이후 소스 컴파일 오류0 확인.
- Start의 인간대포 경로와 빌드 프로필은 최신 DEV와 같음. 로컬 다른 worktree의 인간대포 씬도 더 오래된 버전이며 현재 씬의 11/5 이후 diff는 옛 설정122줄 제거뿐. 대포 위치/각도/발사속도·발판 배치 변경 없음.
- 외형 차이는 11/11 대기실 장치 로더가 RoundTrampolinePrepared를 선택하는 반면 공연/대포 재연습은 CC_Trampoline을 쓰는 분기에서 확인. 대기실 장식에도 두 종류가 존재. 커밋 날짜를 정본의 근거로 제안한 설명을 철회하고 사용자가 지정한 튜토리얼·대기실 원본을 미리보기로 확인 중.
- 트램펄린 usePropOutline 차이가 외형뿐 아니라 반동 접촉과 F 기준점을 바꿈을 추가 발견. TrampolineSettings 공통 설정으로 이동 중. 기본 속도·거리·손잡이 로컬 위치는 동일. 재연습은 대포10→4/운반발판3→1/추격불2→0의 의도된 별도 배치이며 비교한 장치 기본 수치는 동일.
- 정본 확정: 사용자 미리보기2번, 빨강·파랑 옆면의 RoundCarryTrampolineVisual. TrampolineSettings.visualPrefab에 저장하고 HumanCannon 공연1개, 대기실/추격대기실/WaitingRoomBase 각각4개 장식 및 RehearsalCourseFromPerformance 트램펄린에 적용. 운반용 RoundTrampolinePrepared는 원래 이 외형이라 유지. 대포·발판 루트·발사속도는 수정하지 않음.
- HumanCannonPropArtBuilder가 다시 CC_Trampoline을 강제로 적용하던 경로도 공통 에셋 참조로 수정. 기존 스크립트에 트램펄린만 적용하는 메뉴를 추가했으며 새 런타임 스크립트는 없음. 외형을 바꾸면 해당 메뉴로 씬에 반영하는 방식과 Tooltip을 제공.
- 반동 접촉(원형/손잡이 외곽)·잡기 기준(손잡이 위0.6m)·운반 충돌 모양을 TrampolineSettings에 연결. 실제 per-object usePropOutline 제거 및 제작 도구의 재도입 방지. 조사 제외인 탈출로 씬의 죽은 직렬화 필드는 재저장하지 않음.
- 첫 check-work: 안전성·문서/규칙 통과, 실제 컴파일 PASS 후 공통 설정 확인용 Pipeline run_script 연결이 reset되어 전체 스크립트는 FAIL. 자동/플레이 테스트는 실행하지 않음. 마지막 저장 후 다시 확인한다.
- 이번 수정 전후와 남은 확인 범위: [오후 수정 목록](afternoon-fixes.md).

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=FAIL; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
