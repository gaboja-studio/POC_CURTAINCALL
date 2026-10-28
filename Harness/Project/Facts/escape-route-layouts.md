---
summary: 최신 A 기반 B/C 배경 공간과 공통 탈출 복도·검토 도구·검사 범위
status: active
updated: 2026-10-28
source: Task-20261027-073 최신 사용자 방향, A 기반 B/C 생성 코드·정적 구조 검사
---

# 탈출로 A/B/C 검토 씬

## 언제 읽나

탈출로 배경을 둘러보거나 A/B/C 공간을 수정·재생성할 때. 상세 비교는 [Task의 variants.md](../../../Tasks/Active/Task-20261027-073-exit-route-space-design/variants.md)를 따른다.

## 현재 구성

- A: `Assets/Scenes/Levels/escape_route.unity`. 최신 수직 설비실·브루탈리즘 확장·사용감/밝기 보완·260m 마지막 탈출 복도가 기준이다.
- B: `Assets/Scenes/Levels/escape_route_b.unity`. 폴리싱된 최신 A에서 복제하고 03/05 내부에 높낮이 발판을 3개씩, 총 6개 배치했다. 점프·착지 중심의 공간안이다.
- C: `Assets/Scenes/Levels/escape_route_c.unity`. 같은 A 기반에서 03/05 내부를 좁은 정비 선반·회복대 4개·하강 계단 69개로 바꿨다. 외줄·균형 횡단을 위한 공간안이며 현재 선반과 계단은 일반 구조물이다.
- A 원본과 B/C 씬 GUID를 보존한다. 01/02/04/06, 마지막 260m 복도와 밝은 외부는 공통이며, 03/05도 A 외피·주 설비·입출구를 유지한다. 이전 B/C의 별도 평면·외부 오프셋·묘기 순서는 현행 기준이 아니다.
- **공간을 먼저 제작하고 구체적인 묘기 융합은 후속 개발한다.** 각 맵에 최종적으로 네 묘기를 포함할 방향이지만, 모든 구간에 네 묘기를 연속 배치하지 않는다. 구간별 대포+외줄·트램폴린+외줄 등의 조합, 외줄 단절 후 트램폴린 반동 같은 동작은 미구현이다. `Reserve_`는 공간 검토 지점이며 확정된 묘기 설치 앵커가 아니다.
- 묘기 사거리·속도·실제 통과, 문/협동 장치·추격 AI·탈출 판정·런타임 세트 선택·대기실 연결은 현재 배경 제작 범위 밖이다.

## 공통 A 기준과 편집 위치

- 03: `16_VerticalFacility`, 50×50m 외피(Y-90–44), 지름 12m·높이 120m 주 설비. Y0 입구와 Y-6 도착부, Y+2로 회복하는 기존 40단 계단을 공유한다. 제작/검사는 `EscapeVerticalCavern.cs` / `EscapeVerticalValidation.cs`다. 기존 03·14와 일부 보조 구조는 비활성 보존한다.
- 나머지 구조: `17_BrutalistRoute`, `EscapeBrutalistRoute.cs`·`EscapeBrutalistEnd.cs`. 04 깊은 수로, 05 하부 100m·도착 Y-8, 06 상행 40단. 실제 미터 UV 메시와 전용 무광 재질을 쓴다. [A 확장 문서](../../../Tasks/Active/Task-20261027-073-exit-route-space-design/a-brutalist-route.md)가 초기 A 치수보다 우선한다.
- 공통 07: `19_RunnerEscape`, X140–400 / Z78 중심 260m, 하부 48m·기본 주행폭 14m. 측면 배출 4곳·상부 낙하 3곳·22m 그네 단절과 설치 자리·밝은 외벽 출구를 담은 배경이다. 실제 기믹 동작은 없다. 17의 옛 07과 관련 18 소품은 비활성 보존하고 08 갈대밭을 새 출구로 이동했다. [마지막 질주 문서](../../../Tasks/Active/Task-20261027-073-exit-route-space-design/a-runner-route.md)를 따른다.
- 사용감·밝기: `18_WorkingFacilityFinish`, `EscapeIndustrialFinish.cs`·`EscapeIndustrialDetails.cs`. 배관·케이블·정비 적치·보수/누수 흔적, 주변광 RGB(.36,.37,.38), 넓은 작업등과 Main Camera의 Skybox 표시를 유지한다. `SurfaceWear.shader`는 기존 private 에셋의 마스크를 참조하며 공유 재질·텍스처 원본은 수정하지 않는다. PC/모니터 사이의 동일 밝기는 미검증이다.
- 공통 폴리싱은 최신 A의 `21_PlacementPolish`를 따른다. 새 B/C 공간은 `23_TraversalSpaces_B` / `23_TraversalSpaces_C`, 제작은 `EscapeVariantASpaces.cs`의 `BuildVariantFromCurrentA()`와 `EscapeVariantCSpaces.cs`다. C의 69개 계단은 03의 30개와 05의 20+19개이며, 마지막 0.2m 하강은 기존 Y-8 바닥으로 받는다.
- `20_SurfacePolish`와 씬별 `*-polished-surfaces.asset`는 중복/내부 표면을 제거한 파생 메시다. 배치를 바꿀 때는 잘린 면이 노출되지 않도록 해당 원본 메시를 먼저 복원한다. `EscapeSurfacePolish.cs`는 배치 확정 후 적용한다. 이전 검사와 복원 사례는 [polish-pass.md](../../../Tasks/Active/Task-20261027-073-exit-route-space-design/polish-pass.md)에 있다.
- 새 그룹은 실제 미터 단위를 사용하며 부모의 1.25 배율을 상쇄한다. 재생성은 해당 그룹의 수동 편집을 교체할 수 있으므로 저장 씬 검토에는 재생성 도구를 쓰지 않는다.

## 현재 검사와 검토 방법

- 저장 후 재개방한 B/C에서 `EscapeVariantSpaceValidation.cs`의 `ValidateASharedVariant()`로 B 바닥/계단 높이 표본 133/133, C 180/180, 공통 연결부 각 5/5, 공통 `ValidateRunnerEscape()` 각 394/394를 확인했다. A 원본 파일 해시는 변경 전과 같고, `SharedASpaceSignature()`의 공통 01/02/04/06·07·08 기록은 A/B/C가 일치한다.
- 구조면 검사는 축 정렬 삼각형 B 12,290개 / C 13,070개를 대상으로 동일 방향·동일 평면의 0.005㎡ 이상 중첩이 각각 0건이었다. 편집 화면도 각 8시점에서 확인했다. 이 범위를 벗어난 모든 형태의 겹침이나 화면 깜박임까지 검증한 것은 아니다.
- 서명은 선택한 공통 그룹의 계층·배치·메시·재질·조명을 비교하며 씬 전체 동일성이나 실제 이동·묘기 통과 검사가 아니다. 이번 묶음의 `check-work`는 안전성·문서/규칙·컴파일 PASS, 자동 테스트 SKIPPED이며 Task의 `log.md`에 기록했다.
- `ValidateCurrentVariant()`는 새23 그룹이 있으면 현행 검사로 연결한다. 이전 연결9·바닥11·단절3 검사는 구안용이다. Task `Layout/b-plan.{png,svg}`·`c-plan.{png,svg}`도 **구안 도면**으로 보존하며 현재 씬의 치수·동선 기준으로 쓰지 않는다.
- 씬 열기: `Tools/CurtainCall/탈출로/A·B·C 구조 씬 열기`.
- 각 Main Camera의 `EscapeRouteFlyCamera`: WASD/마우스, Space 상승·Ctrl 하강·Shift 가속·Esc 해제. 비행 중 기존 걷기 카메라와 씬 Player만 정지한다.
- `EscapeRouteReviewLabels`는 `Label_` 제작 라벨을 Play에서 숨긴다. `Wayfinding_` 현장 표식과 편집 화면의 제작 라벨은 보존한다.
- AI 이동 입력·자동 플레이는 하지 않는다. 공통 A 경험, B/C 공간 차이, 후속 묘기 융합과 최종 재미·밝기·성능은 사용자가 직접 확인한다.
