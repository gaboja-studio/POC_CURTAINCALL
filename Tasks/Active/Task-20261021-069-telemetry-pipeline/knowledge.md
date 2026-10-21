# 지식·검증 메모

- 기존 GameplayRecord는 개인 외줄 시도·512 event/3000 sample 한도. 신규 전체 게임 파이프라인과 계약이 다르다.
- 공유 런 ID 원천은 PerformanceRewards.State.SessionId(N UUID)+Generation. NGO 슬롯과 clientId는 별개.
- 기존 LostParts는 bitmask. 신규 lostCount는 0~4 개수로 변환한다.
- 프로젝트 Facts/색인은 Task-062의 광역 Docs/Harness 구역에 잡혀 이번에는 수정하지 않음. 소유 반환 후 PM이 정식 지식으로 이관.
- doctor submodule FAIL과 gh WARN은 샌드박스 조회 제한. 권한 확장으로 submodule HEAD·MoHoDu gh 계정 확인.
- 전체 PlayMode 테스트 후 runInBackground 0→1, 머티리얼 3개 _Color 소수점 변경 관측. 초기에는 clean이었다.
- 2026-10-21 PM이 자동 변경 4개 원복을 명시 승인. PlayerSettings는 Editor API, 머티리얼은 확인된 1줄 차이만 git restore. 이후 추적 파일 clean 확인.
- 서버 코드 업로드는 자동 승인 검토 1회 거절 후 사용자가 2026-10-21 명시 승인하여 재시도 성공.
- Google 원본·시트·설치 소스 폴더, Apps Script 코드 저장 확인. 권한 승인 후 ccScheduledRefresh_ 시간 기반 트리거 1개 설치·본인 전용 웹 앱 v1 배포 확인.
- 전체 PlayMode 검사는 900초 완료 시간 초과, 취소. 전체 EditMode 918/918, 로그 전용 11/11, Node 18/18 통과.
- 사용자 INGEST_TOKEN 직접 설정 완료. 수신 웹 앱 공개 범위는 별도 자동 거절 후 사용자 명시 승인받아 v2 배포. 잘못된 token의 실제 HTTP 요청은 UNAUTHORIZED.
- 본인 전용 v1은 빈 본문이었으나 같은 주소의 배포 버전을 검증된 v2로 갱신한 뒤 표시됨. 편집기 저장만으로 배포 코드가 바뀌지 않으므로 실제 배포 버전을 확인한다.
- Unity 자동 전송·ACK 후 pending 0 확인. Play 종료 이벤트는 durable outbox에서 다음 실행으로 재전송됨. 즉시 종료 업로드와 구분해 보고한다.
