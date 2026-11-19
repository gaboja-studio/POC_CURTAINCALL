# Log

- 2026-11-19: 사용자 요청으로 feat/escape-route-map-edit-20261119 브랜치 생성. 담당 @RagSpirin. Unity Start 씬이 저장된 상태이고 Play가 정지된 것을 확인한 뒤 정상 종료했다. origin/dev fetch 및 fast-forward 확인 결과 95070a3d에서 최신 상태였다.
- 기존 _Recovery 파일과 외부 에셋 로컬 변경을 보존했다. 이전 에셋 수정의 asset-helper 보관 기록도 유지한다. 이번 준비는 맵 구현·테스트를 포함하지 않는다.
- Unity 에디터에서 Assets/Scenes/Levels/escape_route.unity를 열었다. 맵 내용 수정 없이 사용자 편집 대기 상태로 준비했다.

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-19 편집 지원: 원래 재질 매핑 93개를 Assets/Scenes/Levels/EscapeFactoryArt/pursuer-edit-colors.json에 보관하고 전용 핑크 재질을 연결했다. 판정 후보 전체를 그리던 임시 오버레이는 사용자 요청으로 완전히 제거했다. 대상은 경로 보행 지지면·노출 추격 구역에서 실제 지지/몸통 통과 검사에 통과하는 바닥 및 명시된 파괴 패널이며 주변 차폐벽 전체를 포함하지 않는다.
- 색상 메뉴: Tools/CurtainCall/탈출로/추격자 발판·파괴벽 핑크색. 껐다 켜기 실검증에서 93개 모두 원래 재질 복구, 재활성화, Transform/활성 상태 불변을 확인했다. 삭제 대상은 건너뛰고 사용자 지정 재질과 슬롯 수 변경을 보존한다. 현재 ON. 컴파일 completed, failed=false, errors=[] 확인.
- 편집 카메라 기본 CameraSettings 복원, dynamicClip=false/nearClip=0.03/farClip=10000. 이전 실제 nearClip=7.209m가 근거리 물체를 잘랐다. 게임 카메라·GameSettings 변경 없음.
- 검은 배경: EscapeBackground/41_AReadableRoute/01_SealedFacilityAndReveal/빛이 닿지 않는 심부, 컨베이어는 43_AConveyorRecovery/컨베이어 심부 암부 차폐 아래 5개 메시. 검은 표현과 EscapeFallVoid는 별개이다.
- 사용자의 실시간 맵 편집이 진행 중이므로 편집을 끊는 추가 Editor 종료·동기화는 하지 않았다. 시작 시 에디터 정상 종료 후 origin/dev fetch/merge를 수행했고 변경 없음이었다. 사용자 맵 편집은 보존한다.
- check-work -SkipUnity -NoRecord: 안전성 PASS, 문서/규칙 FAIL(기존 Assets/_Recovery 경로 위반 3건); 컴파일은 선행 실패로 SKIPPED, 자동 테스트 미실행. 별도 Unity recompile_status completed/errors=[] 및 원래 재질 93개 복구·재활성화·형상 불변 실검증은 통과. SceneView 캡처로 주변 벽은 원래 재질이며 지정된 발판/파괴패널만 핑크색인 것을 확인했다. 기존 외부 에셋 변경 및 _Recovery는 보존했다.
- 2026-11-19 사용자 저장·핑크 원복 요청: dirty escape_route 씬을 먼저 저장한 뒤 SetEnabled(false)로 93개 재질을 복구하고 저장했다. 삭제 대상 0, 슬롯 변경 제외 0, 활성 렌더러의 핑크 참조 0을 확인했다. 사용자가 삭제·이동한 맵 형상은 유지했다.
- 벽 안쪽/뒤쪽에서 사라져 보이는 표면을 막기 위해 factory-concrete/factory-floor/factory-recess의 _Cull만 2(Back)에서 0(Off)으로 바꿨다. 원래 색·텍스처·알파(1)·셰이더·충돌 형상은 보존했다. 이전 값은 .harness/local/escape-wall-cull-before.json에도 보관했다.
- 최종 확인: scene dirty=false, pinkEnabled=false, 전체(비활성 포함) 렌더러 핑크 재질 참조 0, 세 콘크리트 재질 _Cull=0. check-work는 안전성 PASS/문서·규칙 FAIL(기존 _Recovery 3개 경로 위반), 컴파일 SKIPPED(선행 실패), 테스트 미선택. 씬 파일 저장은 완료했으나 검사 게이트 실패로 Git commit/push는 수행하지 않았다.
- 2026-11-19: 편집 중 변경을 저장하고 에디터를 종료한 뒤 origin/dev fetch 및 fast-forward 확인(95070a3d, 변경 없음). 삭제된 그네 모델의 남은 Inspection flight recovery swing 본체를 제거하고, 새 Inspection cannon midair transfer swing을 씬에 연결했다. 직접 삭제한 기존 발판·난간·장식은 복원하지 않았다.
- 대포의 중립 탄도를 발사 후 약 3.4초의 새 그네 잡기 위치에 맞췄다. 첫 비행 구간에는 다른 그네·트램펄린 우회를 제한하며, 지정 그네를 잡기 전에 지지면 또는 낙사 높이에 도달하면 호스트가 대포로 복귀시킨다. 정상 그네 잡기 이후에는 공용 운동·착지 규칙을 사용한다.
- Unity 컴파일 성공. InspectionRelayAimsThroughItsRequiredMidairSwing 및 RealSwingInputReachesTheWidenedTransferAndBothIntendedLandings 각 1건 통과. check-work는 기존 _Recovery 경로 위반으로 실패. 다인 플레이 및 실제 제동 뒤 대포 복귀는 미검증. 씬 저장 확인.
- 2026-11-19: 사용자 정정에 따라 59_FirstRopeRecovery를 삭제하지 않고 위로 29m 이동했다. 기존 움직임을 유지하고 InspectionRelay 무입력 탄도 최고점과 연출 최하단 사이에 6.88m 여유를 확보했다. 연출 대포 내부에 기존 불투명 강철 재질을 쓰는 무충돌 막을 붙였다. 씬 저장과 연출 그룹 충돌체 0개 확인.
- 2026-11-19: 이전 InspectionRelay 탄도는 방향 전환·입력 해제·제동을 포함한 EditMode 물리 시뮬레이션 672가지 중 25가지가 TimedWire 지지면에 착지했다. 전환 그네를 시설 로컬 (69, 1, 33)으로 옮기고 대포를 서쪽으로 재조준한 뒤 같은 입력에서 외줄 착지 0/672를 확인했다. 이전 호스트 착지 보호는 겹쳐서 남겨 두었다.
- 심부: GameSettings의 탈출로 루트 기준 음영 범위를 -19m에서 -48m로 조정하고, 씬 미리보기 콘크리트·바닥·어두운 콘크리트 3종에도 일치시켰다. 맵 전체의 해당 표면에서 높이 기준으로 적용되며 게임플레이 충돌 설정은 변경하지 않았다.
- 최종 확인: Unity 컴파일 성공. InspectionRelay 잡기·외줄 우회 2건, 그네 이후 양 좌석 정상 착지 포함 1건, 심부 설정 3건 등 EditMode 총 6건 통과. check-work는 기존 _Recovery 경로 위반으로 실패했다. 실제 다인 플레이와 장비 조합은 미검증. 탈출로 씬 저장 상태 확인.
- 추가 숏컷 수정: InspectionRelay 첫 비행과 그네를 놓은 뒤 비행에 사라지는 외줄 주변의 수평 이동 경계를 적용했다. 높이와 무관하게 몸 폭을 포함한 프레임 이동을 경계에서 제한한다. 지정 컨베이어 발판 이외의 지지면은 호스트가 통과로 처리하지 않고 대포 시작점으로 복귀시킨다.
- InspectionRelay EditMode 3건 통과. 공중 그네 도달, 정방향·역방향 조작의 외줄 착지 회귀 검사, 세 높이에서 외줄 경계 접근 차단 및 정상 목적지 방향 허용 확인. 다인 플레이 미실행.
- 제출 전 Unity 정상 종료 후 origin/dev 확인(95070a3d, 변경 없음). 기존 Unity 복구 파일과 메타는 .harness/local/unity-recovery-20261119-153128에 보관했다. 외부 에셋 로컬 수정은 커밋에서 제외한다.
