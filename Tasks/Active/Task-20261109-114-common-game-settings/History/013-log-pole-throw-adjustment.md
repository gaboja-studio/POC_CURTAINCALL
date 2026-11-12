## 11/11 도약 거리 수치 재조정
- 사용자 보고: 모션·왕복은 만족하나 최대 반동 도약이 과도함. 튜토리얼 다음 그네 정도로 줄이고 수치만 변경해 검사 없이 즉시 제출 요청.
- Unity 종료 후 origin/dev b7d9af06 반영. PoleSettings.asset horizontalSpeedLimit만10→3.5m/s로 수정. 코드·씬·반동/각도/감쇠/상승속도6m/s 변경 없음. 런타임과 공중 조절은 기존 공통 상한을 사용한다.
- 검사/컴파일/플레이: SKIPPED(사용자 명시 지시). 다음 그네 도달 감각은 미검증. 이전74개 통과를 이 수치 변경의 검사 결과로 확대하지 않는다.
- 저장·제출: c8fd9839, [PR329](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/329), dev 대상·PM 팀 검토 요청. 병합하지 않음.
