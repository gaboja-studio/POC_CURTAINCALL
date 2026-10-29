---
summary: 공연장 톱날·철골·쇼 조명·안개·현수물·먼지/불티의 동작과 전체 Scene 미리보기
status: active
updated: 2026-10-29
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

## 남은 확인

- 최종 미감·회전 범위·대상 PC 성능은 사용자 확인이 필요하다.
- 배경 씬과 파생 메시가 private 저장소에 있으므로 공개 코드와 비공개 에셋을 함께 보존해야 한다.
