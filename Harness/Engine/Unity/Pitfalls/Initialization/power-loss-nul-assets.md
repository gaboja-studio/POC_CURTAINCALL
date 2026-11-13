---
summary: 갑작스러운 전원 종료 뒤에는 Library 복구 경고와 실제 에셋의 NUL 손상을 구분하고 GUID를 보존해 복구한다.
status: active
updated: 2026-11-13
source: Task-20261103-100-exit-route-pursuer
---

# 전원 종료 뒤 에셋이 읽히지 않을 때

Unity의 복구 씬은 마지막 저장 씬보다 오래될 수 있다. 복구본을 별도로 보관하고 저장 씬·프리팹의 내용과 참조를 비교한 뒤 선택한다. 복구본을 곧바로 정본에 덮어쓰지 않는다.

2026-11-13 점검에서 Library/StateCache의 빈 JSON과 BuildProfileContext 경고 외에, 실제 Settings.asset 1개와 prefab.meta 1개가 전체 NUL 바이트로 손상돼 있었다. 크기가 0이 아니므로 빈 파일 검사만으로 발견되지 않았다.

1. 원본 손상 파일을 Assets 밖 로컬 백업에 보관한다.
2. Git 연결성과 작업 에셋의 전체 NUL·텍스트 내부 NUL을 확인한다. 비밀값이나 파일 원문을 검사 출력에 포함하지 않는다.
3. 추적 에셋은 확인한 Git 버전으로 복구한다. 미추적 프리팹의 meta는 기존 저장 씬 참조에서 원래 GUID를 확인해 보존한다.
4. Library 경고는 재생성 가능한 캐시 문제인지 별도로 판단한다. 실제 프로젝트 에셋을 캐시처럼 삭제하지 않는다.
5. Unity 재로드 후 컴파일, 저장 씬/프리팹의 Missing Script·Missing Prefab·필수 참조, 실제 Play 초기화를 검사한다.

Kernel-Power 41과 EventLog 6008은 비정상 종료 증거이며, 특정 Unity 코드가 원인이라는 증거는 아니다.
