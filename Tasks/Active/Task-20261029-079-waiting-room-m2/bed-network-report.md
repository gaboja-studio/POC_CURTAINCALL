# 침대 자세 동기화·실제 2인 검증 (2026-10-29)

## 변경
- NetworkPlayer의 소유자 쓰기 NetworkVariable<int> RestingBed로 선택 침대를 복제한다. 게임 판정이 아닌 외형 상태이며 기존 CampaignRun 준비/호스트 판정을 유지한다.
- NetworkBedPose가 각 화면에서 PlayerModelSlot을 해당 침대 매트리스 위치에 수평으로 놓는다. 모델 Animator는 잠시 멈추며 취소/기상/컴포넌트 종료 때 원래 로컬 위치·회전·Animator 상태를 복원한다. 충돌체·플레이어 이동 루트·부상 상태는 변경하지 않는다.
- 침대는 이름 정렬로 안정적인 인덱스를 만든다. NightBed 준비 진입 때 번호를 보내고 BedRestView 종료 때 -1로 해제한다. Night/Dawn 외 시간에는 남아 있는 표시값이 있어도 모델을 눕히지 않는다.
- 보류된 연습실·타이머·튜토리얼·공연 해금·탈출 진행·세대 교체·스케줄 편성은 수정하지 않았다.

## 실제 LAN 2인 검증

Unity Multiplayer Play Mode PlayerTwo를 활성화해 별도 에디터 프로세스로 연결했다. 실제 두 NetworkManager/전송을 사용했으며 오프라인 인원 수 모의가 아니다. 진행 중 방 입장은 거절돼 세션을 닫고 대기 상태에서 두 명을 먼저 연결한 뒤 DevStartLauncher로 밤에 진입했다.

- 양쪽 모두 Night, RequiredCount=2, 플레이어2명, 침대4개 확인.
- 참가자 BedIndex1 준비: 양쪽 ReadyCount1, Night 유지. 양쪽에서 참가자 RestingBed1·IsLying=true, 모델 up=(1,0,0), 위치(18.59,0.61,80.58)가 동일했다.
- 참가자 취소: 호스트에서 ReadyCount0, RestingBed=-1, IsLying=false 확인.
- 호스트 BedIndex0 준비: 참가자 화면에서 ReadyCount1·Night, 호스트 RestingBed0·IsLying=true 확인.
- 참가자도 다시 준비: 양쪽에서 2일차 RestTime으로 전환, localReady=false, InputGate.IsBlocked=false, NightPower.IsCut=false, 두 플레이어 모두 RestingBed=-1·IsLying=false 확인.
- 실제 접속검증은 2인이다. 3~4인·취소 키의 수동 조작·접속 종료·동일 침대 동시 선택/예약은 이번 검증 범위 밖이다. 침대 전용 전신 애니메이션 클립은 없으며 실제 모델을 침대 위치/자세로 배치하는 연출이다.
- 임시 Play Mode 종료 및 PlayerTwo 비활성화 완료. 에디터 씬 dirty0. 승인검토 사용량 제한으로 종료 요청 한 번 거부됐지만 사용자 계속 지시 후 정상 정리했다.

자동 회귀·최종 검사 결과는 log에 기록한다. 커밋/푸시하지 않는다.
