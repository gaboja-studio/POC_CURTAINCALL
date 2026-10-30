---
summary: 대포 포신 회복은 호스트 발사 시각으로 구간을 샘플링하고 외부 안전 잠금과 분리한다
status: active
updated: 2026-10-30
source: Task-20261030-084
---

# 대포 포신 동작과 재탑승

## 언제 읽나
포신 회복 중 탑승되거나, 늦게 접속한 화면에서 예전 발사가 재생되거나, 협동 안전 잠금이 회복 종료와 함께 풀릴 때.

## 계약
- `InteractionAvailable`은 기존 협동 장치의 외부 잠금 값이다. 회복 타이머가 여기에 true를 덮어쓰면 안 된다.
- `ReadyToUse`는 외부 잠금과 포신 회복 완료를 함께 확인한다. 시각 에셋 유무와 관계없이 모든 Station에 적용한다.
- 공통 `HumanCannonSettings`의 쿨타임은 4~20초(기본 5초). 하강 1.2초·회복 2.8초는 고정하고 비활성 대기만 늘린다. 외부 잠금 ON은 남은 대기를 건너뛰고 회복하며, 다음 발사는 다시 전체 쿨타임을 쓴다.
- 호스트가 Launch를 확정할 때만 `FireBarrel`. State 메시지는 발사·외부 활성화 시각 double 2개를 포함한 81바이트. 수신과 송신을 함께 바꾼다.
- `NetworkManager.ServerTime.Time` 기준 절대 시각을 사용하며 Sync는 현재 구간을 seek한다. Reset/오프라인은 이전 발사 시각을 지운다.
- 원본 Blender의 4 take에서 BarrelPivot 곡선만 Unity 클립으로 복사한다. 게임 Seat/Muzzle/BoardingPoint 및 Collider는 움직이지 않는다.
- 포신은 +25도 활성에서 32도 내려간다. 회전된 Renderer AABB의 빈 모서리가 바닥을 침범해도 실제 메시 최저점은 약 .40m다. 형상 간격 검사는 변환된 정점을 쓴다.

## 확인
`CannonBarrelTests`, `BarrelMotion_` 실제 Play 테스트와 `CannonBarrelGallery`에서 검증한다.
프리팹 재생성은 `CannonBarrelArtBuilder.BuildPrefab`; 기존 소품 갱신 메뉴도 이 빌더로 연결된다.
