---
summary: 보유 외부 에셋과 Unity 패키지 목록 및 탈출로 공장 설비 후보
status: active
updated: 2026-11-18
source: 프로젝트 dev와 외부 에셋 서브모듈 Git 트리 및 Packages 잠금 파일
---

# 보유 에셋 패키지 조사

- 조사 기준은 프로젝트 `56c05863b54a37692c16bf4874ce99b61f0559d3`, 외부 에셋 `701a1bbee3809a13ecfc4c3e94f6275c7c9b4565`이다.
- 외부 에셋 최상위 폴더 59개, 추적 파일 18649개(.meta·숨김 항목 제외, 문서 포함), Unity Package Manager 잠금 항목 79개(직접 의존성 56개)를 확인했다.
- 폴더는 공급사, 버전별 UI 키트, 프로젝트 산출물, 공용 리소스를 포함한다. 모델은 원본 파일 확장자 기준이고, Unity에서 생성한 메시 .asset은 전체 파일 수에만 포함된다.
- 범위는 프로젝트에 설치된 패키지와 로컬 Asset Store 기본 저장소다. 이 계정의 Asset Store My Assets에만 있는 미다운로드 패키지는 조사 범위 밖이다.

## 탈출로 교체 후보

| 대상 | 우선 탐색 경로 | 배치 전 확인 |
|---|---|---|
| 컨베이어·설비 | `Conveyor Works/Prefabs/Conveyor/{Static,Mixed,Rotor}`, `Conveyor Works/Prefabs/Machine` | 고정/회전 변형, 롤러 방향, 시각 프리팹의 Collider 및 Animator |
| 콘크리트 바닥·천장·기계 소품 | `Dzendor/StylizedFactory/Prefabs/{FloorsAndCeilings,Machines,DoorsAndWindows}` | 4m 모듈 접합, 피벗, URP 머티리얼 |
| 산업용 배관·팬·제어반·강판 | `LeartesStudios/AbandonedFactory`, `LeartesStudios/HDRP`, `Task073/SelectedEquipment` | HDRP 원본의 셰이더 호환성, 기존 프로젝트용 재질/메시 재사용 |
| 기존 탈출로 시설·표면 | `Task073`, `Task154`, `Assets/Scenes/Levels/EscapeFactoryArt` | 최적화 메시와 개별 원본 간 대응, 현재 씬의 연결과 충돌면 |
| 연기·증기·불티 | `Solodream/VFX_SmokeSteamPack/Prefabs`, `Synty/PolygonApocalypse/Prefabs/FX` | 카메라 가림과 파티클 수, URP 렌더 |
| 엔딩·추격·표지 | `EscapeResultsKit`, `curtaincall/Pursuit`, `Task100`, `curtaincall_worldlabels_v2` | 애니메이션 리그, 기존 UI 연결, 사용 중인 버전 |

실제 플레이용 씬은 `Assets/Scenes/Levels/escape_route.unity`와 B/C 변형이다. 개별 교체 시 씬 참조, 충돌·착지면, 기믹과 애니메이션 연결을 확인한다.

## 전체 목록

- [외부 에셋 폴더 59개](third-party-asset-folders.csv) — 파일·프리팹·모델·재질 수와 주요 하위 폴더.
- [Unity Package Manager 79개](unity-package-lock-inventory.csv) — 잠금 버전, 직접·간접 의존성, 공급 방식.

## Conveyor Works 재질 호환성

탈출로 작업 브랜치에서 확인한 비공개 패키지 119cb7d2는 컨베이어의 atlas, base color, rotor, glass, laser 그래프에 HDRP 대상만 활성화돼 있었다. 프로젝트는 URP를 사용한다. 각 그래프의 기존 Lit 블록과 Universal Lit 서브 타깃에 URP 대상을 연결하면 원래 재질과 벨트 움직임을 유지할 수 있다. 패키지의 원본 재질 6종과 탈출로 수조 이송대에서 확인했다.
