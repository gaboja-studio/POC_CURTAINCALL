# A 쇠창살 시작홀8분 타이머
- 담당 @RagSpirin / feat/exit-route/design-map / Task073 / 2026-11-01.
- 사용자 승인:8분,대기실 타이머 UI 재사용,쇠창살 구역 진입 시작,기존 탈출 성공 조건 유지,Play/MPPM 미실행.

## 구현
- EscapeRouteTimer를 EscapeCampaignFrame에 배치.480초는 씬 Inspector에서 조절한다. 별도 제한시간 UI 디자인이나 대기실 시간 판정 변경은 없다.
- FacilityContent/53_EscapeCountdownStart: 시설 로컬 중심(15,2.5,11.25),크기(30,5,22.5),Trigger. 첫 전망 홀 X0~30/Z0~22.5 바닥 전체를 포함한다. 실제 쇠창살 외곽은 X31/Z2~20.5다. 계단 끝(-62,2,19.5)은 바깥이다. 충돌용 바닥/벽은 추가하지 않는다.
- 로딩 커튼이 끝나고 transfer.Ready인 뒤 최초 정상 참가자 진입으로 시작한다. 부상은 허용,1~4인 누구나 시작 가능. 시작 후 구역 이탈/다른 참가자 진입은 시간을 연장하지 않는다.
- NetworkSessionManager.ServerTime의 시작 시각과480초를28바이트 상태 메시지(revision/token/generation/epoch/duration)로 공유한다. 호스트만 시작/리타이어를 결정한다. 클라이언트는 표시만 계산하고,주기 스냅샷/최초 요청으로 상태를 받는다.
- 기존 RouteResetBarrier/NetworkPlayer.Restarted 경계를 사용한다. R 재시작 시 이전 스냅샷을 거절하고,원격 소유자 위치가 스폰에 돌아온 뒤 새 진입을 받는다. 씬/세션 종료 시 구독·메시지 핸들러를 해제한다.
- UI는 Resources/Prefabs/UIs/RestTimerV28/W1_RestTimer_v2_8 정본의 A 인스턴스다. 기존 A HUD Canvas 안 좌상단(48,-64),192×220. 최초 진입 전 숨김,시작 후8:00/남은 비율·원본30초/10초 경고를 표시한다. 실제 일차를 전달하고 시간대 아이콘은 미지정한다.
- 타임아웃 때 Normal인 현재 참가자만 Retired. EscapeCampaignContent(-32000)가 먼저 도착을 확정하고 타이머(-50)가 미도착자를 처리하므로 같은 프레임 출구 도착자는 보존한다. 성공/실패는 기존 content.Update의 전원 집계가 처리한다. 도착자0이면 기존 전원 실패,1명 이상이면 기존 성공이다.
- 마지막 추격 화염의 트리거/20초 지연/5.2초 간격은 변경하지 않는다.

## 검사·제약
- 컴파일 성공. 새 EditMode12/12:480초 경계/음수 방지·미시작,중복/이전 스냅샷 거절,재시작 ACK,잘못된 시각/기간 거절,회전된 실제 크기 시작홀의 원격 부상자 진입/접근 통로 제외,기존 UI8:00/경고 단계.
- 편집 검사: 시작홀5표본 포함/계단 끝 제외,타이머/표시 각각1개,참조 누락0/씬 누락스크립트0,UI 원본 동일 GUID/무변경,A dirty0·Play stopped. 성공 집계 코드/대기실 씬/공용 타이머/외부 원본 변경0.
- 증거 .harness/local/escape-timer-20261101: backup-manifest.json,scene-before.json,installed.json,final-audit.json,timer-tests.json.
- 실제 네트워크 동시 만료/연속 완주·재시작 체감은 검증하지 않았다. 사용자 지시에 따라 Play/MPPM을 시작하지 않는다.
- 직전 check-work는 외부 Play 전환으로 중단됐으나,2026-11-02 중지 상태에서 dev586a3d19 반영 후 안전성/문서·규칙/컴파일 PASS로 마무리했다. 코드/관련 의존성 무변경의 EditMode12/12는 재사용,Play/MPPM 재실행 없음. 최신 편집 증거 .harness/local/escape-timer-final-20261102.
