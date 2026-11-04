# 추격자 재조립 단원 — 2D 검토 초안

## 최신 검토본
- [남성형4종 v5](../../../Assets/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/fbx-proportions-male-v5.png)
- [여성형6종 v5](../../../Assets/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/fbx-proportions-female-v5.png)
- [실제 남성 FBX 렌더](../../../Assets/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/fbx-male-front.png)
- [실제 여성 FBX 렌더](../../../Assets/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/fbx-female-front.png)

v3는 의상 참고, v4는 머리·손이 과장되어 체형 기준에서 제외한다.
v5는 FBX 위에 의상/재질을 제안한 2D 원화이며 실제 모델 수정본이나 최종 승인본이 아니다.
생성: 내장 image_gen. 정확한 프롬프트는 private Concepts/JointSeams/Source~/generation-prompts.json.

## 기준
1. 체형·키·손발 크기·관절·결손은 Pursuit/Models/Pursuit_*.fbx에 맞춘다.
2. 인형 재질은 CharacterArt의 실제 플레이어를 참고한다. 작은 도색 눈·고정된 입·매끈한 종이/천/도자기 면·로우폴리 의상. 캐릭터 정체성은 플레이어와 다르다.
3. 사람처럼 움푹 팬 얼굴이나 큰 아기 인형 머리로 바꾸지 않는다. 공포는 고정된 시선·낡은 표면·부품 조립의 어긋남에서 표현한다.
4. v3 의상의 색·직업별 차이를 유지하되 기존 몸을 따라 얇게 맞춘다. 체형이 다른 괴력사나 과장된 긴 손을 새로 만들지 않는다.
5. 목 리본 없음. 얼굴/가슴/등의 세로 봉제선 없음. 관절 틈을 가로질러 감싸는 짧은 실밥만 쓴다.
6. 혈흔은 마른 적갈색 얼룩·손자국·작은 비산. 먼지·때와 색을 구분하고 원래 의상/봉제/면이 읽히게 한다.

## 개체별 제작 제안
좌우는 캐릭터 본인 기준. 개수는 실땀 수가 아니라 봉제된 관절 수이며 아래 표가 후속 제작 기준 제안이다.
원화에서 실밥이 추가되거나 가려져도 자동으로 이 표를 변경하지 않는다.

| ID | 이전 역할·의상 | 관절 봉제 제안 | 개수 | 혈흔 중심·상대량 |
|---|---|---|---:|---|
| M01 | 곡예사·황토/먹색 마름모 | 목, 오른팔꿈치, 왼무릎 | 3 | 오른손/전완, 왼무릎·적음 |
| M02 | 괴력사 의상·자주/검정 세로무늬 | 왼어깨, 오른손목, 오른고관절, 왼무릎 | 4 | 왼가슴 손자국, 손/무릎·중간 |
| M03 | 악단원·검정/구리색 제복 | 오른어깨, 왼팔꿈치, 오른손목, 왼고관절, 오른무릎 | 5 | 오른손/전완/무릎, 왼옆구리·많음 |
| M04 | 안내원·갈색 코트/황토 바지 | 오른팔꿈치, 왼발목 | 2 | 오른손/팔꿈치, 왼정강이·적음 |
| F01 | 공중곡예사·바랜 청록 | 왼팔꿈치, 오른무릎 | 2 | 왼전완/오른무릎·적음 |
| F02 | 마임 단원·먹색/아이보리/황토 | 목, 오른어깨, 왼손목, 오른발목 | 4 | 오른어깨/왼손·중간 |
| F03 | 외줄 단원·포도주색 | 왼어깨, 오른고관절, 왼무릎 | 3 | 오른골반/왼정강이·중간 |
| F04 | 무용 단원·올리브/낡은 금색 | 오른어깨, 왼팔꿈치, 오른손목, 왼고관절, 오른무릎 | 5 | 왼옆구리 손자국/오른전완/무릎·많음 |
| F05 | 마술 단원·가지색 조끼/줄무늬 바지 | 목, 오른팔꿈치, 왼발목 | 3 | 남은 오른손/전완, 결손 어깨 아래·적음 |
| F06 | 자동인형 공연자·남색/황동 | 왼어깨, 왼손목, 오른고관절, 왼무릎 | 4 | 왼손/오른허벅지/왼무릎·중간 |

혈흔 가독성 제안: 가장 진한 개체도 화면상 약 1/4 이하에 국소적으로 집중하고 3/4 이상은 원래 재질이 보이게 한다.
이 수치는 AI 미술 제안/상한 가이드이며 생성 이미지의 측정값이나 확정 게임 설정값이 아니다.

## 실제 자료와 차이
- 현재 프리팹은 남4/여6. Models에 남5/6 파일도 있으나 이번 추격 프리팹 대상에 포함하지 않는다.
- Blender5.2.3에서 FBX10종을 직접 import하여 치수·정점·본 위치·SHA256을 Source~/fbx-proportion-audit.json에 기록했다.
- Male01 높이1.7783m, 정점560. 머리가 있는 소스 높이는 약1.768~1.778m, 머리가 없는 소스는 약1.49m. 전시 포즈의 월드 경계 기준, 사용자 게임 배치 스케일과 구분한다.
- CharacterArt 근거: BluePierrot/Source~/rebuild-v03-unity-clean.png, PinkGirl/Source~/unity-front.png, Bear/Source~/bear-unity-final.png, Rabbit/Source~/Previews/rabbit-v14-final.png 및 각 README.
- v5 M02 목에 표에 없는 봉제가 보인다. M03/F04 고관절 등 의상에 가려진 봉제와 좌우 일치도 후속 교정 대상이다.
- v5 F06은 오른쪽 상완 결손 길이를 소스보다 짧게 그렸다. 실제 모델의 남은 상완 길이를 반드시 보존한다.
- v5도 의상 두께·두상·신발이 소스와 픽셀 단위로 일치하는 정투상 도면은 아니다. 메시를 원화에 맞춰 키우지 말고 의상을 원본 메시 위에 맞춘다.

## Blender/Unity 후속
- 새 작업본을 만들고 기존 FBX·blend·메시·리그/가중치·GUID를 보존한다. 이번에는 import/render만 했고 save/export/모델 편집은 하지 않았다.
- 본 위치와 실제 메시 표면을 사용해 관절 봉제 마스크/얇은 실 메시를 만든다. 한 몸 전체의 정규화 좌표나 단순 높이만으로 모든 개체에 같은 링을 그리지 않는다.
- 기존 ReclaimedShell.shader의 p.x 중심 세로 seam/stitches 계산을 교체해야 한다. 새 의상만 추가하면 기존 봉제선이 남는다.
- PursuitAppearanceBuilder가 만드는 Reclaimed_Collar/UnfurledCollar 및 목 장식은 최신 리본 제거/목 관절 가독성 지시와 대조한다.
- 기존 혈흔은 CaptureCount×BloodPerCapture를 _BloodAmount에 넣는다. 개체별 기본 얼룩과 포획 누적을 구분하고 InjuryBloodEffects.IsVisible 설정을 함께 존중한다.
- 기존 애니메이션 및 포획/배치/네트워크 동작을 유지한다. 관절 굽힘·의상 관통·표면 미끄러짐·혈흔 상한·빛 아래 가독성은 실제 Unity에서 후속 검사한다.

