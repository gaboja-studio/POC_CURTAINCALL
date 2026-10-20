# Plan

## Context

2026-10-20 PM이 Body Type 1 왼다리 절단·충격을 직접 확인하고 오른다리→왼팔→오른팔 순차 절단과 한발 이동·앉은 한발 이동·기기·팔 점프 표현을 요청했다. 승인된 확장 계획에 따라 기존 단일 검증을 보존하고 독립 순차 씬을 만든다. Task 전체의 4인 배정·온라인·실게임 연결은 이번 범위 밖이다.

## Approach

1. 현재 worktree·전용 Unity(7801)만 사용. 원본 FBX·기존 검증 씬/프리팹·실제 Player.prefab·PlayerMover·BodyDamageEffects·공용 밸런스·다른 에디터 보존.
2. Body Type 1의 정상+누적 본체 4개, 분리 사지 4개를 private SequentialValidation에 생성한다. mask 0/4/12/13/15만 지원하며 임의 16조합은 만들지 않는다.
3. 이전 body를 다음 cutter 입력으로 사용, rest pose/bindposes/웨이트/본 순서/기존 캡 재질 보존. 단일 폐곡선·정점 근접·폐쇄/퇴화 guard 실패 시 중단한다. 자동 허용 오차 완화 없음.
4. 대칭 캡과 다음 절단면 정점 일치가 발생해 Body Type 1 고정 프로파일은 왼다리 .45/오른다리 .47/왼팔 .45/오른팔 .47로 명시한다. 자동 위치 탐색이 아니며 최종 아트 승인값도 아니다.
5. PlayerCondition의 공통 순서를 재사용한다. 새 손실만 전환 전 포즈에서 모두 BakeMesh한 후 본체를 전환한다. 기존 조각 유지·역순 복구·전체 복구·비활성 정리, 초기 손실/재활성은 과거 조각을 만들지 않는다.
6. 각 조각은 독립 Mesh+Collider+Rigidbody, 실제 신규 캡 면적 가중 중심에 Impulse/중력/연속 충돌. 본체에는 힘을 주지 않는다. 마지막 오른팔 생성 후 사망 단계로 이동·점프를 막는다.
7. 별도 표현 프로파일 슬롯(단계별 idle/move/low move/jump)과 검증 버튼 제공. 미확보 부상 클립은 일반 동작으로 대체하지 않고 MISSING과 포즈 정지를 표시한다. applyRootMotion=false, 공통 이동/기기 점프 규칙은 바꾸지 않는다.
8. 공식 무료 클립을 우선 확보·라이선스·실제 포함 목록 확인. Quaternius Standard CC0 취득/유효 Humanoid 임포트 완료지만 전용 hop/crawl/arm-jump는 없음. Mixamo 카탈로그 접근 불가로 실제 취득 못 함. 유료 구매·모델 업로드·제3자 재배포 파일 취득 없음.
9. 미확보 동작은 로컬 클립 수정/단일 모델 절차적 포즈 시제품 대안을 보고하고 확인 후 별도 구현한다. 일반 injured walk·swim·무릎 crawl을 적합품으로 표시하지 않는다.

## 사용자 제공 동작 추가 적용 (2026-10-20 승인)

- army_crawl/carrying/crawling/fall/flying/pickup/reach hand/tightrope_walk 8개를 private Task062/Mixamo로 GUID 유지 이동하고 Humanoid/Create From This Model로 변환한다. 자동 매핑의 기술 실패뿐 아니라 의미상 오류도 검사해 수동 보완한다. 실제로 목/머리·눈·엄지를 수정했고, 동일 49본 구조/길이/scale 확인 후 원본 name/parentName을 유지한 T-pose TRS 보정으로 자체 Avatar를 생성했다.
- 별도 AnimationClipPreview/AnimationPreviewBuilder·MixamoAnimationPreview.unity에서 Body Type 1에 적용한다. 파일명 선택·재생·graph 정지/재개·재시작·끝 정지·정리 및 정상/부상 메시 비교를 검증한다.
- 원본 길이·60fps·loop 설정·FBX 바이너리·클립 local fileID 보존. 사용자 제공 파일의 개별 출처/라이선스 미확인도 private sources.md에 기록한다.
- 실제 Play 시간 진행·본 자세 변화·유한한 스킨 좌표·루트 유지·시작/중간/끝 화면·두 crawl의 양다리/오른팔만 상태를 비교한다. 일반 리타게팅 성공을 접지/부상 전용 완성으로 보고하지 않는다.
- 기존8개 회귀+신규 검증, 저장 씬 재열기, 구역→문서→컴파일→테스트. MISSING 정책·공통 Player·상하체 레이어·새 동작 제작은 변경하지 않는다.

## 실제 Player 연결 (2026-10-20 추가 승인)

1. 기존 검증을 보존하고 HumanoidPresentation 게임용 참조/상태 연결/동작 driver·private 외형을 생성한다. PlayerModelSlot의 모델 교체 알림을 재사용해 최초/재생성에 연결하고 root/capsule/카메라/입력은 유지한다.
2. PlayerCondition 차분에서 전환 전 모든 신규 사지를 bake → 누적 본체 전환 → 단면 impulse. legacy LimbDismemberment와 상호 배제. 초기손실/late join은 조각 미생성, 복구/종료는 소유 리소스 정리.
3. 정상 이동/앉기/점프 및 공개 공연 상태에 클립을 연결한다. 한발은 정상 클립, 양다리/한팔은 army_crawl 임시 표현; 전용 hop/한팔/팔점프 완성으로 보고하지 않는다. root motion=false, 부상 판정·행동은 유지한다.
4. BaseGameSettings Character에 1~4색, 기존 NetworkPlayer.Slot/빈번호 재사용 유지. surface MPB/캡 보존/조각 당시 색상, 원격 이동·달리기 표현만 최소 동기화한다. 게임 내 색상 UI는 추가하지 않는다.
5. 구역→문서→compile→기존11 EditMode+신규 EditMode/PlayMode→실제 입력·공연 회귀. 다중 클라이언트/입력/접지/출처 미검증은 그대로 보고한다. 사람 확인 전 done 처리하지 않는다.

## Files

- 코드: Assets/Scripts/Systems/Player/HumanoidValidation/ (Editor·Tests 포함).
- 기존 private 결과: Assets/_ThirdParty/Task062/HumanoidValidation/ (보존).
- 새 결과: Assets/_ThirdParty/Task062/SequentialValidation/; 공식 팩/출처: QuaterniusStandard/.
- 씬: Assets/Scenes/Tests/Systems/HumanoidCharacters/SequentialHumanoidCharacters.unity.

## Auto Verification

- 구역→문서→전용 컴파일→기존 3개 회귀+순차 검증 테스트. EditMode 테스트에서 EnterPlayMode하는 합성/물리 검증이며 실제 사람 입력으로 보고하지 않는다.
- 누적 메시 캡/재질·bindpose·가중치, 4부위 신규/기존 조각·발사/낙하/착지·본체 유지·역순 복구 확인.
- 초기손실·재활성·복수 동시손실·중복·미지원 mask·오류복구·미확보 표현·사망 차단 검사.
- 실제 Play 시간 경과로 순차 화면 촬영, 저장 씬 재열기/참조 유지 확인. 실패·미실행·환경 제약 기록.
- 미확보 부상 클립의 접지·루프·이동/점프/착지 전환은 미완성으로 남긴다.

## 공동 테스트 초안 (기능 완성 후 한 번에)

1. 네 부위 순차 절단 시 각각 독립 비산·낙하하고 기존 조각과 캡이 유지되는지 확인한다.
2. 역순/전체 복구가 표시·조각을 일치시키고 사망 단계에서 이동·점프가 막히는지 확인한다.
3. 부상 전용 클립 확보 후 한발/낮은 한발/기기/팔 점프 접지·골반·전환 품질을 사람이 승인한다. 현재 미확보로 실행 불가.

## 보존

- 기존 TightropeCourse.prefab·LeartesStudios dirty·다른 worktree/dev 보존. 커밋/push/PR·제출·원복·구매는 별도 요청 전 하지 않는다.
