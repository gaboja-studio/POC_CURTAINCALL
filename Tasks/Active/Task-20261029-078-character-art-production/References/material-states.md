# 토끼의 세 가지 표면 상태

2026-10-30 v15. 모델/UV/리그/절단 형상은 v14 그대로다.

| 상태 | 머티리얼 | 외형 |
|---|---|---|
| Clean | RabbitCostume.mat (기존) | 깨끗한 원래 표면 |
| Light | RabbitCostume_Light.mat (추가) | 중간 혈흔·오염: 핏방울, 번진 피, 흙 얼룩 |
| Heavy | RabbitCostume_Heavy.mat (추가) | 넓게 스민 피, 마른 혈흔과 심한 오염 |

머티리얼·게임용 모델 리소스 폴더: `Assets/_ThirdParty/CharacterArt/Rabbit/`. GUID 이동으로 기존 연결을 보존한다. Player용 Rabbit.prefab·코드는 공개 위치를 유지한다.
Light는 사용자 후속 요청에 따라 최초보다 강화했다. 셰이더 수치 Blood .60 / Dirt .48, Heavy .94 / .88. 수치는 실제 면적 백분율이 아니다.

## Unity에서 보기/바꾸기
1. `Assets/Scenes/Tests/CharacterArt/RabbitMaterialStates.unity`를 열면 세 상태를 같은 조명에서 비교할 수 있다.
2. Hierarchy에서 `01 Clean`, `02 Light`, `03 Heavy` 중 하나를 선택한다.
3. Inspector **Rabbit Surface States → State**를 Clean/Light/Heavy로 바꾼다.
4. 실제 조작 씬 `RabbitGameplay.unity`에서는 Player 아래 `Rabbit Editor Preview`(편집) 또는 생성된 `Rabbit`(Play)의 같은 항목을 사용한다.

상태 컴포넌트가 정상 몸체·누적 절단 몸체4·분리용 사지4의 표면 슬롯0/1을 함께 바꾼다. 장식의 플레이어 색과 단면 재질은 보존한다. 이미 떨어진 사지는 절단 당시 머티리얼을 유지한다. 원본 프리팹 기본값은 Clean이다.

실제 Player에서는 HumanoidPlayerPresentation이 PlayerCondition.LostCount/NetworkPlayer.State를 읽어0부상Clean/1~2부상Light/3부상이상 또는사망(Fallen·Retired)Heavy를 자동적용한다. 복구·초기입장·모델재생성도 현재 상태로 맞춘다. 이미 분리된 조각은 당시 재질을 유지한다. 추가 네트워크변수 없이 기존 동기화된 부상/생존상태에서 표시를 계산한다. 수동3상태비교는 독립 비교씬에서 사용한다.

Blender: `Assets/_ThirdParty/CharacterArt/Rabbit/Source~/Source/V15/rabbit-material-states-v15.blend`, RabbitSkin_v14 슬롯0 Clean/1 Light/2 Heavy. 원본 packed atlas와 편집 가능한 절차적 노드를 사용한다. Unity 게임 외형은 Bloodied Paper 셰이더가 정본이며 두 엔진의 노이즈 무늬는 픽셀 단위로 동일하지 않다.

검증: Editor2/PlayMode16 통과,9스킨 교체·캡/팀색 보존·Light 조각 유지/새 Heavy 조각 생성·복구 확인. 근거 `Source/V15/*tests.json`, 비교 이미지 `Previews/rabbit-v15-material-states.png`.
