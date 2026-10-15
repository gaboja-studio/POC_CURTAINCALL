# Handoff

## 지금 상태

- 단계: `integrated` (2026-10-15 통합 공동 테스트 PASS). PR [#66](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/66) 병합됨. 통합 균형 보완·기록은 이번 PM 준비 저장 대상, dev 반영 전이므로 done 아님
- 동작: 공용 시점 preset·요청 우선순위·전환, 1인칭 pose/마우스/커서·로컬 모델 render 숨김, 시점 구역, 외줄 구도 MainStage 이전(GUID 유지)·가림 예외, Fallen/Retired 관전
- 10-15 사용자 요청으로 CameraViews에만 bootstrap 연결·저장, 로컬 접속/재시작 시 균형 비활성. 공용 Player·실제 외줄은 변경하지 않음
- 작업 구역·공용 파일 소유 반납(setup 이력 보존). 테스트 씬은 기존 PM 방침대로 유지, 전체 테스트 씬 마련 후 일괄 처리

## 다음 할 일
- 035 배정 문서 sync → 032 연결. 10-15 사용자 인계 준비 저장·동기화 승인, 1인칭 이동은 별도 fix Task-045 준비
- 10-15 사용자 FPS 이동 제보: 기본 Platform이 ViewForward를 소비하지 않아 코스축으로 이동하는 연결 누락 확인(코드 조사, 새 실행 재현 없음). 통합 `first-person-movement-issue.md` 참조, 별도 fix Task-045(CC-131) 준비·미구현. 3인칭 유지. 입력 차단 연결·TightropeSettings 카메라 칸 정리·Confluence 갱신도 후속이며 이번 PASS가 1인칭 이동 UX 완료는 아님
- CLI beta.6 status 검색 불일치: 프로젝트 경로를 지정한 직접 명령 사용. 자동 테스트 뒤 dirty 씬/대화상자·빌드 설정 차이를 확인하며 현재 플레이는 임의 종료하지 않음

## Verification

- 시작 doctor: WARN(FAIL 0, WARN 3), 같은 작업 중 재실행 안 함
- 구역: SKIPPED(PM 통합 브랜치, exit 0). 작업 브랜치 당시 구역 검사는 PASS
- 1 문서/규칙: PASS(10-15 이번 결과 기록·035 인계 준비 수정 후 verify-fast, 기존 경고 4·hard 0). 선택 문서 git diff --check PASS
- 2 컴파일: PASS(10-15 통합, 외부 에셋/에디터 재시작 복구 후)
- 3 테스트: PASS — EditMode 46/46·PlayMode 9/9, 실패·생략 0. 균형 회귀는 수정 전 8/9 FAIL → 수정 후 PASS(접속·재시작 후 각 15초 false·Normal). 상세·원본은 통합 result.md
- 4 실행 로그: 자동 검증 직후 Missing 0·Console 오류/경고 0. 브리지 누적 서비스 인증 오류 2건은 별도 이력이며 최신 재검사로 꾸미지 않음
- 5 공동 테스트: PASS — 2026-10-15 사용자: 외줄 구도·사망 관전·가림, 구역 1/3인칭·모델·마우스, A/B 상대 화면 모델, 거리·높이·FOV 즉시 반영·원복, 플랫폼 이동·점프, 리타이어 관전·대상 없음·복구 모두 성공

## 공동 테스트 결과
- 일시: 2026-10-15(리타이어·복구 승인 후 15:15 +09:00 기록)
- 참여자: 사용자 PM @MoHoDu(2인 A/B 화면 확인), AI(호스트 런타임 API 호출·상태 조회). 원격 조작자 신원 미기록
- 재현: 통합 CameraViews LAN 2인, A 1인칭·B Normal → A만 ServerSetState(Retired) → B도 Retired → ServerRestartAll(). 앞서 proto 외줄·시점 설정·역할 교대 모델 표시 확인
- 예상: Retired는 Normal인 상대만 3인칭 관전, 모두 Retired면 대상 없음, 복구하면 관전 해제·각자 자기 카메라/조작/구역 전환 복귀
- 실제: 사용자 “A 관전 성공”, “대상 없음 확인”, “복구 테스트 성공”. 호스트 조회 A 관전→B(slot 1), 모두 Retired→Target null, 복구→양쪽 Normal·관전 false·로컬 균형 false/HasFallen false. 실제 탈출로 잡힘 입력·전체 런 흐름 검증은 아님
