# 남은 확정 상세 연결 묶음

2026-10-22 사용자: 가능한 작업부터 짧은 단계로 마무리, 두 PC 보류. 치료는 현재 복구 여부로 완료, 미수령·미이용은 쉬는 시간 종료 확정·중도 이탈 분리. 통로 막힘은 최초 대기실 이동 요청임을 설명하고 기준 미정으로 보류.

## 구현

- 기존 승인 HumanCannonHazardCourse.cs에 ContactResolved(player,hazard,cut,position,evaluated)를 추가했다. 원래 ServerCut 호출 조건/횟수·HitConfirmed·ContactDetected·지속 접촉 중복 방지는 유지한다. 같은 프레임에 추가로 닿은 함정은 FRAME_LIMIT, 판정 후 무절단은 NO_CUT, 실제 절단은 CUT으로 관측한다.
- PerformanceLogAdapter의 CANNON_HAZARD_CONTACT에 실제 접촉 위치·구간·장애물·부위·결손 수·결과를 기록한다. 치명상 정산 직후 사건도 같은 시도에 보존한다. 도식은 부상 여부에 따라 사물/부상 아이콘, 상세와 툴팁은 결과를 표시한다. 일반 함정 판정 외 임의 벽/장식물 전체 충돌은 포함하지 않는다.
- WaitingRoomLogAdapter가 StepChanged 전환에서 이전 breakId 문맥의 BREAK_ENDED를 1회 발행한다. HOST는 당시 접속 플레이어의 networkClientId 목록을 보존한다.
- 집계는 같은 앱·방·런·쉬는 시간의 HOST 구매→수령 및 LOCAL 의료 예약→입장을 연결한다. 종료가 관측된 남은 수량/예약만 확정하고, 취소/플레이어·게임·런 종료는 별도 사유로 분리한다. HOST 종료 스냅샷에서 구매자의 연결 ID가 없으면 퇴장으로 구분한다. 앱이 끊겨 종료를 관측하지 못한 공백을 실패로 꾸미지 않는다.
- 운영 요약은 확정 거절 5종(HOST), 운반 3종/서비스 이탈(LOCAL)을 eventId별 한 번 집계한다. 환경·게임 버전·런 필터를 그대로 적용한다. LIVE/TEST를 혼합하지 않는다.
- 대시보드 ‘실패 · 운반 기록’, ‘미수령 · 의료 미이용’ 표 추가. 새 구역·기물·게임 규칙·가격·판정은 만들지 않았다.

## 배포

- 기존 수신 URL에 v13(02:26 KST), 본인 전용 대시보드 URL에 v14(02:30 KST). v13 수신은 68종 새 계약, v14는 의료 취소 별도 집계/한국어 작업명까지 포함한다. 접근·token·30분 트리거·OAuth 범위 변경 없음.
- Drive 설치 소스 2개는 같은 ID/parents로 갱신. editor 소스 readback 일치 후 배포했다. 새 이벤트를 담은 대기 배치는 다음 게임 실행에서 기존 재전송 경로로 전달된다.

## 최소 검사

- Scope origin/dev PASS, Fast PASS(기존 길이 경고), Unity Compile PASS. 기본 check-work는 이전 dev 수신 구역 FAIL 상태와 구분하며 반복하지 않았다.
- 기존 대포 실제 접촉 전용 PlayMode 1/1(12.71초): 접촉·장애물·실제 절단·정산 시도 일치, 지속 접촉 1회·재접촉·4번째 사망까지 확인. NO_CUT/FRAME_LIMIT는 계약·코드 경로 검사이며 실제 다중 장애물 접촉 조작 검증은 아니다.
- 기존 대기실 전용 PlayMode 1/1: Tick 만료와 이전 D1T2 종료 1건 확인. 이후 종료 스냅샷에 접속 ID 목록만 추가한 변경은 재컴파일로 확인하고 플레이를 반복하지 않았다.
- Node 56/56(0.11초): 원천 계약·피해 중복 금지·실패 권위·운반 중복·부분 수령/의료 입장·다른 앱/런/쉬는 시간/버전 제외·의료 취소·퇴장·관측 누락 구분.
- 두 PC·물리 입력·PC 전체 오프라인·전체 테스트/실제 신규 POST 왕복은 생략했다. 기존 프로젝트 목마 점프 기대값 불일치도 전체 통과로 바꾸지 않는다.
- 자동 생성 폰트·GripYellow는 이번 명시 승인으로 원복. pole-gate-black/section-sign은 이미 승인한 동일 _Color 소수점 자동 변경만 원복했다. 사본은 .harness/local/telemetry-final-*에 보존한다. 씬·설정·기존 아트 의도 변경 및 커밋/push 없음.
- 증거: .harness/local/telemetry-final-{cannon,waiting}-20261022.json, telemetry-code-v14.txt.
- Google scheduledRefresh 02:32 KST 정상 완료. .harness/local/telemetry-final-google-refresh.png 저장. 새 UI는 도구 연결 프레임 about:blank로 화면 확인 미완료이며 배포·서버 실행 완료와 구분한다.

## 후속 표시 확인(2026-10-22)

- 사용자 지시대로 두 기획 항목/두 PC는 보류하고 실제 배포된 기존 탭만 읽었다. 코드·배포·데이터 변경/게임 검사 없음.
- ‘실패 · 운반 기록’, ‘미수령 · 의료 미이용’ 두 섹션 모두 실제 화면에 표시된다. TEST/0.1.0은 해당 사례가 없어 ‘기록 없음’으로 표시된다.
- 버전 미기록 선택에서 기존 검증 기록의 실패 표 6행(구매·수령·상호작용·의료 예약·입장·보관)과 사유/횟수가 렌더링된다. 미수령/의료 미이용은 종료 확인 사례가 없어 ‘기록 없음’이다. 실제 새 미이용 건의 수치 검증으로 해석하지 않는다.
- 확인 후 게임 버전을 0.1.0으로 복귀했다. 증거 .harness/local/telemetry-final-visible.png. 이번 합의 범위의 구현·배포·표시 확인을 마무리하며 보류 기획/실플레이와 커밋·통합은 별도다.
