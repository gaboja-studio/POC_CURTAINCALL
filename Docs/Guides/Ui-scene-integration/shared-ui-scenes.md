# 공통 UI 씬과 공간 UI 구성

2026-10-20 사용자 추가 지시: 공통 UI는 별도 씬, 공간 전용 UI는 공간 씬에 두고 UI 루트로 정리한다.

## 현재 구성
```text
CommonPerformanceUI.unity (로컬 Additive)
└─ UI [CommonPerformanceUI]
   └─ CommonPerformanceCanvas (PerformanceHud 정본 인스턴스)
      ├─ C2 타이머
      ├─ C3 진행도
      └─ C1CheerGauge (C1 정본 인스턴스)
Tightrope.unity
└─ UI [PerformanceSceneBinding]
   ├─ TightropeBalanceCanvas (PerformanceHud 정본 인스턴스, P1만)
   ├─ BalanceGauge (기존 표시, 실행 중 대체)
   └─ 기존 세션/디버그/공연 조작 UI
HumanCannon.unity
└─ UI [PerformanceSceneBinding]
   └─ 기존 Canvas UI
```

## 소유권·로딩
- 공통 씬: Assets/Scenes/UI/CommonPerformanceUI.unity. Build Settings에 활성 씬으로 추가했다.
- 공간 UI가 활성화되면 CommonUiSceneLoader에 등록한다. 공통 씬은 Unity SceneManager로 로컬 additive 로드한다. 공간을 active scene으로 유지한다.
- 공간 binding은 CommonPerformanceUI의 C1/C2/C3를 런타임에 받아 현재 공간 데이터를 전달한다. 씬 사이 직렬화 참조는 없다.
- 두 공간이 잠깐 함께 있으면 active 공간을 우선하며, 없으면 마지막 등록 공간을 선택한다. 다른 공간 binding은 공통 표시를 갱신하지 않는다.
- 마지막 공간이 해제되면 공통 씬도 unload한다. Single 전환으로 사라져도 새 공간 등록 후 다시 로드한다. 로딩 도중 공간이 사라지는 경우도 다음 처리에서 해제한다.
- NGO VerifySceneBeforeLoading/Unloading에 공통 UI만 제외하는 필터를 연결한다. 기존 delegate는 일반 씬에 위임하고 관리자가 바뀌면 원복한다. 공통 씬에는 NetworkObject/EventSystem/입력 모듈이 없다.
- P1은 외줄 공간 binding이 직접 제어한다. 인간대포에는 P1 오브젝트를 만들지 않는다.
- 정본 PerformanceHud의 공통/공간별 불필요 자식은 인스턴스의 removed-child override로 분리했다. 원본 프리팹과 그래픽은 그대로다.
- 기존 개발 패널의 조작 기능은 보존했다. 개발 패널이 켜져 있으면 HUD를 가릴 수 있다.

## 편집과 확장
1. 공간 씬을 열고 CommonPerformanceUI.unity를 Additive로 열면 두 씬의 UI를 함께 편집할 수 있다. 씬 이름을 확인하고 해당 UI 루트 아래에서 작업한다.
2. Play는 공간 씬을 active로 둔다. 공통 씬을 미리 열어도 추가 복제하지 않는다. 공통 씬 단독은 게임 데이터가 없어 표시가 숨겨진다.
3. 새 공통 화면은 공통 UI 씬의 UI 루트에, 공간 전용 화면은 해당 공간 UI 루트에 둔다. 원본 프리팹 유무를 먼저 확인한다.
4. 시작/로비/대기실의 후속 화면 적용 때 공통 씬의 표시 문맥을 확장한다. 현재 공통 씬 연결 대상은 두 공연 씬이다.
5. Tools/Curtain Call UI/Install shared performance UI scenes는 최초 마이그레이션 도구다. 이미 생성된 공통 씬이 있으면 중복 실행을 거부한다.

## 검증
- 신규 PlayMode 9/9: 기존8개 + 두 공간 동시 등록 시 공통 씬1개, 한 공간 해제 시 유지, 마지막 공간 해제 시 unload, 공통 씬 P1 없음.
- 실제 오프라인 Play: 외줄→인간대포 Single 전환 후 씬2개(공간+공통), binding1개, 인간대포 P1 없음 확인.
- 실제 호스트에서 공통 UI 이름은 NGO load filter가 false, Tightrope는 true 반환 확인. 실제 원격 참가·NGO 전환은 미실행.
- 외줄 C1 첫 활성화 시 Awake 초기화에 대응해 최신 스냅샷을 재적용한다.
