# Task-20261105-110

- **Title:** 플레이테스트 설정·정산 규칙·캐시 초기화·추적자 경로 개선
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/playtest-settings-navigation
- **Base:** dev

## Context
플레이테스트 후속 개선 7건: 쉬는 시간 기물 구매 허용, 빌드 설정 화면의 개인 캐시 초기화, 신체 복구 기본 단가 50(+25 유지), 공연 사망 안내를 개인 호응 절반 차감으로 수정, 아침·가이드·공연 사이 쉬는 시간 모두 300초, 공연 성공 시 연결된 모든 생존자를 완주 처리/보너스 지급하되 실제 최종 거리 보존, 추적자 정찰 장애물 우회·도달 불가 목표 보정. 조정 가능한 값은 설정과 쉬운 가이드로 제공한다.

## 계획
1. 기존 설정·구매/정산 진입점·설정 UI·추적자 NavMesh 구성을 조사한다.
2. 구매 가능 시간 설정, 서비스 단가 및 쉬는 시간 에셋 값을 적용하고 잘못된 사망 문구를 고친다.
3. 개인 캐시 초기화 버튼과 명확한 초기화 범위·결과 안내를 연결한다.
4. 생존자 완주·보너스와 실제 거리 보존을 정산 모델에서 함께 검증한다.
5. 정찰 직선 이동/제자리걸음 원인을 재현하고 기존 NavMesh 경로·도달 가능 지점 조정·정체 복구를 보완한다.
6. 설정을 직접 바꾸는 메뉴/에셋 위치와 값을 짧은 가이드로 작성한다.
7. 관련 회귀 검사 및 완료 시 check-work 1회, 문제·결과·미검증 범위를 기록한다.

## 미정·주의
- 담당 @MoHoDu, 최신 origin/dev eb086e19 기준 별도 feat/playtest-settings-navigation 생성. 후속 사용자 요청에 따라 최신 dev를 반영하고 관련 변경 commit/push 및 dev PR까지 진행한다.
- Unity 종료 확인 후 브랜치 생성, 이후 Editor 실행. doctor는 Task 중 1회 수행했으며 기존 Task 번호 중복과 샌드박스 내 Unity/외부 에셋 검출 실패를 기록한다.
- 기존 escape-labels.asset 변경은 .harness/local/playtest-settings-navigation/initial-escape-labels.asset에 원본 백업, SHA256 D47C06125FDC23D7978DA433677FCEB79BADE30CD972734F26929A1C4B1459DA 보존.
- 밤 정비 시간은 유지한다. 사용자 확인: 캐시는 설문·안내 이력만 초기화하고 조작·음량 설정과 플레이 로그는 보존한다.
- 기존 공개 프리팹/GUID 및 외부 에셋 원본은 보존한다. 설정 에셋은 Editor의 SerializedObject로 바꾸고 필요한 에셋만 저장한다.
