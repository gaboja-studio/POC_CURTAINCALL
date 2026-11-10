# 추가 공통화 검증

2026-11-10 · Task-114 · refactor/common-game-settings · origin/dev 2d591802

## 완료한 검사
- 새 공통 규칙 EditMode 최종 **18/18 통과**: 3m/5m 경계, 1초 간격, 화염 종류, 벽/층/몸 중심 거리, F 한 번의 소비, 공통 연결 누락, 다른 씬의 기물 재생성, 구조 거리의 공통값 변경 반영.
- 전체 EditMode **1,860개 중 1,845 통과 / 15 실패**. 전체 통과로 기록하지 않는다.
- 위 실패 중 새 줄 끝 검사 1개는 테스트의 좌표 중복 보정 문제를 수정했고 **외줄 집중 69/69 통과**로 확인했다.
- 나머지 14개: 보류한 바닥 톱 높이1, 난이도/휴식9, 추격자 배치1, 보상1, 문구2. 이전 묶음과 동일한 실패다. 깨끗한 dev 전체를 별도 프로젝트로 재실행한 것은 아니다.
- 전체 실패 이름·메시지·후속 조치는 [CSV](Inventory/followup-verification-failures.csv)에 기록했다.
- 남은 14개의 관련 씬/시스템·기대값·실제 결과·원인: [상세 표](Inventory/remaining-test-failures.md). Confluence 변경 전후 문서에도 같은 표를 추가했다.
- PlayMode 공통 묶음 **18/18 통과**. 일반 낙하 실제 3m·5m/순간이동 초기화, 위험물 1초·즉사 화염, 봉/바닥 목마/외줄 이동, 매달림 실패·시간 만료, 일반 낙하·대포 착지 신발 반동을 확인했다.
- 최초 PlayMode 7개 중 2개 실패는 봉의 옛 무피해 기대와 신발 검사 장부 초기화를 수정해 위 18개에서 재확인했다.
- 추가 구조 거리 정리 후 구조 PlayMode **4/4 통과**. 최초 재검사 3/4에서 위치 차이 8cm가 나와, 이동 입력 즉시 초기화와 수평 위치 유지를 각각 확인하도록 검사했다. CharacterController의 줄 위 수직 보정을 이동 입력으로 오인하지 않는다.
- 검사 이름을 파일명으로 지정한 실행 1회는 **NO_PROJECT_TESTS**였다. 통과로 세지 않고 실제 테스트 이름으로 위 4개를 다시 실행했다.

## 에셋·제작 도구
- Unity API로 8개 씬·4개 프리팹·2개 기존 설정의 옛 저장값 정리. Base 공통 피해 설정도 저장.
- 재직렬화 대상 씬/프리팹 12개는 git diff에서 위치·회전·크기 변경0.
- 공통 설정 검사: 연결8개·봉 외형2개, 폐기 필드64개, 제작 도구 참조, 옛 저장값. 마이그레이션 뒤 오류0.
- 최종 check-work: **안전성·문서/규칙·컴파일·공통 설정 검사 통과**. 자동 테스트는 위 별도 실행 결과를 사용하며 이 명령에서 반복하지 않았다.
- 첫 check-work는 Unity 컴파일 자체는 통과했으나 도구 응답의 중첩 result를 잘못 해석해 실패했다. 응답 파서를 수정하고 정상/연결 누락/실행 실패 3가지를 확인한 뒤 최종 검사를 통과했다.
- 테스트가 만든 CheerGauge/CommonButtons 폰트 캐시는 Unity API로 정리해 diff0, escape-labels는 작업 전 사본과 해시 일치를 확인했다.

## 증거와 한계
- `.harness/local/followup-focused-final.json`, `followup-edit-final.xml`, `followup-rope-focused.json`, `followup-play-final.json`, `followup-rescue-final.json`, `followup-migration-last.json`.
- 최종 작업 검사: `.harness/local/followup-check-work-final.txt`. 변경 파일별 목록307개: [CSV](Inventory/changed-files.csv). 이전 단계부터의 누적 목록이며 기존 사용자 변경을 이번 추가 구현으로 세지 않는다.
- 전체 검사 JSON은 도메인 재로드 뒤177개만 개별 기록을 남겨서, Unity TestRunner의 원본 결과 트리를 XML로 복구했다. 전체 수/실패 목록은 XML을 기준으로 한다.
- 실제 호스트 플레이와 지연 보고 모의 검사는 두 PC 동시 플레이를 대체하지 않는다. 전 코스 완주·부상별 높은 버튼 동선은 수동 확인 대상.
- UI 외형, 톱 높이, 운반 기물 회수 정책은 변경하지 않았다. 위 검증 후 사용자 저장·제출 승인으로 commit/push 및 dev PR을 진행한다. 실제 제출 링크는 [작업 기록](log.md) 참조.
