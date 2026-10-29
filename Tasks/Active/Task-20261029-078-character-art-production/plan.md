# Task-20261029-078

- **Title:** 원화 기반 토끼 제작 및 실제 Player 교체
- **Type:** feat
- **Assignee:** @MoHoDu
- **Branch:** feat/modeling/characters
- **Base:** dev

## 현재 목적 (2026-10-29 사용자 요청)
`References/rabbit-four-views.png`를 기준으로 연결된 Blender에서 모델링·UV·리깅·스키닝 후 Unity 실제 캐릭터를 교체한다. 이동·조작·기존 애니메이션·절단·래그돌·복구·카메라 표시 계약을 유지한다. 과거 v11의 실제 Player 변경 보류보다 이번 명시적 교체 요청이 우선한다.

## 보존·제약
제작 자료 정리(2026-10-30): 현재 제작 정본은 `Assets/_ThirdParty/CharacterArt/Rabbit/Source~/Source/V15/rabbit-material-states-v15.blend`. 원화/최종V12~V15/atlas/제작스크립트/보고서/최종비교를 Source~로 보관했다. 아래 원래Task경로는 과거결과 기록이며 현위치는 private Source~/README.md 및 cleanup-manifest.json 참조. 불필요 중간/백업과Unity구시험은 저장소밖 복구폴더로 정리했다.
2026-10-30 후속: 게임용 모델/9메시/텍스처/머티리얼/셰이더19개를 `_ThirdParty/CharacterArt/Rabbit/`로 GUID 이동. 코드·Player용Rabbit.prefab은 공개 위치 유지. private PR10은 사용자가 병합 완료. 공통 Player·NetworkManager·빌드씬11개 참조 조사. 실제 Player는0부상Clean/1~2Light/3~4또는Fallen·Retired사망Heavy 자동전환, 모델재생성/복구도 현재 상태를 재적용한다. 저장·푸시 명시 승인됨.
v15 제작: 기존 Clean 머티리얼 보존, Light/Heavy 2머티리얼 추가. 후속 피드백에 따라 중간 Light를 혈흔.60/오염.48로 강화(Heavy .94/.88), 총3상태 유지. 상태 선택·9스킨/절단 당시 조각 재질 보존 및 Editor2/PlayMode16 통과. [재질 사용법](References/material-states.md), 비교 씬 RabbitMaterialStates.unity. 자동 부상 단계 연동 규칙은 추가하지 않는다.
v14 후속 완료: 얼굴780→358tri/음영 완화, 팔·바지·신발180tri 추가 및 면별 음영으로 종이 질감 통일. 양쪽 발목184tri 추가, 정상9922tri. 560포즈 조건의 발목 끝 겹침·300신체포즈·16단면gap0, Editor2/PlayMode15 통과. 현재 제작본은 Source/V14/rabbit-final-v14.blend, Player는 같은 Rabbit.prefab/GUID를 사용한다. [직접 절단 확인](References/manual-dismemberment-check.md).
이전 v13: 얼굴·귀·의상 비율과 둥근 눈·봉제·패치·프릴 보완,9980tri. v12/v13을 보존하고 동일55본 휴지 리그와 게임 계약을 유지했다.
- 원화·최종제작본·게임기능참조를 보존한다. 이후 명시적정리요청에 따라 오래된중간/실패파일·백업은 현제작폴더에서제외했다.
- 전체 정상 모델 10000tri 이하, 수평 손목 T-pose, 정수리1.73m(귀 별도).
- 외부 유료3D 생성 없음. Blender MCP로 새 형상/관절을 만들고 computer-use로 실제 창을 확인한다.
- 현재 feat/modeling/characters 브랜치에 commit/push 요청됨. Unity는 D:/github/POC_CURTAINCALL 명시, 씬 미저장 변경 보존.
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
수학/자동 검사 통과와 원화 재현 완성도·사람의 최종 플레이를 구분한다. 원본의 촘촘한 봉제/패치 마감, 귀 보조 물리, 경사/계단·연속 접점/옷 자기겹침, 동시4인 성능 및 온라인 실기기 확인은 별도 기록한다.
