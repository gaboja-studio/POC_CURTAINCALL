# Log

- 최신 origin/dev fa86538e에서 작업 브랜치를 생성했다. 외부 에셋은 DEV가 가리키는 비공개 커밋 1290bdf6를 사용했다.
- 패키지의 Mat_DissolveBurn이 실제로 사용하는 Shader Graphs/DissolveShader의 프로퍼티는 _DissolveAmount다. 밧줄 아틀라스 TouringRigging을 유지하고 데모용 표면 맵의 영향을 제거했다.
- Unity 프리뷰에서 기본 소실 진행을 사용하면 경고 90%에 가시 픽셀이 0개였다. 경고 곡선을 조정한 후 같은 메시에서 경고 98%에도 끝 가닥이 남는 것을 확인했다.
- 에디터 검사에서 씬 인스턴스 전용 머티리얼, 부분 소각 3곳과 안전 범위 4곳, 이웃 줄 원본 재질, 연속 지지 콜라이더 1개, 예고 중간 상태 적용 및 해제 후 원본 복구를 확인했다.
- 초기 doctor 실행 시 기존 Task 식별자 중복과 전환 전 작업 브랜치/에디터 경고가 있었다. 이후 전용 에디터 연결 및 현재 Task 식별을 별도로 확인했다. PlayMode는 실행하지 않았다.
- 화재 순서 EditMode 22건, 소각 후 탈출 경로 EditMode 2건이 통과했다. 실제 불티 파티클은 에디터에서 생성·상승했고 소각 시점에 방출이 멈췄다. 작업용 에디터에 Tightrope 씬을 열어 두었다.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출 전 origin/dev 86eeabe7을 반영했다. DEV에 먼저 등록된 탈출로 Task와 겹친 외줄 작업 번호를 162로 정리했다.
- DEV 317ecfbf까지 동기화했다. 에디터 종료 때 갱신된 폰트·라벨 4개의 Git 객체가 최신 DEV와 모두 일치하는 것을 확인했다.

- 2026-11-18 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/429 (feat/tightrope-dissolve-burn → dev)
