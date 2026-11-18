# Task-20261117-155

- **Title:** 탈출로 에셋 전면 교체
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/escape-route-asset-replacement
- **Base:** dev

## Context
운영 탈출로 `Assets/Scenes/Levels/escape_route.unity`의 시각·연출 에셋을 전면 교체하기 위한 준비 작업이다. 기준은 DEV `19b09e68`, private 에셋 `25f9cdee`다. DEV에는 기존 폴리싱 PR #416과 새 에셋 입고 PR #418이 포함되어 있다.

## 계획
현재 에셋을 씬 계층/컴포넌트와 GUID 단위로 조사하고, 프리팹·재질·텍스처·VFX·사운드·엔딩의 연결을 추적한다. 새 팩의 후보를 소스 목록과 대조한다. 교체 구현은 독립 장식/재질, 이동 공간 건축, 기물 외형, 추격자·엔딩 순서로 검토한다. 기존 바닥·벽·낭떠러지 실루엣과 앞뒤 차폐, 대포·봉·외줄 앵커와 충돌, 와이어/톱날, 네트워크 ID·엔딩 연결을 교체 전후 비교한다.

## 주의
공용 프리팹 원본과 다른 씬까지 참조하는 재질은 영향 범위를 확인한다. 외부 원본은 `_ThirdParty`에 두고 필요한 씬 인스턴스에서 연결한다. 실플레이는 사용자가 맡는다.

## 현재 에셋과 교체 경계

운영 씬을 EditMode로 열어 프리팹 내부까지 펼친 기준으로 오브젝트 9,847개, 렌더러가 붙은 오브젝트 6,973개, Unity 에셋 의존 경로 1,224개가 있다. 의존 경로에는 씬과 엔딩 리소스 2개를 포함한다. 주 에셋 타입은 메시 383, 머티리얼 216, 텍스처 189, GameObject 169, 애니메이션 54, 오디오 11이다. 152개 프로젝트 에셋은 다른 프로젝트 씬 파일에도 직접 연결된다.

| 구간·역할 | 현재 소스와 연결 | 교체 시 확인 |
|---|---|---|
| 입구·관측·미궁 벽과 바닥 | EscapeArrivalStairs, EscapeBackground/70_LabyrinthRemake, EscapeFactoryArt/optimized 및 Task154 변형 | 바닥·층고·낭떠러지·출입구, 벽 양면과 차폐, 장식 유격 |
| 대포·봉·외줄·발판 | HumanCannonStation 10, HumanCannonPole 36, HumanCannonLandingZone 89, TightropeCourse/공용 외줄 프리팹 | 발사·좌석·그랩·착지 앵커, Collider, 기물 ID와 시야 |
| 컨베이어·공장 설비 | EscapeConveyorProcession 3, EscapeIndustrialVignette 2, Leartes/Task073 철골·배관·작업등 | 운반 경로/애니메이션, 하차 바닥, 래치와 플레이어 여유 공간 |
| 협동 구간·화염 복도 | 32_A_CooperativeRopes, 38_ACooperativeWorks, 39_AFinaleInferno | 잠금/접이교에 물린 Transform, 분사 방향·피해 판정, 발판 연결 |
| 추격자·벽 돌파 | EscapePursuer_A의 Concealed pursuit structure, Physical pursuit platforms, Concrete wall routes, 캐릭터 리그와 NavMeshData | 경로/점프와 포획, 벽 파괴, 등장 시야, 전용로 비노출 |
| 마지막 배선·엔딩 | EscapeExitSaw 4, EscapeCuttableCable 24, EscapeEndingCinematic + Resources의 SuccessEndingArt/FailureEndingA | 발사·절단·문·엔딩 참조, 외부 차폐 |
| 조명·효과·소리·UI | Light 290, ParticleSystem 313, 씬 AudioSource/공용 알림·공연 HUD | 경고 및 경로 식별, 가독성, 공용 에셋의 타 씬 영향 |

## 입고된 교체 후보

| 에셋 팩 | 후보 파일 | 검토할 쓰임새 |
|---|---:|---|
| Dzendor Stylized Factory | 283 | 공장 벽/기둥, 철제 계단, 배관·작업 설비. 콘크리트 분위기에 맞는 소재부터 선정 |
| Nebula Forms Conveyor Works | 473 | 컨베이어 직선/회전/경사 모듈, 하역대, 제어반, 산업 조명 |
| Solodream Smoke/Steam | 23 | 증기 분사, 연기, 스파크 동반 연기 |
| Deadleaf Dissolve & Burn | 72 | 번짐·그을림·소멸 표현 |
| Synty Polygon Horror Carnival | 1,149 | 입구/엔딩 및 공장 연출용 카니발 소품. 현재 묘기 식별과 분위기에 맞춰 선별 |

후보 수는 모델·프리팹·재질·VFX·애니메이션 등의 파일 합계다. 패키지 데모와 변형도 포함하므로 고유 모델 개수로 해석하지 않는다.

## 첫 환경 교체 시안 범위

탈출로 전용 공장 환경 외형을 교체한다. 바닥·천장·벽과 외부 차폐, 기물·기물 외형·공용 게임 규칙은 기존 버전을 사용한다. 배관·철골은 형상을 유지하고 새 재질을 적용한다. 기존 외형 범위에 맞는 난간·작업등 하우징·카트·팔레트는 렌더링 메시를 교체한다. 배경 증기는 입고된 플립북을 사용한다.

## 전면 교체 최종 범위

사용자 최신 지시에 따라 바닥·천장·벽도 교체한다. 프로젝트의 기존 콘크리트·아스팔트·플라스터와 철제 텍스처를 용도별로 조합한다. 표면의 길이는 월드 좌표에서 계산하고, 엇갈린 축척과 불규칙한 얼룩으로 반복을 줄인다. 기존 형태와 연결을 유지할 수 있는 건축 메시, 배관 소매, 대형 출구문의 철제 패널, 분배함 외형, 절단날과 화염을 탈출로 씬에 적용한다. 발사기 몸체, 대포, 봉, 그네·외줄·트램폴린 등 공용 기물은 그대로 둔다.

## 입고분 전체 조사와 배치

위 후보 표는 1차 선별 목록이다. private 에셋 입고분을 파일 단위로 대조한 최종 목록은 메타 파일을 제외하고 2,738개다. Dzendor 562개, Conveyor Works 531개, Synty 1,509개, Solodream 34개, Deadleaf 102개다. 이 중 프리팹 823개와 FBX 934개를 Unity에서 읽어 계층, 메시, 재질, 충돌체, 동작 컴포넌트와 크기를 조사했다. 운반·검사·가공·생산·반출 설비는 기존 밀폐 작업대에 배치하고, 증기와 불티는 배관 접합부 및 연삭 접점에 적용했다.
