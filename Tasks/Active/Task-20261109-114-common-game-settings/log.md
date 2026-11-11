# Log

이전 원문: [001](History/001-previous-stages.md), [11/10까지](History/003-log-through-tutorial-followup.md), [11/11 진행 기록](History/004-log-before-completion.md).

## 2026-11-11 후속 회귀·모션 수정
- 최신 지시로 저장·제출 승인. 이전 제출 금지 해제. 공통 GameSettings 유지·탈출로 제외·기존14개 보류는 유지한다.
- Unity 종료 후 DEV fb39d3dd 통합. 병합 커밋52f39e4b, 작업 브랜치 fix/common-rule-regressions. 기존 Task112/다른 문서/탈출로 캐시/복구 백업은 제출 제외.
- 숙소 모서리 오접지·탐색 자격/화면 이용자·추격 해제 제어·같은 방 런 재시작 자금 문제 수정. 집중93검사와 실제 의료 재예약/자금 재시작 검증은 [기존 실행 결과](runtime-play-results.md).
- 모션11항목과 현재 검사 결과는 [최종 감사](completion-audit.md). 최초 모션6개 수정 전 실패→후 통과, 추가9개/캐릭터36개/리허설120개/공통21개 통과. 네 캐릭터×부상4단계 걷기·점프·착지와 구조 손 접점 확인.
- 최신 독립 실행 파일 2·3·4인 Relay로 이동·점프·밤→아침 확인. 4인은 목마 두 쌍·스위치/대포/트램펄린 점유자 사망·복귀·재사용 확인. 안정 상태14개에서 불일치0. 다른 PC 원본 끊김은 미확정.
- 대포 개발용 봇에서 공통 봉 등록 빈 칸의 NullReference를 실제 로그로 확인해 수정. 공중 입력은 옛4.5 대신 공통 설정 사용. 검사 fixture의 리프트 탑승·도착 LateUpdate·수신 위치 준비를 바로잡고 최신 봇으로 실제 역할 교대/발사/반동/R재시작/접속 종료1검사 통과.
- [Confluence 변경 전후51행](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/40534020/11+10) 생성. 폴더37355775, wide/표1400px, 번호·장소·전·후·셀 글머리 기호를 읽어 확인.
- 전체 EditMode1954/1968, 대포64/80, 외줄63/99(1생략)는 최초 넓은 검사 수치다. 이후 집중 재검사와 혼동하지 않으며 전체 통과/모든 버그 해소로 보고하지 않는다.
- 증거 폴더: .harness/local/common-rules-completion-20261111. 임시 QA 프로브는 Assets 밖에 있고 네 플레이어는 정상 종료했다.
- 마지막 집중 재검사: 빠른 톱4/재접촉1/봉 접근1/외줄 전멸 부활1/시간 종료1 통과. 게임 규칙을 바꾸지 않고 숨김 봉·공통 피해 간격·입력 프레임·정산 연출 대기시간을 검사 준비에 반영했다.

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출 직전 Unity 비재생·미저장0 확인 후 정상 종료. 14:16 KST origin/dev fetch: fb39d3dd 동일, HEAD에 포함됨. 최신 집중 검사 결과 재사용. 임시 Pipeline 생성 폴더와 preload 설정 복구, 테스트 플레이어 전부 종료.
- 관련52파일6b772dae 저장·push, [PR324](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/324)를 dev 대상으로 제출하고 gaboja-studio/pm 검토 요청. 직접 병합 없음. 무관한 변경·생성된 QA 스크린샷은 로컬 보존·제출 제외. 이 제출 기록은 문서만 후속 저장한다.
