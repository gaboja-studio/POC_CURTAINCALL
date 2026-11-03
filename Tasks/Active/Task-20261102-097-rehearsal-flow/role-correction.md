> 실물 비교에서 인간 그네 원본 일치가 미완료임을 확인했습니다. 현재 공연에서는 Relay 모드가 회전봉을 비활성화하고 TightropeTrapeze를 사용합니다. 아래 원본 재사용 완료 표현은 정정합니다.

# 역할 구분과 잘못된 추가 철회

최신 사용자 정정 기준. 이전 full-course-and-night.md의 공연 퀘스트·중간 발판·전용 소품은 철회된 구현이다.

- 실제 공연의 대기실 퀘스트 분기·문구·폰트 연결을 제거했다. 대기실의 리허설·안내·밤 정비 퀘스트만 유지한다.
- 중간 지점에 만든 발판과 별도 받침·트리거 틀·점선 소품 생성 코드를 제거했다.
- 직접 반동·놓기·공중 잡기로 이동하는 인간 그네는 공연 TightropeSwingPole, 잡은 동안 동료 줄을 잇는 트리거는 공연 TightropeRelayGate를 사용한다. 2인 몸 교대 전용 장치로 임의 교체하지 않는다.
- 실제 Tightrope 씬의 코스 원본은 Interactables/Tightrope/TightropeCourse.prefab이고 TightropeArtProfile을 연결한다. 리허설의 Prepared 변형도 같은 원본과 같은 프로필 참조다. 리허설 전용 아트 에셋을 새로 추가하지 않았다. 남아 있는 그네 틀/색 표시는 원본 공연 컴포넌트가 그리는 표시다.
- 이번 정정에서는 줄 수·그네 수·연결 쌍을 다시 바꾸지 않았다. 애매한 기획 변경은 사용자 확인 뒤 진행한다.

## 필요한 확인 결과
- 실제 키 입력: 1/1 PASS(56.3초). 균형바·옆줄 점프·추락 복귀·직접 조작/연속 그네·트리거 연결/해제.
- 공연 퀘스트 부재와 대기실 안내→집결→둘째 공연→밤→Day 2: 1/1 PASS(87.9초). 공연에서는 FirstDayQuestVisible=false를 검사한다. 공연 종료는 기존 공개 신호, 취침은 침대 상호작용을 사용한다.
- 최신 배치 촬영: 1/1 PASS(58.8초). 검토 카메라와 임시 조명으로 실제 코스만 촬영했다. 협동 전 코스 완주 검사는 아니다.

1단계 · 중간 발판 제거

![1단계 · 중간 발판 제거](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Role-correction/01-stage1.png)

2-1 · 직접 조작 그네

![2-1 · 직접 조작 그네](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Role-correction/02-stage2-1.png)

2-2 · 연속 잡기

![2-2 · 연속 잡기](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Role-correction/03-stage2-2.png)

2-3 · 동료 줄 연결

![2-3 · 동료 줄 연결](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Role-correction/04-stage2-3.png)

3단계 · 역할 종합

![3단계 · 역할 종합](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Role-correction/05-stage3.png)

실제 공연 화면 — 대기실 퀘스트 없음

![공연 퀘스트 제거](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Role-correction/performance-without-quest.png)

최종 check-work: 안전성·문서/규칙·컴파일 PASS(exit 0). 자동 테스트는 추가하지 않았다. 커밋·push 없음.
