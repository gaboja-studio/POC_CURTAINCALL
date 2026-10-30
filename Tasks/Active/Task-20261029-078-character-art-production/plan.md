# Task-20261029-078

- **Title:** 토끼 모델 교정 및 곰 플레이어 추가
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/bear-player
- **Base:** dev

## 현재 목적 (2026-10-29 사용자 요청)
2026-10-30 곰 3D 후속: bear-turnaround-v01을 기준으로 토끼와 같은 종이·봉제 재질, 55본 Humanoid 및 정상/절단9메시 제작 흐름을 사용해 곰을 제작하고 플레이어 모델 카탈로그에 추가한다. 짧고 넓은 체형·둥근 귀·주둥이·녹색 모자와 의상을 새 형상으로 맞춘다. 제작 및 게임 리소스는 private CharacterArt/Bear에 보관한다.
2026-10-30 분홍 소녀 추가 요청: 원화 #3·#2의 얼굴/땋은 머리/리본/드레스와 후면을 참조해 이전 시트와 같은 정면/좌측/후면/우측 A-pose 다면도를 제작한다. References/pink-girl-turnaround-v01.png와 프롬프트 보존. 후면 세부는 제안이며 이번 산출물은 2D 참고 시트다.
2026-10-30 파란 삐에로 추가 요청: 원화 #3·#2에서 얼굴/머리/의상과 후면을 참조하고 곰 다면도의 배치·재질 스타일을 맞춘 정면/좌측/후면/우측 A-pose 2D 시트를 제작한다. References/blue-pierrot-turnaround-v01.png와 프롬프트를 보존하고 3D 제작·Unity 적용은 별도 단계로 둔다.
2026-10-30 추가 요청: 원화의 곰 캐릭터도 토끼와 같은 형식으로 2D 다면도만 제작한다. 원화 #3·#2는 곰의 외형/후면 참고, 기존 rabbit-turnaround-v02는 시트 배치/표면 스타일 참고다. 정면·좌측·후면·우측 동일 축척 A-pose를 생성·시각 점검하고 References/bear-turnaround-v01.png와 프롬프트를 보존한다. 곰의 3D 제작·Unity 연결은 이번 산출물에 포함하지 않는다.
`References/rabbit-four-views.png`를 기준으로 연결된 Blender에서 모델링·UV·리깅·스키닝 후 Unity 실제 캐릭터를 교체한다. 이동·조작·기존 애니메이션·절단·래그돌·복구·카메라 표시 계약을 유지한다. 과거 v11의 실제 Player 변경 보류보다 이번 명시적 교체 요청이 우선한다.

## 보존·제약
V16: 최신origin/dev e58b207f를51150752로병합(목차두항목보존/private main포인터유지). 4인슬롯에따른버건디/블루/머스터드/세이지 의상팔레트와서로다른패치문양을제작한다. 동일55본/9메시/9922tri, 슬롯지정/재생성/분리조각/3부상표면계약유지. 이번요청에는새제작커밋·푸시요청없음.
제작 자료 정리(2026-10-30): 현재 제작 정본은 `Assets/_ThirdParty/CharacterArt/Rabbit/Source~/Source/V15/rabbit-material-states-v15.blend`. 원화/최종V12~V15/atlas/제작스크립트/보고서/최종비교를 Source~로 보관했다. 아래 원래Task경로는 과거결과 기록이며 현위치는 private Source~/README.md 및 cleanup-manifest.json 참조. 불필요 중간/백업과Unity구시험은 저장소밖 복구폴더로 정리했다.
2026-10-30 후속: 게임용 모델/9메시/텍스처/머티리얼/셰이더19개를 `_ThirdParty/CharacterArt/Rabbit/`로 GUID 이동. 코드·Player용Rabbit.prefab은 공개 위치 유지. private PR10은 사용자가 병합 완료. 공통 Player·NetworkManager·빌드씬11개 참조 조사. 실제 Player는0부상Clean/1~2Light/3~4또는Fallen·Retired사망Heavy 자동전환, 모델재생성/복구도 현재 상태를 재적용한다. 저장·푸시 명시 승인됨.
v15 제작: 기존 Clean 머티리얼 보존, Light/Heavy 2머티리얼 추가. 후속 피드백에 따라 중간 Light를 혈흔.60/오염.48로 강화(Heavy .94/.88), 총3상태 유지. 상태 선택·9스킨/절단 당시 조각 재질 보존 및 Editor2/PlayMode16 통과. [재질 사용법](References/material-states.md), 비교 씬 RabbitMaterialStates.unity. 자동 부상 단계 연동 규칙은 추가하지 않는다.
v14 후속 완료: 얼굴780→358tri/음영 완화, 팔·바지·신발180tri 추가 및 면별 음영으로 종이 질감 통일. 양쪽 발목184tri 추가, 정상9922tri. 560포즈 조건의 발목 끝 겹침·300신체포즈·16단면gap0, Editor2/PlayMode15 통과. 현재 제작본은 Source/V14/rabbit-final-v14.blend, Player는 같은 Rabbit.prefab/GUID를 사용한다. [직접 절단 확인](References/manual-dismemberment-check.md).
이전 v13: 얼굴·귀·의상 비율과 둥근 눈·봉제·패치·프릴 보완,9980tri. v12/v13을 보존하고 동일55본 휴지 리그와 게임 계약을 유지했다.
- 원화·최종제작본·게임기능참조를 보존한다. 이후 명시적정리요청에 따라 오래된중간/실패파일·백업은 현제작폴더에서제외했다.
- 전체 정상 모델 10000tri 이하, 수평 손목 T-pose, 정수리1.73m(귀 별도).
- 외부 유료3D 생성 없음. Blender MCP로 새 형상/관절을 만들고 computer-use로 실제 창을 확인한다.
- 현재 feat/bear-player 브랜치에 토끼 수정/곰 추가 분리 commit 및 push/dev PR 요청됨. Unity는 D:/github/POC_CURTAINCALL 명시, 씬 미저장 변경 보존.
- 온라인2인, 최종 미술·플레이 감각 검토는 별도 남은 항목이다.

## 진행
1. Blender MCP·Editor·작업 상태 확인: 완료. doctor의 third-party 오류는 샌드박스 오판, 외부 읽기에서 초기화 확인. Task-077 중복은 기존 이슈.
2. v11 초안을 보존하고 v12 새 메시/55본·52 Humanoid/스킨·표면 제작: 9792tri, 4986 Blender 정점.
3. 정상+누적Body4+Limb4 생성: 각 절단 폐곡선1, 열린 단면0, 캡 마지막 submesh. 절단된 쪽 옷자락은 몸통에 접어 유지.
4. Unity Rabbit.prefab 생성: 정상5248정점/9792tri, 표면2슬롯+단계별캡. 원화색과 플레이어 프릴/트림색 분리.
5. 300 Editor 포즈·16단면·독립 분리 본 검사 통과. 물리/실Player 검사는 별도 진행.
6. 실제 Player의 modelPrefab=Rabbit, fitToHeight=false, squashWhenCrouched=false 적용. 기존 모델 보존/이전 설정 JSON 기록.
7. 실제 Player Editor2/PlayMode15·카메라 숨김/복원 통과. Blender/Unity 시각 확인, check-work 안전성·문서·규칙·컴파일 PASS. 테스트 재실행은 생략하고 별도 통과 보고서와 한계를 기록했다.

## 경로
- 현재 제작: `Source/V14/`, 재현순서 refine-rabbit-v14 → cut-export-rabbit-v14 → refine-rabbit-ankle-fit-v14 → import-rabbit-v14. 이전 v12/v13 보존.
- 게임 프리팹: `Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab`.
- 기존호환 조사: `References/character-compatibility.md`.
- 이전 계획·미완료 세부항목: [History/006-before-v12-plan.md](History/006-before-v12-plan.md), [History/006-before-v12-todo.md](History/006-before-v12-todo.md).

## 검증 경계

곰 후속 제작 정본은 `Assets/_ThirdParty/CharacterArt/Bear/Source~/bear-buttons-fixed.blend`, 토끼 단추 교정 정본은 `Assets/_ThirdParty/CharacterArt/Rabbit/Source~/rabbit-buttons-fixed.blend`. 곰 허리 v02와 두 캐릭터의 단추·오른팔 봉제선 교정을 포함한다. Unity Bear.prefab/카탈로그에 추가하며 기존 슬롯 기본값은 유지한다. 후속 저장 요청에 따라 토끼 수정과 곰 추가를 각 저장소에서 분리 커밋하고 dev PR 1개로 제출한다. 이전 PR238 병합 결과와 기존 작업 기준의 트리가 같음을 확인하고 최신 origin/dev에서 새 브랜치를 만들었다. 비공개 리소스 PR 병합은 사용자가 진행한다.
수학/자동 검사 통과와 원화 재현 완성도·사람의 최종 플레이를 구분한다. 원본의 촘촘한 봉제/패치 마감, 귀 보조 물리, 경사/계단·연속 접점/옷 자기겹침, 동시4인 성능 및 온라인 실기기 확인은 별도 기록한다.

## 플레이어 외형 설정
- 플레이어 1~4의 기본 모델과 의상을 설정 에셋에서 지정하고 로비에서 개인이 변경한다. 호스트가 선택을 검증하고 신규 참가자에게 미사용 의상을 우선 배정한다.
- 모델 옵션에는 기본 프리팹과 선택적인 의상별 프리팹을 연결한다. 추후 모델도 기존 HumanoidModel·애니메이션·절단 계약과 부상별 표면 구현을 갖춰야 한다.
