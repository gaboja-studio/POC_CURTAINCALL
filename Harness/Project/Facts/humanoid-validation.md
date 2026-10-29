---
summary: Body Type 1 고정 절단·8개 Humanoid·실제 Player 표현과 미완성 동작 제약
status: active
updated: 2026-10-30
source: Task-20261019-062, Task-20261029-078
---

# 휴머노이드 절단·외형 연결

다른 모델에 고정 절단 메시를 준비하거나 Player 외형/부상을 연결할 때 읽는다.

## 메시와 검증

- Body Type 1: 유효 Humanoid, 단일 스킨 7768정점/표면 슬롯1. 원본 readable=false, private 복사본만 가공한다.
- 고정 순서 왼다리/오른다리/왼팔/오른팔, 비율 .45/.47/.45/.47, 지원 mask0/4/12/13/15. 좌우 같은 .45는 기존 캡 근접 guard에 걸려 프로파일을 달리했다. 임의 절단 시스템 아님.
- private Task062/SequentialValidation에 본체4/사지4. 직전 body를 다음 입력으로 삼아 기존 캡·재질·본 순서/bindposes/보간 가중치 유지. submesh2/3/4/5, 초기 왼다리 양쪽 캡 각46삼각형.
- 모든 신규 사지를 전환 전 포즈에서 BakeMesh → 본체 전환 → 단면 삼각형 면적 가중 중심 Impulse. 조각은 독립 Mesh/Rigidbody, 부모·본·Joint 없음. 본체에 힘을 주지 않는다. 중력/연속 충돌/복구 정리를 사용한다.
- 가공기는 rest pose 단순 단일 경계용. tangent·추가UV·blendshape는 복사하지 않는다. 의상/다른 체형/복수 경계는 재가공·검증 필요.
- HumanoidValidation 코드는 독립 검증용. 기존 single/Sequential/MixamoAnimationPreview 씬은 HumanoidCharacters 테스트 폴더에 보존한다. 순차 검증 MISSING 정지 정책과 게임용 표현은 다르다.
- AssetPathToGUID는 세션에서 삭제한 GUID도 반환할 수 있어 OnlyExistingAssets와 파일/meta 검사를 함께 쓴다. 이미 active인 새 additive 씬의 SetActiveScene 실패는 생성 실패와 다르다.

## 애니메이션

- 공식 Quaternius Standard(non-RM) CC0는 실제43 Human 클립이며 hop/crawl/arm-jump 없음. private QuaterniusStandard/sources.md에 출처/목록 기록.
- 사용자8개 FBX는 private Task062/Mixamo로 GUID 이동, 자체 Humanoid Avatar/목·머리·엄지·눈 매핑 보정. 동명49본 구조/scale 확인 후 기준 T-pose TRS만 적용하며 SkeletonBone name/parentName은 보존한다. struct 전체 복사는 부모 불일치 임포트 실패를 유발했다.
- 8개 GUID/localID/길이/60fps/바이너리 유지. army_crawl은 낮은 양팔 포복, crawling은 높이 변화가 크다. 둘 다 한팔 전용 아님. 이전 미리보기 표본 army 약 -0.087m/crawling 약 -0.052m 관통; 접지 완료 아님. 개별 출처·배포 권리는 미확인.

## 실제 Player

- 2026-10-30: 토끼 게임용 리소스는 `Assets/_ThirdParty/CharacterArt/Rabbit/`(19개 GUID보존), 기능wrapper Rabbit.prefab은 Resources 유지. private PR10 사용자병합. NetworkManager의 PlayerPrefab·빌드씬11개 연결 조사 완료. HumanoidPlayerPresentation은0부상Clean/1~2Light/3이상또는Fallen·Retired사망Heavy를 적용하며 모델재생성/복구에도 재계산한다. 별도 표면동기화변수 없이 기존 상태를 사용한다.

- 2026-10-29 Task078: 실제 Player 슬롯은 `Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab`으로 교체. 기존 private Humanoid와 Controller/클립은 보존하며 아래 부상·카메라·동기화 계약을 재사용한다. v14 토끼는 정상9922tri/55skin bones/52명시적Humanoid,정상+누적Body4+Limb4,표면2+단계별cap. `fitToHeight=false`, `squashWhenCrouched=false`, 트림슬롯1만 플레이어색. Clean/Light/Heavy 3표면(RabbitSurfaceStates), Editor2/PlayMode16검사 통과(v15,2026-10-30), 발목560조건·300포즈/16단면gap0(v14), 5신체상태 숨김/복원은 v12 검증, 온라인2인·최종 미술 검토는 별도.

- HumanoidPresentation 코드 + private Task062/PlayerPresentation 외형/Controller/클립. 실제 PlayerModelSlot에 연결, root motion=false/squash=false. PlayerCondition이 부상 원천이며 HumanoidModel에는 legacy LimbDismemberment가 반응하지 않는다.
- 기본 이동/점프와 외줄/대포/소품·트램폴린 운반 상태를 읽는다. 한발은 정상 모션, 양다리/한팔은 army_crawl 임시 재사용. CrawlHeightOffset=-.05의 평지121표본 최저점은 root 기준 .0013~.0522m. 전용 hop/한팔 끌기/팔점프·공연 접점·경사 접지 품질 완성 아님.
- 초기 손실/모델 교체/재활성은 현재 메시만 복원. IsApplyingInitialCondition 구간만 비산 생략. 재시작/복구/종료는 소유 조각 정리. detach는 Animator 정지·현재 스킨 bounds의 root BoxCollider로 기존 단일 몸체 래그돌을 사용하고 월드 위치를 보존한다.
- BaseGameSettings Character 슬롯0~3 색상(빨강/파랑/노랑/초록)은 표면 MPB, cap/shared material은 보존. 살아 있는 외형은 live 변경, 조각은 절단 당시 색상 스냅샷. 기존 서버 슬롯/빈 슬롯 재사용 정책 유지.
- 원격 이동/달리기는 owner-write 표시 전용 bool. 기존 crouch/jump/부상 재사용, 같은 빌드/설정 전제. 게임 내 색상 편집UI/설정 실시간 네트워크 전송은 없다.
- CameraOcclusionFade는 MPB 표면 alpha도 조정/복원하고 숨긴 renderer는 건너뛴다. 1인칭은 기존 CameraModelVisibility가 현재 모델의 렌더 동안만 숨기며 부상 renderer.enabled는 보존한다.

## 검증 경계

- 기존 HumanoidValidation EditMode 11개, HumanoidPresentation의 자산 계약 EditMode/실제 프리팹 PlayMode 검사. 최신 결과/미실시는 Task062 handoff를 따른다. 입력 상태 주입과 Keyboard 이벤트 경로·사람 플레이를 구분한다.
- 순차 씬 Run 50 automated cycles는 5상태×10회 물리/복구용이며 사람/온라인 검증 대체가 아니다. 새 모델은 Avatar·웨이트·양쪽 캡·스킨·클립을 다시 검증한다.
