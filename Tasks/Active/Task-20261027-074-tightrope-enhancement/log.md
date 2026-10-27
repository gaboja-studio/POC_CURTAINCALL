# Log

- 2026-10-27 작업 세팅: 사용자 요청에 따라 `feat/tight-rope/enhancement`에 Task-074를 연결하고 담당자를 @lakuase2로 설정했다. 게임 코드·씬 수정은 하지 않았다. 다음 작업은 현황 조사와 고도화 범위·수용 기준 구체화다.
- 초기 doctor(-SkipEditor): FAIL — 현 브랜치에 대응하는 Active Task 없음. 이번 Task 생성으로 누락을 보완했다. unity-cli 버전 문서 차이와 gh 로그인 필요 경고가 있었다. 문서 세팅에는 영향 없으며 Unity CLI 사용·커밋 시 각각 확인한다. 동일 작업의 doctor는 반복하지 않았다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-27 추격 불 연출: 빨간 상자였던 추격 불을 파티클 불벽(`Tightrope/Prefabs/ChaseFire/ChaseFireWall.prefab`: 불꽃·밑불·불씨·주황 점광원, 생성 텍스처·URP 가산 머티리얼)으로 교체했다. `FireView.chaseEffect`에 연결하면 실행 중 추격 거리로 이동하고 X 배율로 코스 폭(19m)을 맞춘다. 비우면 기존 상자로 돌아간다. `Tightrope.unity`의 추격 시작 위치(z=-6)에 배치해 편집 화면에서 볼 수 있다. 플레이 중 이동과 화면 느낌은 사람이 확인해야 한다. 씬 저장 시 TMP 글자색 오버라이드와 vivox `Settings.json`이 에디터에 의해 함께 바뀌었다.
- 2026-10-27 수평톱 외형·이펙트: MoHoDoo의 예외 허용에 따라 공용 아트 키트 `HorizontalSaw.prefab`·`HorizontalSaw.mat`을 수정했다. 크기(외형 1.2m·판정 1.0m·두께 0.1m)는 유지했다. 날은 밝은 강철로 바꾸고 0.09m로 얇게 했으며, 빨강 허브·노랑 경고 링(발광)과 불꽃 스파크(`SawSpark.mat/png`)를 더했다. 스파크는 이동 거리 기준(9개/m)으로 나와 발판으로 멈춘 동안에는 나오지 않는다. `TightropePreparedAssetsTests` 11/11 통과, `PreparedArtFollowsSupportRemovalPoleMotionAndSawRebuild` 통과. `SavedTightropeArtAndFixedLevelOneDensityWorkWithoutRuntimeRebinding`은 활성 수평톱 수 10 기대·9 실제로 실패하며, 원본 프리팹으로 되돌려도 같아 기존 실패다(밀도·배치 설정 확인 필요). 저장 중 `Pretendard-Bold SDF.asset`이 에디터에 의해 함께 바뀌었다.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-27 수평톱 외형 재수정: 사용자 피드백(빨강 허브·노랑 링이 별로)에 따라 허브·링과 해당 머티리얼을 제거했다. 외부 모델 날 대신 생성 메시 `HorizontalSawBlade.asset`(갈고리형 톱니 18개, 날끝 지름 1.2m·두께 0.09m, 회전 방향으로 날끝)을 넣고 `HorizontalSawBody.mat` 어두운 몸체(0.62m)·강철 축으로 대비를 줬다. 스파크는 유지했다. `TightropePreparedAssetsTests` 11/11 통과.
- 2026-10-27 수평톱 중앙 금속화: 사용자 요청으로 어두운 몸체를 없애고 날판 전체를 동심원 연마 강철(`HorizontalSawBrushed.png`)로 바꿨다. 원형톱 표식으로 중앙 플랜지(`HorizontalSawFlange.mat`)·볼트·축 구멍(`HorizontalSawSlot.mat`)을 더했다. 팽창 슬롯 검은 선은 사용자 요청으로 뺐다. `HorizontalSawBody.mat`은 삭제했다. `TightropePreparedAssetsTests` 11/11, `PreparedArtFollowsSupportRemovalPoleMotionAndSawRebuild` 통과.
- 2026-10-27 수평톱 위협적 외형: 사용자 요청으로 날 메시를 앞으로 굽은 갈고리 톱니 14개(깊은 홈, 날끝 지름 1.2m 유지)로 바꾸고 귀 자르기 삼각분할로 생성했다. 톱니 영역은 그을린 어두운 쇠, 날끝은 주황 발광(`HorizontalSawHeat.png` 발광 맵)으로 달궈진 느낌을 줬다. 중앙은 검은 플랜지·회색 볼트·축 구멍. `TightropePreparedAssetsTests` 11/11 통과.
- 2026-10-27 수평톱 금속감: 주황 발광이 싸 보인다는 피드백으로 발광·그을린 톱니를 제거하고 `HorizontalSawHeat.png`를 삭제했다. 날 메시 테두리에 0.009m 모따기를 넣어 금속 하이라이트를 만들고, 금속·광택 맵(`HorizontalSawGloss.png`: 날판 반무광 0.6~0.78, 톱니 0.9)과 중립 강철색을 적용했다. 금속도 1은 기본 하늘 반사로 파랗게 떠 0.85로 낮췄다. 중앙은 어두운 금속 플랜지·연마 볼트·축 구멍. `TightropePreparedAssetsTests` 11/11 통과.
- 2026-10-27 수평톱 원거리 외곽선: 실행 중 확인 결과 `TightropeOutline` 3px(화면 고정)이 거의 옆에서 보이는 얇은 가로 톱을 멀리서 검은 막대로 덮고, 중앙 원판 메시들이 날 안쪽에 검은 고리를 만들었다. `TightropeSaws`에서 가로 톱만 카메라 거리 5m까지 3px, 14m에서 0이 되도록 테두리를 줄였다. 중앙 플랜지·볼트·축 구멍은 날 텍스처에 그리고 별도 메시와 `HorizontalSawFlange/Slot.mat`을 삭제했으며 날 두께를 0.1m로 맞췄다. Play 중 3/7/12/25m 촬영으로 확인. Tightrope 에디터 테스트 267개 중 266 통과, 실패 1개 `PiggybackHopUsesSameHeightAndGravityAsRopeJump`(목마 점프 높이 0.9 기대·1.35 실제)는 톱과 무관. `PreparedArtFollowsSupportRemovalPoleMotionAndSawRebuild`는 아래 검사 참고.

- 2026-10-27 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-27 톱날 경로 선 비활성화: 사용자 요청으로 `TightropeSaws.Paths.cs`에 `showSawPaths`(기본 꺼짐) 인스펙터 토글을 추가했다. 꺼지면 가로·정면·협동·해제 톱 경로 LineRenderer를 `forceRenderingOff`로 그리지 않고 경로 계산·활성 상태는 유지한다. Play 중 활성 9개·표시 0개 확인, `SawLaserPathsFollowActualRoutesAndResetWithoutAddingCollision` 통과. 아트 키트 readme의 "전기톱 경로 표시" 항목은 현재 기본값과 달라졌다.
