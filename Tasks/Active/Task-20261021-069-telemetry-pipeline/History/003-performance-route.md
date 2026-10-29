# 공연 좌표 도식 (2026-10-21)

## 구현
- 사용자 요청에 따라 대기실·기타 타임라인은 현재 목록 유지, 공연 2종만 좌표 도식으로 교체.
- 월드 +Z 진행 방향을 가로로, 외줄 X/대포 Y를 세로로 표시. 좌측 출발 방향·우측 도착 방향. 실제 코스 경계가 로그에 없으므로 관측 좌표 범위를 도착점으로 오인하지 않도록 방향 표기.
- 플레이어 번호 1~4별 고정 색, 이동·부상·사망·팀원·사물 5종 SVG 아이콘. 호버/키보드 포커스는 시간순 사건 연결·동일 색 선·다른 플레이어 채도/투명도 감소·핵심 결과 툴팁.
- 클릭/Enter/Space 상세 고정, 다른 사건 선택·외부 클릭·닫기·Escape 해제. 원본 JSON은 접혀 있으며 긴 상세는 줄바꿈/스크롤. 도식 최소 1120/최대 4800px·영역 내부 가로 스크롤.
- 좌표 없는 사건은 접힌 목록에 남기고 그 사건 전후의 동선을 끊음. 겹침 아이콘은 분산하되 정확한 좌표 anchor/leader와 동선 원좌표 보존.
- TelemetryRuntime/DetailLogIds는 공연 사건에 position이 없는 경우 발행 시점 플레이어 좌표 및 positionSource=EMIT_OBSERVATION 추가. 이미 존재하는 원천 좌표·명시적 null은 보존.
- core.gs 도식 대상에 공연 시작/이탈·부상 변경·상호작용 실패 포함. 이전 v1 배치와 호환, 수신 v3 변경 불필요.

## 검증
- Node 35/35: 기존 28+좌표 투영·미수집/불량·색/아이콘 분류·시간순·동선 단절·겹침 분산·도식 크기·추가 사건 7건.
- Unity 컴파일 PASS, 로그 EditMode 27/27, LAN 공연 PlayMode 1/1(시작 좌표·positionSource assertion 추가). 합성 정산이며 실제 물리 입력 검증 아님.
- 로컬 예시 외줄/대포: 플레이어별 동선/다른 플레이어 흐림, 절단/사망/팀원/트램펄린 아이콘, 클릭 상세·배경 클릭 해제, 키보드 포커스, 도식 가로 스크롤 scrollLeft=43 확인. 예시 데이터는 Google에 업로드하지 않음.
- 증거 .harness/local/performance-route-rope-preview.png, performance-route-cannon-preview.png, telemetry-route-editmode-20261021.json, telemetry-route-playmode-20261021.json.
- PlayMode 후 GripYellow.mat의 _Color.b 0.07999996→0.079999976 자동 소수점 변경 관측. 이전 사용자 명시 승인한 자동 변경 원복 범위와 일치하는 1줄만 검증해 복원. 씬/설정/원본 아트 추가 변경 없음.
- 전체 EditMode는 이전 dev 목마 점프 기대값 1건 실패가 남아 있으며 이번에 전체 반복 검사를 통과했다고 보고하지 않음.

## Google
- Drive 기존 설치 소스 code.txt/dashboard.html.txt 갱신 완료, 같은 IDs/parents 유지.
- Apps Script HTML 선택·복사 readback이 로컬 HTML과 줄바꿈 정규화 후 일치. Code.gs는 빈 줄 1개를 제외한 소스 일치 확인.
- 기존 본인 전용 URL에 버전6 배포 완료(22:43 KST). 권한 나만·실행 사용자·OAuth·token·수신 v3·트리거 변경 없음.
- doGet 직접 실행 완료/오류 없음. 배포 직후 iframe 빈 로딩이 반복됐으나 재로딩 후 실제 UI 정상 표시 확인. 새 합성 공연 테스트 로그를 실제 HTTP ACK(pending 0) 후 TEST 259건으로 적재했다. 최종 도식은 handoff에 기록.
- 실제 v6의 최신 런 1일차 공연에서 시작·구간 진입/이탈 위치 아이콘 3건 확인. 공연 시작 클릭 상세에 (-7.5,0,-1), 원본 positionSource=EMIT_OBSERVATION 확인. 실제 도식/상세 증거 .harness/local/performance-route-google-v6.png.
