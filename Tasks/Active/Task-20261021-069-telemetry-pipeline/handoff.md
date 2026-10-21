# Handoff

## 지금 상태
- dev 12267d7 FF 수신, feat/telemetry-pipeline → integration/log-system. 기존 88개 파일 해시 보존. 사용자 요청으로 전체 로그 작업 로컬 커밋 대상으로 확정, push·PR·병합 없음.
- Google 대시보드 v14(02:30 KST)·수신 v13: 실패/운반·미수령/의료 미이용·대포 일반 함정 무절단 접촉 추가. 게임 버전 분리·기존 URL·권한·30분 트리거 유지.
- 같은 앱의 저장 완료 사건을 연결해 구매/배송/수령/보관·스케줄/준비·의료 단계별 시간 추가. 누락/다른 방·런/시각 역전은 null, 최근 2,048개 기준 유지.
- 시설 성공 예약에 구매자·조정·적용 일차·지불액 기록. 원천 구독자 예외 격리, 의료 흐름과 분리.
- 배송→다른 플레이어 수령을 HOST 수령 스냅샷으로 같은 시계에서 측정. LOCAL 이용 횟수 유지, 실제 원격 콜백은 다인 검증 대기(History/005-dev-remote-receipt.md).
- 평균 소요 시간: 상점·운반/편성·준비/의료/시설 4개 탭·18단계. 누락 제외·실제 0 보존·호스트/본인 수령 중복 표본 방지(History/006-stage-time-dashboard.md).
- Drive 설치 소스 2개 같은 ID 갱신, HTML readback 일치·doGet 완료. 사용자 Chrome에서 평균 소요 시간 섹션 표시 확인(2026-10-22). 값별 대조·두 PC 검증은 별도.
- 단일 PC 격리 큐의 저장/재생성·전송 실패 보존·Google ACK 및 동일 원문 재POST 확인. Drive 1개·두 번 가져온 후 Events/Batches 각 1행(History/007-offline-replay-verification.md).
- Application.version(이미 0.1.0) 자동 기록·원래 버전 재전송 보존. Events V열 및 LIVE 요약 5개 시트 버전 열, 대시보드 전체 필터·과거 무버전 분리(History/008-game-version.md). 실제 Events 760행 0.1.0·Google 갱신 완료, Google 서버 소스 readback 일치.
- 이전 쉬는 시간 BREAK_ENDED 1회, 구매→수령/예약→입장을 같은 앱에서 연결. 종료 미관측은 미확정, 정상 종료·취소·중도 이탈 분리. 치료는 사용자 결정으로 현재 복구 여부까지 완료(History/009-final-detail-batch.md).
- 기획자 운영 안내: Docs/Log-system/operator-guide.md·reading-guide.md. 재부팅 후 열람·빌드별 환경·자동 전송·종료 대기·지표 분모·공연 도식 해석 포함.

## 다음 할 일
- [기획/원천/사람 검증 분리](../../../../Docs/Log-system/remaining-work.md)를 따른다.
- 통로 막힘·우회는 콘텐츠/기준 미정 보류. 치료 비교는 현재 복구 여부로 완료, 미수령 마감은 쉬는 시간 종료로 확정·연결 완료.
- 외줄 발판/지속 손잡기·서버 미도달 실패의 확정 원천 필요. 일반 함정 밖 벽/장식물 전체 충돌은 미연결.
- CODE 다인·재접속·실제 프로세스 강제 종료/PC 전체 오프라인·물리 입력/마이크/의료·공연 공동 검증은 보류.

## Verification
- 0 구역: 기본 check-work FAIL(dev 수신 구역 77건). 별도 origin/dev 기준 PASS, 기본 전체 통과 아님. 실행 자동 에셋은 승인 범위 원복.
- 1 문서/규칙: 별도 Fast PASS(기존 길이 경고).
- 2 컴파일: 별도 Unity Compile PASS(대포 접촉·쉬는 시간 종료/접속 스냅샷).
- 3 테스트: 별도 PASS(대포/대기실 전용 PlayMode 각 1/1·Node 56/56). 기존 outbox/버전 회귀 반복 안 함, 전체 게임 검사 생략.
- 4 실제 Google: PASS(v14 새 두 섹션 실제 표시). TEST/0.1.0 빈 상태·버전 미기록의 실패 요약 6행 및 미수령/의료 미이용 ‘기록 없음’ 확인 후 0.1.0 복귀. 표시만 확인했으며 실제 입력/새 미이용 사례의 값 검증은 SKIPPED.
- 5 공동 테스트: SKIPPED(CODE 다인·실플레이 미진행). 기존 전체 EditMode 목마 점프 0.9/1.35 실패는 미해결이며 전체 통과 아님.
- 저장 직전(10-22): doctor WARN(차단 없음)·origin/dev 기준 Scope PASS·Fast PASS·Unity Compile PASS(up_to_date), 씬 dirty 0. 자동/실플레이 재검사는 요청대로 SKIPPED. 기본 save-work -DryRun은 이전 dev 유입 77건을 구역 위반으로 오인해 FAIL. 도구/규칙·메타 Integration 변경 없이 현 기준 개별 검사 후 명시 경로만 로컬 커밋하며 push 안 함.

## 증거
- .harness/local/telemetry-final-{cannon,waiting}-20261022.json, telemetry-code-v14.txt, telemetry-final-visible.png. 이전 증거는 History/005~008.
- Docs/Log-system/schema.md, detail-source-contracts.md, remaining-work.md.
