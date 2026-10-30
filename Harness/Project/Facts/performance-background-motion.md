---
summary: 공연장 톱날·철골·쇼 조명·안개·현수물·먼지/불티의 동작과 전체 Scene 미리보기
status: active
updated: 2026-10-30
source: Task-20261020-067; performance_background 씬 연결 및 메시 삼각형 보존 확인
---

# 공연장 동적 연출

## 언제 읽나

공연장 철골·조명 움직임, 속도·각도, 편집 중 미리보기를 조정할 때.

## 구성

- 씬: `Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity`.
- `PerformanceBackground/CircularVenue/28_DynamicShow`의 `PerformanceBackgroundMotion`이 크레인 3·쇼 스폿 7·빛기둥 12·톱날 15개를 제어한다.
- Atmosphere 50개는 흐르는 안개 14개와 깃발·천·케이블/현수 조명 36개다. 작업등 44개와 발광 부재 84개는 약한 밝기 맥동, Surface 98개 중 나머지 14개는 안개 시간 제어다.
- Particles 18개는 먼지/불티 공간 방출기 12개와 큰 톱날 주변 그라인딩 스파크 6개다. 광원·충돌체를 추가하지 않는다.
- Inspector의 Speed는 전체 속도, 각 track의 Rotation Degrees·Period Seconds·Phase는 각도·주기·시간차다. 원래 local 자세를 기준으로 왕복하며 누적 회전을 하지 않는다.
- Inspector의 Preview로 Scene 뷰에서 재생한다. Stop·다른 오브젝트 선택·저장·Play 전환·스크립트 재컴파일 전에 원래 자세로 복구한다.
- Play에서는 자동 재생한다. 물리·네트워크 기물 동작을 바꾸지 않는 로컬 배경 연출이다.
- `HeroSawGantry_*` 원본 메시를 유지하고 `Dynamic/`의 파생 메시로 고정 기초와 회전 상부를 분리했다. 5개 재질별 원본/분리 후 삼각형 수가 동일하다.
- `Machinery`의 Spin Degrees Per Second는 톱날의 연속 회전 속도다. 원형 본체·치형·표면 디테일을 함께 분리하고 127,800개 삼각형 총합 보존을 확인했다.
- 안개는 원본 ScenicHaze 재질을 보존한 새 FlowingVenueHaze 재질로 흐름·농도 변화·표면 난류를 표현한다. 고정 기둥/벽/통로는 유지한다.
- 고정 가로보·베어링을 추가했고, 실시간 광원 수는 늘리지 않았다. 실제 Spot Light와 장식 DustBeam의 움직임 track은 독립적이다.
- 코드: `Assets/Scripts/Systems/PerformanceBackground/PerformanceBackgroundMotion.cs`, 편집 도구는 `Editor/`.
- Setup.Build 및 AmbientSetup.Build는 이미 연결된 씬에서 재실행을 거부한다. 재생성 대신 기존 track을 조정한다.

## 접합·철골 재배치

- `30_ConnectedStructure`와 `Connections/`는 2026-10-30 접합 보강의 파생 기하다. 원본 메시 에셋은 유지한다. 천장 하단과 외벽 상단은 y=245에서 접한다.
- 플랫폼별 장기둥은 공용 벽 거더와 캔틸레버로 대체했다. 최초의 밀도를 되살리기 위해 원래 외곽 샤프트 4개 메시를 복원하고, `SharedIndustrialFrames`에 공용 타워 12개·깊은 벨트 트러스 8층·발판 전달보 368개를 두 재질 배치로 추가했다. 실시간 광원·충돌체는 추가하지 않았다.
- 통로/난간 끝, 하부 좌석, 크레인 고정부, 톱 축받이, 천장 조명·장식 빛기둥과 깃발 고정점을 실제 부재에 연결했다. 렌즈 18개의 SurfaceMotion이 추가되어 총 116개다.
- 깃발·천 22개는 위쪽 고정 모서리에 `hingeAxis`를 맞춰 움직인다. 일반 Euler 흔들림과 위치 이동은 이들 track에서 제거했다. 두 고정점의 0~90초 샘플 오차는 0이었다.
- `PerformanceBackgroundConnectionRefit.Build()`는 최초 접합용이며 이미 설치한 씬에 재실행하지 않는다. `RestoreIndustrialDensity()`는 그 뒤의 밀도 복원 단계로 중복 실행을 거부한다. 기존 씬에서는 재설치 대신 개별 부재를 조정한다.
- 미감 기준: [공연장 철골 밀도](../Decisions/performance-background-density.md).
- `31_ShowExpansion`/`ShowExpansion/`: 공용 타워에 붙인 독립 회전·상하 프레임 6개를 추가해 Crane track 총9개다. 끝단 상향/하향 스폿·가시 빛기둥은 각각12개 추가(ShowLights 총31), 그림자는 끈다. 기존 희박한 GrindingSparks 6개를 비활성으로 보존하고 톱15개에 고정 접촉 슈·World 공간 스파크를 연결했다. Particles 총27개(먼지/불티12+톱15). 생성 도구 `PerformanceBackgroundShowExpansion.Build()`는 중복 실행을 거부한다.

## 남은 확인

- 최종 미감·회전 범위·대상 PC 성능은 사용자 확인이 필요하다.
- 배경 씬과 파생 메시가 private 저장소에 있으므로 공개 코드와 비공개 에셋을 함께 보존해야 한다.
