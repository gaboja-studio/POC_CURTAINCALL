# 토끼 교체 호환 기준

## 2026-10-29 실제 Editor 조사
- 프로젝트 D:/github/POC_CURTAINCALL, 편집 모드, Start 씬 clean 확인. 기존 자산 변경 없음.
- 재현: `Source/audit-existing-character.cs`를 연결 Editor eval_file로 실행. 상세 결과 `Source/existing-character-audit.json`.
- Player: `Assets/Resources/Prefabs/Characters/Players/Player.prefab`.
- 모델: `Assets/_ThirdParty/Task062/PlayerPresentation/Humanoid.prefab`, GUID `74a838ed07e947f408fe81514c332e88`.
- Avatar: `Assets/_ThirdParty/Task062/HumanoidValidation/Body Type 1.fbx`. isValid/isHuman=true, skin bones 49, Humanoid 매핑 41개.
- Controller: `Assets/_ThirdParty/Task062/PlayerPresentation/Player.controller`. applyRootMotion=false.
- Base: Idle/Walk/Run/Crouch/CrouchWalk/JumpStart/JumpLoop/JumpLand/Death/Crawl/Tightrope/Flying/Fall/Hanging. Carry는 index 1, Carry.mask, Carrying 상태. 총 15개 클립 모두 humanMotion=true.
- SMR 9개: Original 7768 vertices/1 submesh, body1~4는 24978/21726/16452/11178 vertices 및 2/3/4/5 submesh, limb1~4는 3834/3840/5838/5838 vertices 및 2/3/4/5 submesh. 모두 49 bones, rootBone=spine.
- slot: fitToHeight=true, squashWhenCrouched=false. CrawlHeightOffset=-.05m.
- 실제 GameSettings ModelHeight=1.73m, CapsuleHeight=1.5m, CapsuleRadius=.3m. 토끼 v03 귀 제외 정수리=1.73m, 귀 포함=2.1586m. 기존 fit을 그대로 쓰면 약 .8014배가 되어 몸통까지 축소된다. 시험 자산은 원래 미터 크기를 유지하고, 실제 교체 때 fit 비활성/기준 신장 정책을 검증한다.

## 코드에서 확인한 계약
- 절단 순서: 왼다리→오른다리→왼팔→오른팔. 정상+누적 body4+limb4, 동일 bones/bindposes. 원본 표면 N개 뒤에 단계별 cap을 추가하여 N+i submesh 유지. 마지막 cap이 분리 충격점 계산 기준이다.
- 기존 FixedLegMeshBuilder는 대퇴/상완 중간(f=.45/.47)을 자르며 단일 연결 성분/단일 폐곡선만 지원한다. 복수 옷 껍질/분리 구두를 합치기만 해서는 동작하지 않는다. 토끼용 부위 소유와 복수 경계 처리가 필요하다.
- 기존 cutter는 position/normal/UV0/4 weights만 보존한다. tangent/vertex color/UV2+/blendshape 의존 시 확장 필요. 새 생성기는 표면 속성을 잃지 않도록 별도 검증한다.
- 모든 skin bones 및 상완/전완/손·대퇴/하퇴/발은 rootBone 하위에 있어야 한다. 분리 사지는 복제 스켈레톤 사용, 원본 Transform 잔존 금지.
- 전신 관절 래그돌은 Hips/Chest 또는 Spine/Head 및 4사지 매핑 사용. 완전 몸 RB11/Joint10, 분리 사지 RB2/Joint1. 반경이 고정되어 새 체형에 대한 실제 검사 필요. 귀 물리는 자동 생성되지 않는다.
- SetColor는 원본 표면 전부의 _BaseColor/_Color를 덮는다. 원화 고유색과 플레이어 식별색 분리 후 교체한다.
- Carry는 index1, 상체 mask. Hanging 손바닥은 Hand→MiddleProximal 70% 위치, 중지 누락 시 손목 fallback. 기존 Avatar에는 중지 매핑이 없으나 토끼에는 중지 포함 전 손가락을 만든다.

## 시험 구조 결정
- v03 원본을 보존한 별도 v04 시험 씬/파일. 외형 부품을 복제해 명시적 휴지 T-pose, Humanoid와 손가락, 귀/옷자락 보조 본을 만든다.
- 다리 절단 대상은 바지와 하단 발목/구두/장식, 팔 절단 대상은 흰 팔과 손/커프스/장식. 골반/벨트/긴 옷자락은 몸통 소유. 각 부품의 소유 태그를 보존한다.
- 팔꿈치/무릎 주변 가중치는 인접 두 본으로 연속 보간. 봉제선은 가까운 표면의 웨이트를 따라가게 한다. 초기 수학적 검사와 최종 변형 품질 판정은 구분한다.
- v03의 분리 부품·열린 조끼는 그대로 최종 통과시키지 않는다. 먼저 시험 리깅으로 동작 결함을 노출하고 관절 루프/폐합/재질을 보완한다.

## 기능별 검증 목록
| 기능 | 유지 조건 | 현재 토끼 상태 |
|---|---|---|
| 기본 조작·14개 상태 | 기존 판정/Controller 유지, 발·관절·옷 관통 검사 | 미실시 |
| Carry | layer1, 하체 유지, 손가락/물체 접촉 | 미실시 |
| 순차 절단 | 5개 신체 상태, 닫힌 캡, 순간 위치 연속 | 미실시 |
| 분리/전신 래그돌 | 복제 본/관절/몸통 및 조각 충돌 | 미실시 |
| 매달림 | 양손/한손 접촉, 팔 도달/손가락 자세 | 미실시 |
| 복구·재시작·재바인딩 | PlayerCondition에 일치하는 상태 | 미실시 |
| 1인칭·가림 페이드 | 실제 슬롯/모든 SMR 연동 | 미실시 |
| 색상 | 고유색 보존+플레이어 식별, 캡 제외 | 미실시 |
| 원격 표시 | 온라인 2인 상태/동작/복구 일치 | 미실시 |

기존 HumanoidAssetsTests는 Task062 경로 고정, HumanoidPlayerTests는 실제 Player 고정이다. 그대로 실행하면 기존 더미만 검증하므로 독립 토끼 후보를 지정하는 검사가 필요하다. Crawl 기존 검사는 finite/기록 위주라 접지 품질 통과로 사용하지 않는다.
