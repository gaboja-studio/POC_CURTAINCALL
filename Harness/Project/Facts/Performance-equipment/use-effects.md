---
summary: 공연 기물 4종의 월드 사용 VFX, 프리팹 교체와 실제 플레이 녹화 경로
status: active
updated: 2026-11-18
source: Task-20261117-155, 사용자 사용 연출·영상 요청
---

# 공연 기물 사용 연출

## 언제 읽나
기물 발동 이펙트, 지속 효과의 종료 또는 실제 공연 시연 영상을 수정할 때.

## 구현
- `EquipmentUseVisual`은 서버 승인 RPC에서 생성하는 순수 시각 효과. 판정·내구도·이동은 기존 NetworkPlayer에 남긴다.
- 신발: 발동 지점에 남는 금빛 확산 링 2겹과 스파크. 구조: 서버가 대상 NetworkObjectId와 확정 착지 위치를 전송하여 실제 이동하는 동료에 연결 줄·링을 붙인다. 소지자 전용 사전 표식은 [rescue-pull.md](rescue-pull.md).
- 끈끈이: Humanoid 양손 뼈에 민트 광채, 시간이 줄어드는 원호. 벌룬: 기존 바지 모델을 골반 높이에서 팽창하고 푸른 바람 궤적을 표시한다.
- 끈끈이/벌룬은 NetworkVariable 활성 상태를 따라 종료한다. 원격 변수 전달을 기다리는 0.12초 여유, 종료 후 0.28초 소멸. 사망·플레이어 제거는 즉시 정리한다.
- `GameSettings.PerformanceEquipment.useVisuals`: 신발/구조/끈끈이/벌룬 순서. `Assets/_ThirdParty/curtaincall/EquipmentUseFx`의 전용 프리팹 4종과 머티리얼 2종. Synty HorrorCarnival Sparks와 ItemArt 모델을 재사용한다.
- 생성 메뉴 `CurtainCall/Performance/Build Equipment Use Effects`는 파생 프리팹을 다시 만든다. 수작업으로 다듬은 프리팹을 보존하려면 다시 실행하지 않는다.

## 녹화
- `EquipmentUseRecording`은 Editor 전용 Unity Recorder 도구. 실제 GameView 1280×720 MP4, 가변 프레임률 기준 30fps, 무음. 게임 규칙을 바꾸지 않고 촬영 카메라와 설명 자막만 추가한다.
- `DevStartLauncherPlayTests.CaptureEquipment_*`는 실제 캠페인 호스트를 실행하며 실제 발동/소모/종료를 검사한다. 실행 전 `Temp/PerformanceEquipment/record-use-videos.flag` 파일을 만들고 완료 후 지운다. 플래그가 없으면 자동 녹화를 건너뛴다.
- 결과는 `Temp/PerformanceEquipment/UseVideos`에 쓴다. 보관본은 외부 리뷰 아티팩트 폴더로 복사한다. 구조 시연은 한 호스트의 두 캐릭터를 사용하며 별도 클라이언트 네트워크 검증을 대신하지 않는다.
