# Log

## 2026-10-29 v11 원화 기준 전면 재제작 착수
- 사용자: 기존 결과의 품질/마감 부족, 기존 파일 보존 후 새 제작, 프릴 단순화, 10000면 이하, 원본4방면 질감/그림체/측면 머리·의상, 손목을 꺾지 않은 T-pose 요구. 면 예산은 더 엄격한 전체10000tri 이하로 적용한다.
- 원본 `References/rabbit-four-views.png` 직접 확인. 현재 Blender는 `rabbit-rig-trial-v10.blend`. 이전 v10 모델과 시험 자산은 변경하지 않는다. 외부 유료 생성은 사용하지 않는다.
- doctor 실행: Task-077 중복으로 FAIL, Blender/Unity 연결 정상. start-work 실행: `feat/modeling/characters`의 다단계 이름을 스크립트가 거부하여 FAIL. Task plan과 현재 브랜치가 일치하므로 이름/다른 Task를 임의 변경하지 않고 동일 브랜치에서 진행한다.

## 2026-10-29 v09 손목·손 웨이트 및 프릴 간섭 보완
- 사용자 후속 진행 요청에 따라 v08을 보존하고 v09 Source/리그/9메시/Prefab/독립 시험 씬을 제작했다. 원화·실제 Player·기존 Controller는 변경하지 않았다. 동일 Task이므로 doctor 반복 및 commit/push 없음.
- 손목을 타원 단면으로 국소 정리하고 긴 변만 세분화/완화했다. 손가락 축까지 거리 감쇠를 추가해 손바닥 먼 정점이 엄지/손가락을 따라 끌리는 현상을 줄였다. 양팔+손 각1연결 성분/경계0/비매니폴드0, 정상10774tri/Unity9541정점. v08 대비1020tri(약10.5%) 증가로, 전신 면 감소는 아니다. 손바닥의 국소 각진 면과 깊은 굽힘의 압축은 남는다.
- 첫 v09 프릴 검사에서 어깨20도/상완45도/75도에4/16/48쌍 교차가 남았다. 해당 중간 blend를 보존하고 측면 여유48→56mm, 측면 지지 최대0.95/Shoulder20%·UpperArm80%로 조정했다. 최종 기본·목20도·어깨20도·상완20/45/75도 양팔6표본의 비공면 교차는 v08 총544쌍→v09 모두0건이다.
- `check-rabbit-frill-v09.py`는 BVH 후보 후 실제 삼각형 교차를 검사한다. 공면/프릴 자기겹침·머리/몸통·연속 움직임은 제외한다. 높은 팔 들기에서 프릴이 강하게 접히며 Idle의 옆 실루엣도 넓어졌으므로 최종 천 외형 합격으로 처리하지 않았다.
- 본59/Humanoid52/휴지 행렬 차이0/가중치 오류0/최대4영향, 재질 순서 및 마지막 cap 계약 유지. 절단4단계 각1폐곡선/열린 단면0, 다리cap30tri/팔cap22tri. 정수리1.73m/귀 포함2.161824m 유지.
- Unity `Trial/V09/RabbitCutTrialV09.prefab`:300 Editor 포즈의 finite/시험 접지,16단면 틈0/독립 분리 본/최대 복제오차0.00000107m 통과. 천·단면 비공면64표본 교차0건이며 프릴 검사와 별도다. 기존 일반 자동 테스트 스위트는 실행하지 않았다.
- `rabbit-v09-playmode-report.json`의 passed=true/failure=null 확인.4절단 조각 실제 이동 약0.518/0.641/0.467/0.389m, 복구/5상태 재활성 및 전신RB11·Joint10 유한 좌표 기본 검사 통과. 시험 종료 직후 background 설정이 true로 확인돼 편집 모드에서 다시 false로 복원했으며 Start 씬/dirty=false/playing=false를 확인했다.
- 최신 개별 렌더로 손·팔·프릴 비교판을 다시 생성했다(위v08/아래v09). 전체 `Previews/rabbit-v09-before-after.png`는 왼쪽v09/오른쪽v08. Blender `Source/rabbit-refined-v09.blend`는 최신 Source 씬을 표시하도록 저장했다.
- plan/todo 갱신, 이전 v08 계획/체크리스트와 v07 계획/결과를 History003/004에 원문 보존했다. 다음은 실제 애니메이션에서의 프릴/몸 간섭·손 국소 마감과 체형 콜라이더/접점이다. 최종 UV/혈흔3재질/실제 Player·온라인 통합은 미완료다.

## 2026-10-29 v08 손 연결·팔 굴곡·얇은 프릴
- 사용자 요청 범위를 plan/todo에 추가하고 v07/원화/실제 Player를 보존했다. 동일 Task이므로 doctor 반복 없음. v08 독립 Source/리그/9메시/Prefab/시험 씬을 생성했다.
- `soften-rabbit-v08.py` 첫 연결에서 중지·약지가 따로 남아3성분으로 검사 실패했다. 기존 손가락과 손바닥 사이에 실제 뿌리를 연장해 연결했다. 팔 전체 voxel remesh/decimate는 굴곡 확대 이미지에서 요철·부피 저하가 보여 그대로 채택하지 않았다. 손만 국소 재구성하고 팔의 규칙적인12각 고리·팔꿈치 지지 루프를 복원해 경계에 연결했다. 실패/중간 blend도 별도로 보존했다.
- 양팔+손 각1연결 성분/열린 경계0/비매니폴드0, 왼팔980tri/오른팔992tri. 새 표면에서 뜨는 기존 팔/손 입체 봉제선 연결 성분을 정리했다. 정상9754tri/Blender5299정점/Unity9038정점: v07의8762tri보다11.3% 증가, v06d10824tri보다9.9% 감소. 손 연결 비용을 숨기거나 전신 면 감소로 표현하지 않는다.
- `rig-rabbit-v08.py`: 통합 UpperArm 객체에도 위치별 손가락 본과 Hand→Proximal 전이를 명시했다. 팔꿈치 전이 폭28→16원화 단위, 지지 고리와 휴지 단면 보완. 본59/Humanoid52/이전 휴지 행렬 차이0/가중치 오류0/최대4영향. 기존 리그/Controller는 변경하지 않았다.
- 프릴3단은 두께5→1.8mm, 반경 고리3→4, 비선형 처짐·가장자리 말림·층별 큰 주름 위상 보완. 측면은 Shoulder/UpperArm을 일부 따라가게 했다. `inspect-rabbit-v08.py`의 팔0/45/90/120도와 손 펼침/75도 관절 굽힘/벌림14이미지, `inspect-rabbit-frill-v08.py`의 기본/목20도/어깨20도6이미지를 동일 카메라로 비교했다. 75도 일괄 굽힘은 시험 포즈이며 완성된 주먹 애니메이션이 아니다.
- 확대 이미지에서 손가락 뿌리 분리는 해소됐고 팔의 분절은 완화됐다. 손바닥/손목의 국소 각진 음영, 깊게 쥘 때 안쪽 압축, 팔을 높이 든 표본의 프릴/어깨 겹침은 남는다. 연결성·유한 좌표 통과로 최종 외형 합격 처리하지 않았다.
- `cut-refine-rabbit-v08.py`: 정상+누적Body4+Limb4, 각 절단 폐곡선1/열린 단면0, 다리cap30tri/팔cap22tri. 기존 접힌 천 UpperLeg 소유와 마지막 cap submesh 계약, 재질 슬롯 순서를 확인했다. 정수리1.73m/귀 포함2.161824m 유지.
- Unity `Trial/V08/RabbitCutTrialV08.prefab`: 300 Editor 포즈의 finite/시험 접지,16단면 틈0, 분리 본 독립성/최대오차0.00000107m, 조각RB2/Joint1 통과. 천·단면64비공면 표본 교차0건. 이는 프릴/어깨·천 자기겹침·연속 전환 검사가 아니다.
- 독립 `RabbitCutTrialV08.unity` PlayMode PASS:4절단 조각 이동 약0.621/0.696/0.431/0.459m, 복구/5상태 재활성/새 인스턴스/전신RB11·Joint10 기본 물리. 시험 동안 runInBackground=true, 종료 후 false 및 Start 씬 편집 모드/dirty=false 복원. 실제 게임/온라인 검사는 아니다.
- 확인 이미지: `Previews/rabbit-v08-before-after.png` 왼쪽v08/오른쪽v07. `rabbit-v08-hands-comparison.png`, `rabbit-v08-elbows-comparison.png`, `rabbit-v08-frill-comparison.png`는 위v07/아래v08. Blender는 `rabbit-refined-v08.blend`의 새 Source 씬을 표시한다. 혈흔3재질/실제 Player 교체는 미착수, commit/push 없음.

- v08 첫 check-work는 plan103줄/todo84줄의 문서 hard 한도로 FAIL, 컴파일은 선행 실패로 SKIPPED였다. v06 계획과 완료 준비를 `History/002-v06-plan-and-preparation.md`로 원문 그대로 옮기고 Python으로 원문 포함 일치를 확인했다. 미해결 사항과 현재 원칙은 본문에 유지했다. 지식 검사 자체는 OK였다.

## 2026-10-29 v07 관절 연결·프릴·천 형태 재제작
- 사용자 지적대로 v06d의 음영 보정만으로는 기계적인 분절/날카로운 형태가 남았다. plan/todo에 연속 연결·큰 비정형 천 접힘·실제 면 감소를 우선 반영한 뒤 Blender MCP로 제작했다. 같은 Task이므로 doctor 반복 없음.
- `soften-rabbit-v07.py`: v03을 별도 Source 씬/컬렉션에 복제. 어깨·상완·팔꿈치 구·전완·손목띠·리벳·꽃잎커프스·손바닥의 분절 대신 연속 팔을 생성했다. 각 팔120정점/연결 성분1/열린 경계0/비매니폴드0. 손가락 뿌리는 기존 별도 메시이므로 손 전체 용접 완료는 아니다. 발목 띠를 아이보리 천으로 교체하고 제거 부위의 일부 입체 봉제선도 덜어 냈다.
- 3단 노랑/아이보리 프릴은12주름 반복을5개의 넓고 비정형인 물결로 변경(48→32둘레 표본). 초기 물결은 너무 평평해 진폭을14→25mm로 조정했다. 옷자락은 칼날형 삼각 패널 대신 폭이 변하는 연속 천과 둥근 끝으로 재제작, 바지의 무작위 요철을 완화했다. 패치워크 텍스처 자체의 각진 색 블록은 유지했으며 후속 표면 마감 대상이다.
- `rig-rabbit-v07.py`: v05와 동일59본/52 Humanoid, 휴지 행렬 최대차이0, 가중치 오류0/최대2영향. 기존 관절 세분화를 무조건 추가하지 않았다. 정상 몸체8762tri(10824 대비19.1% 감소), Unity9155정점(13755 대비33.4% 감소). 무조건 subdivision으로 면을 늘려 부드럽게 만들지 않았다.
- `cut-refine-rabbit-v07.py`: 최초 재사용 cutter가 v07 이름 접미사를 붙이지 않아 refine에서 KeyError. 생성된8절단 메시를 확인해 v07 이름으로 정리하고, 실패한 신규 refine 컬렉션의 Original1개만 제거 후 재개했다. 스크립트에도 명명 수정을 반영했다. 기존 v03/v05/v06d 자산은 변경하지 않았다.
- 새4단계 cap은 각1폐곡선/열린 단면0, 다리30tri/팔18tri. v06d의 접힌 천 UpperLeg 가중치와 소유 계약을 재사용했다. 표면10재질의 이름/순서가 이전과 일치함을 확인해 기존 팔레트를 재사용했다. v07 원본·리깅·절단·최종 blend를 별도 보존했다.
- Unity `Trial/V07/RabbitCutTrialV07.prefab`과9메시/상태별 접지 지지점을 생성. 정수리1.73m/귀 포함2.161824m 유지. 300 Editor 포즈의 finite/시험 접지 통과,16단면 틈0, 복제 스킨 원본 본 참조0/최대오차0.00000107m, 조각RB2/Joint1. 기존 실제 Player/Controller/더미는 변경하지 않았다.
- 천/단면64포즈 비공면 교차0건: v06d의 강제 Crouch8표본104쌍도 이번 검사에서 해소됐다. 공면/천 자기겹침·연속 전환·래그돌 외형까지 합격한 것은 아니다. `rabbit-v07-cloth-report.json` 참조.
- 독립 `RabbitCutTrialV07.unity`에서 순차4절단/물리 이동/복구/5상태 재활성/새 인스턴스/전신RB11·Joint10 기본 검사 PASS. 처음에는 runInBackground=false 상태에서 frame2에 멈춰 있었다. 시험 동안만 true로 실행하고 완료 후 false 및 Start 편집 모드/dirty=false를 복원했다. 결과 `rabbit-v07-playmode-report.json`.
- 미리보기 cleanup에서 Camera.targetTexture가 연결된 RenderTexture를 먼저 해제하는 오류를 확인했다. v07 렌더/비교 스크립트와 생성기의 정리 순서를 PreviewScene 종료→RenderTexture 해제로 수정했다. 비교 재실행 후 새 콘솔 오류0건 확인. 이미지 결과 성공과 cleanup 오류를 구분해 기록한다.
- 직접 확인한 시각 기준: Blender 정면/후면/사선 및 Unity `Previews/rabbit-v07-before-after.png`(왼쪽 v07/오른쪽 v06d), `rabbit-v07-calibrated-stage*-*.png`. 팔의 분절과 프릴/옷자락의 날은 완화됐다. 큰 굴곡의 팔 부피, 손가락 뿌리, 프릴 두께/처짐, 패치 무늬는 아직 최종 마감이 아니다.
- Blender는 `rabbit-refined-v07.blend`의 Source 씬을 열어 새 외형을 볼 수 있게 했다. 다음은 남은 연결/굴곡/천 마감과 체형 콜라이더·실제 접점 검증. 혈흔3재질 및 실제 게임/온라인 교체는 미착수, commit/push 없음.

## 2026-10-29 v06 단면 옷자락·음영·키 보완
- 사용자 승인에 따라 plan/todo를 단면 침범→smooth normal→기존 키 비교→회귀 검사 우선으로 갱신했다. 원화/v05b/기존 더미/실제 Player는 보존했다. doctor 재실행 및 commit/push 없음.
- 단면의 뾰족한 형상은 cap이 아니라 남은 옷자락이었다. v06 단일 접힘, v06b 수평 이중 접힘은 돌출을 다른 방향으로 옮겨 불충분했다. v06c는 천의 세로 방향 이중 접힘으로 시각 개선됐으나64포즈에서 천/단면 교차682쌍이 검출되어 전체 합격에서 제외했다.
- v06d는 접힌 천을 다리 절단 평면보다 몸통 쪽2.5cm 이상 물리고 남은 UpperLeg에 가중치1로 연결했다. 천은 Body 소유이며 분리 사지에 들어가지 않는다. 정상 의상은 그대로다. `Source/refine-rabbit-v06c.py`의 VERSION=v06d 실행으로 재현, `Source/rabbit-refined-v06d.blend` 보존.
- smooth normal과40도 sharp edge, 평평한 cap을 적용했다. Blender set_sharp_from_angle이 smooth 플래그를 바꾸므로 각도 설정 뒤 cap의 flat 설정을 적용한다. 정상 몸체10824tri 유지, Unity 정점23070→13755(약40.4% 감소). 절단 후 천 접힘의 세분화는 Body1+152tri, Body2~4+304tri이며 모든 상태의 면 수가 그대로라고 표현하지 않는다.
- 최신 독립 자산 `Trial/V06d/RabbitCutTrialV06d.prefab` 및9개 메시를 생성했다. 이전 v06/v06b/v06c도 보존한다. 기존 Avatar/Controller/팔레트 및 정상+Body4+Limb4 계약을 재사용한다.
- 키: GameSettings 목표1.73m, 토끼 원본 정수리1.727437m이므로 후보 루트 scale1.00148368. 귀 포함2.161824m. 기존 모델은 비교 인스턴스만 실제 메시 높이1.73m로 정규화했고 원본 자산은 수정하지 않았다. 휴지 자세 어깨는 기존1.245978m/토끼1.283383m, 손은1.241338m/1.270634m. 같은 Idle 포즈 비교 PNG에서 발 기준과 정수리 비율을 확인했다. 실제 슬롯의 귀 포함 bounds 자동 축소 방지는 아직 통합 전이다.
- 측정 도구 보완: 기본 BakeMesh+TransformPoint는 토끼 높이를2.165031m로 측정했다. true 옵션은2.161824m로, 직접 bone matrix×bindpose 가중 계산과 최대오차0.000000596m(기본 옵션0.003218m). 키 비교·포즈 검사·렌더에 true를 명시했다. 엔진 내부 원인 확정은 아니며 기존 측정치를 그대로 최종값으로 쓰지 않았다.
- 키 보정 후 `test-rabbit-cuts-v06d.cs`:300 CPU 포즈의 유한 좌표/적용 지상 접지 통과,16단면 표본 틈0, 원본 본 참조0, 분리 복제 최대오차0.00000107m, 조각마다RB2/Joint1. `Source/rabbit-v06d-cut-test-report.json` 참조.
- `rabbit-v06d-cloth-report.json`:64비공면 교차 표본 중56개0건. stage3/4 강제 Crouch의8개는 총104삼각형 쌍 교차가 남는다. 실제 양다리 손실 이동은 Crawl이고 해당 표본은0건이다. 공면 겹침/천 자기겹침/실제 전환은 검사하지 않아 전체 의상 합격으로 기록하지 않았다.
- 별도 `RabbitCutTrialV06d.unity`에서 기존 Probe를 v06d에 연결: 순차4절단 실제 조각 이동·정상 복구·5상태 재활성·새 인스턴스 초기화·전신RB11/Joint10 기본 물리 검사 PASS. `Source/rabbit-v06d-playmode-report.json`. 종료 후 Start 씬 편집 모드로 복원했다. 실제 Player/게임 재시작/온라인 검사는 아니다.
- 최신 시각 기준은 키/스케일 처리 보정 후 `Previews/rabbit-v06d-calibrated-stage*-*.png`, 기존 모델 비교는 `rabbit-v06d-height-comparison.png`. 이전 v06d-stage 이미지는 키 보정 전 기록이다. 기어가기 단면의 기존 돌출은 해소됐으나 천 접힘/손가락/콜라이더 최종 품질은 미완료다.
- 다음: 남은 의상 간섭·손가락 변형·체형 래그돌 맞춤, 실제 접점/전환 검증 후 깨끗함/소량 혈흔/다량 혈흔+오염 재질. 혈흔 재질 및 실제 Player 교체는 아직 하지 않았다.

## 2026-10-29 v05 변형·접지 보완 및 절단 조기 시험
- 요청한 깨끗함/소량 혈흔/다량 혈흔+오염 3단계 재질 목표를 plan/todo에 추가했다. 의상 형태 변경·단계별 메시 복제 없음. 오염 재질 자체는 아직 만들지 않았으며 게임 전환 조건도 임의로 정하지 않았다.
- 같은 Task에서 doctor를 불필요하게 다시 실행했다. 기존 Task-077 중복 외에는 정상이며 이번 제작 차단 없음. 이후 반복하지 않았다.
- v04를 보존하고 `rig-rabbit-v05.py`로 별도 v05를 만들었다. 좌표 부호 대신 원래 부품의 좌우 이름으로 웨이트/절단 소유를 지정해 혼합 면6→0. 칼라의 Neck 영향, 옷자락의 상부 다리 영향, 팔꿈치/무릎 주변 세분화 보완. 6038 vertices/10824 triangles, 웨이트 오류0. Unity 삼각형도10824로 일치한다. v04의20개 삼각형 차이 원인을 확정한 것은 아니다.
- v05 FBX에 v04 humanDescription 전체 복사 시 이전 skeleton 루트 이름 때문에 valid=true/human=false였다. 새 자산의 실제 Transform 휴지값으로 skeleton을 재구성해 valid/human 모두 true 확인. `RabbitAnimationV05.prefab` 및 시험 FBX는 기존 모델과 독립이다.
- v05 실제 CPU 포즈60표본에서 기어가기 침투가 최대16.7→8.5cm로 감소했지만 잔여 침투가 있었다. `RabbitTrialGrounding.cs`는 같은 웨이트별 볼록 껍질 지지점(상태별1584~2910개)으로 최저점을 구해 시각 뼈 부모만 올린다. 이동/충돌 판정은 변경하지 않으며 점프/매달림/비행/사망/줄타기에는 적용하지 않는다. 런타임 전체 BakeMesh를 하지 않지만 CPU 비용·경사/계단/전환은 후속 검사 대상이다.
- `cut-rabbit-v05.py`는 부위 소유별 절단 평면과 폐곡선 캡을 생성한다. 바지/구두/팔/손이 해당 사지에 속하고 옷자락은 Body 소유로 유지된다. 4단계 모두 절단 경계1개, 열린 단면 edge0, 다리 cap34tri/팔 cap28tri. 모든 누적 body/limb는 원본 표면10개 뒤에 cap을 추가하며 빈 이전 cap 슬롯도 보존한다.
- 첫 절단 렌더에서 의상 색 손실 발견: Blender 재질 슬롯 clear가 면 인덱스를0으로 만들었다. 인덱스 보관/복원으로 수정하고 `cut-rabbit-v05b.py` 및 `rabbit-rig-cuts-v05b.blend`/`rabbit-v05b-cuts.json`로 수정 전 결과를 보존한 새 산출물을 만들었다. Unity에서는 정점 배열과 마지막 cap이 동일함을 확인하고 자체 생성한8메시의 submesh 분류만 수정해 GUID를 유지했다. 새 Mesh 할당 후 Blender 그룹 이름 목록이 빈 문제도 원본 순서로 복원했다.
- 최신 후보 `Assets/Scenes/Tests/CharacterArt/Prefabs/Rabbit/Trial/RabbitCutTrialV05.prefab`: Original+Body4+Limb4, 동일59bones/bindposes. `PaletteV05`는 고유 단색을 작은 BaseMap으로 옮겨 기존 Tint(white)와 분리 조각에서 색을 보존한다. 기존 v04 재질·더미·Controller는 변경하지 않았다. 팀색 분리 및 오염 상태는 후속이다.
- `test-rabbit-cuts-v05.cs`: 5신체×15동작×4시점=300 CPU 포즈 표본. 지상 보정 대상의 minY>=-0.001m 및 모든 좌표 finite. 단면4단계×Walk/Crouch/Crawl/Hanging의 양방향 경계 틈0. 실제 BakePiece 호출에서 각 조각의 원본 본 참조0/RB2/Joint1, 복제 포즈 최대오차0.00000123m. Editor 검사이며 물리 시뮬레이션은 별도다. 최신 결과 `Source/rabbit-v05b-cut-test-report.json`.
- 별도 `RabbitCutTrialV05.unity`에서 `RabbitTrialProbe`로 PlayMode 검사: 실제4회 절단/조각 이동, 정상 복구,5상태 재활성,새 인스턴스 초기화,전신 RB11/Joint10 및60physics tick 유한/바닥 이탈 검사 통과. 최초 결과 `Source/rabbit-v05-playmode-report.json`. 재질 보존·접지 캐시 수정 후 `Source/rabbit-v05b-playmode-report.json` 회귀도 PASS. 실제 Player 슬롯/게임 재시작/원격 표시 검사를 뜻하지 않는다. 종료 후 Start 씬 편집 모드/dirty=false 복원, 재연결된 Blender의 v05b 파일과 단면 미리보기도 확인했다.
- 현재 시각 기준 `Previews/rabbit-v05b-stage*-*.png`. 단면과 패치워크 보존을 직접 확인했다. 긴 옷자락의 절단 후 실루엣·손가락 자세·래그돌의 넓은 바지/귀 맞춤은 미완료이며 외형 최종 승인 아님. 모델의 실제 슬롯 교체·온라인 시험·commit/push 없음.

- v01~v04 제작·연결·시행착오 원문: [History/001-v01-v04-production.md](History/001-v01-v04-production.md)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-29 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
