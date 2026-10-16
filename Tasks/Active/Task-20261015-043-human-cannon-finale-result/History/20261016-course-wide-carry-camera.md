# 전체 코스 운반 시점

- 사용자 최신 지시: 트램폴린 잡기 자유3인칭/놓기1인칭을 원본 전체에 적용. 앞으로 범위를 지정하지 않으면 기존 연속 코스 전체가 기본이며 연습에도 같은 동작을 적용한다.
- HumanCannonPilot이 확정 소유권으로 원본/연습 공통 시점을 선택한다. 첫 연습 구간 전용 카메라 partial을 제거했다.
- 기본1인칭, 운반자유3인칭, 놓기1인칭. 정지 둘러보기 중 몸·트램폴린 방향 유지. 대포 탑승3인칭/발사즉시1인칭과 사망 관전 우선 유지.
- 공용 Camera/Player 코드·Resources 프리셋·씬 자산은 이번 보완에서 수정하지 않았다.

## 검증

- Unity 컴파일 completed/failed:false.
- 전체 HumanCannon PlayMode23/23 PASS, 54.92초. 원본 잡기/놓기·정지 둘러보기·렌더 중 몸 가림/표시, 첫/둘째 운반, 승강기3곳 운반시점/놓기, 원본10대 탑승/취소, 발사즉시1인칭, 관전·재시도·메뉴 입력 차단 회귀 포함.
- 근거: `.harness/local/task043-course-wide-carry-playmode.json`, 같은 이름 `.xml`.
- F8 없이 원본 LAN호스트 실행, 실제 손잡이에 접근/잡기/놓기. practice:false, Grab free:true/hidden:false/carrier=actor0, Drop first:true/hidden:true/carrier=Nobody 확인.
- 실제 게임 카메라 화면: `.harness/local/task043-original-carry-third.png`, `task043-original-drop-first.png`. 운반 중 몸·초록 손잡이 표시, 놓으면 눈높이/몸 숨김.
- 최종 Stop·씬 dirty:false, Console 신규 오류0(cursor4536→4542), 현재 오류0/경고0.
- 실제4인 동기화·사용자 마우스 조작감은 미검증. 자동 검증을 직접 플레이 완료로 표시하지 않는다.

## 제출 준비

- 사용자 요청은 제출 준비. 커밋·푸시·PR 생성은 수행하지 않는다.
- 대상 `integration/main-stage-human-cannon`. 원격 부모204be8c가 HEAD0128623에 포함됨을 확인했고 원격 작업 브랜치도0128623이다.
- `check-work` 구역 FAIL8은 기존 관리 문서(통합3/042문서4/043setup1). 후속 단계 SKIPPED와 별도 실행한 컴파일/테스트 PASS를 구분한다. 제출 전 구역 판정 해소 필요.
- PR 초안: [제출 준비](20261016-submission-preview.md).
