# M2 버전과 설문 범위 — 2026-10-26

- PM: M2 버전을 올려 로그를 구분하고 M1 설문은 제외. 후속으로 정확 버전 대신 패치 범위, 원하는 폼·회차 조절 요청. 새 빌드에 적용하는 방식으로 확정했다.
- Unity PlayerSettings.bundleVersion=0.2.0. 현재 PC LOGS=true, SURVEY_010=false. ProjectSettings의 버전 외 변경 없음. 다른 PC는 태그 확인 필요.
- 새 설정 화면과 비밀값 없는 공유 JSON. 정확 버전/0.1.x/*, 로그·설문 각각 선택. 밤 일차/퇴장 두 시점의 폼·ID·회차 설정. 같은 패치는 기존 최초 실행을 유지하고 회차 변경 시 재실행한다.
- 실제 gameVersion을 CCS2 응답에 보존, M1 CCS1·기존 마커 호환. Google 코드에서 새 회차·추가 원본·설문-only 버전 캐시를 처리한다. 통계 시트와 대시보드 회차 분리.
- 기획자 [설정 안내](../../../../Harness/Project/Guides/Log-system/versioning.md), 개발자 [계약](../../../../Docs/Log-system/survey-campaigns.md).

## 검사
- 컴파일 오류0. EditMode SurveyPolicyTests 20/20. 패치 경계, M2 제외, 새 회차 재열림, 옛 캐시 유지, 실제 패치/회차 코드 확인.
- PlayMode SurveyBootstrapTests 4/4. M2에서 M1 컴포넌트 생성 차단, M1 설문-only 부트스트랩, 호스트 종료/런 미진입 회귀. 브라우저를 열거나 실제 응답을 제출하지 않았다.
- Node 관련 84/84. CCS2·구형 응답, 회차별 통계·저장본 필터, 로그 없는 새 버전의 Drive 캐시 확인.
- 빌드/Google 배포/commit/push 없음. 기존 다른 Task 문서 이동 때문에 전체 구역 검사는 계속 실패하며 해당 기존 변경은 보존한다.
