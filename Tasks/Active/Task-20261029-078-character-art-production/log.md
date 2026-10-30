# Log

## 2026-10-30 네 플레이어 기본 외형 설정
- 카탈로그 기본값을1토끼/노랑·2곰/초록·3삐에로/파랑·4소녀/분홍으로 저장했다. 곰/삐에로/소녀의 원본 재질 색과 무늬0을 설정에 반영했다. 의상 무늬를 별도 편집할 수 있고 기존 미설정 카탈로그는 의상 번호 무늬로 호환한다.
- UI Toolkit Inspector에서 플레이어별 모델/의상을 이름 드롭다운으로 선택하고 Undo/직렬화로 저장한다. 실제 패널에 붙인 사본의8드롭다운 변경 확인. 캐릭터 PlayMode3/3와 실제 로비 기본 외형·의상 변경/시작 유지1/1 통과. 기존 로비 테스트의 고정 의상 번호는 설정 기본값 기준으로 바꿨다. 다음 참가/생성 적용 및 로비 개인 선택 안내: References/player-appearance-settings.md.

## 2026-10-30 다리 절단 시 치마 보존만 수정
- 사용자 요청에 따라 export.py의 치마 높이 압축만 제거했다. 치마/프릴/봉제5,788정점·10,608삼각형의 위치와 길이가 모든 Body1~4에서 원본과 같고 분리된 Limb1~4에는 치마0점임을 검사한다. 절단된 쪽의 기존 Hips 연결은 유지했다.
- cuts_only export와 import-cut-bodies.cs로 네 절단 몸체/접지 데이터만 갱신했다. 원본/분리 사지의 JSON SHA 동일, Unity 원본·사지·FBX·재질·셰이더16리소스의 파일 SHA 동일을 확인했다. 정상 외형이나 조작 코드는 바꾸지 않았다.
- 최종 PlayMode1/1·300포즈/16단면gap0 통과. 실제 Play 프레임68→2944→4742에서1다리→2다리→정상 복구를 확인하고 skirt-intact-one-leg.png/two-legs.png로 치마 보존을 촬영했다. Play 종료 후 편집 상태로 복원. 이번 변경은 커밋/푸시하지 않았다.

## 2026-10-30 분홍 소녀 측면·정수리·후방 재작업
- Unity 동일5시점에서 관찰한 앞머리 관통 띠/떨어진 모자 조각/판형 리본을 교체했다. 머리카락은 실제 머리 표면에 닿는 앞머리와 사선 후두부 덩어리, 모자는 머리에 맞는 닫힌 연속 주름, 리본은 두께 있는 접힘/뒷면으로 만들었다. 뒤 허리 리본도 같은 방식으로 보완했다.
- 이번에는 Computer Use 창의 실제 새 메시와 편집 모드가 정상 갱신됨을 확인했다. 측면에서 머리띠294정점을 native G Y 0.006으로 조정하고 MCP 전후 수치로 검증했다. 모달 화면/정점 근거는 Source~/oblique-manual-refinements.json. 이전 GPU 캡처 문제가 항상 해결된다는 의미는 아니다.
- 첫 Unity 후면 검사에서 후두부 피부가 머리카락보다 최대 약7mm 바깥인 문제를 추가 발견했다. 머리카락/모자/리본의 뒤쪽 접합을 함께 보정했고35개 후방 표본에서 머리카락이 피부를 최소9.63mm 덮는다. Blender와 Unity의 측면/정수리/후방·전신을 재확인했다. 정본46,374tri/55본/52Humanoid, 같은GUID9메시·4의상·부상 계약 유지.
- 최종 PlayMode1/1(이동/점프/의상/부상/절단/복구),300포즈·16단면gap0·4독립 분리 스킨 통과. 단추840표본 최대1.00mm,발목96점/구두 안6.09mm 유지. Avatar유효/셰이더 메시지0,시험 씬 stopped/dirty=false. 수정 전후5시점은 Source~/History/before-oblique-fix 및 Oblique-v02에 보관. 온라인2인/동시4인 성능/최종 미술 승인은 별도, 이번 변경은 미커밋이다.

## 2026-10-30 삐에로 리소스 병합 후 푸시 완료
- 사용자 PR15 병합 통보 후 GitHub MERGED/949c92ed와 private ac8199f9의 최신 origin/main 포함을 확인했다. ThirdParty 훅 통과 후 공개 feat/blue-pierrot-player(9ebb3171/48c140db)를 푸시하고 upstream을 같은 원격 브랜치로 설정했다. 체크아웃을 바꾸지 않아 분홍 소녀의 공개/비공개 미커밋 변경과 현재 편집 상태를 보존했다. 기존 검증 결과를 재사용했으며 게임 파일 수정·추가 테스트는 없다.

## 2026-10-30 분홍 소녀 플레이어 제작
- 이전 삐에로를 공개9ebb3171/48c140db, private ac8199f9로 분리 저장했다. private resource/blue-pierrot-player 푸시와 PR15 생성/연결 완료. 사용자 직접 병합 방침과 상위 pre-push의 private main 포함 조건 때문에 공개 브랜치 푸시는 PR15 병합 대기다. 이후 공개 feat/pink-girl-player/private resource/pink-girl-player에서 새 제작을 진행했다.
- 착수 doctor1회: 샌드박스의 submodule/Unity/gh 접근 오판은 허용된 외부 읽기로 구분했고, 기존 Task077/078/079 번호 중복 경고는 남았다. Blender5.2.3/addon1.8과 Unity6000.3.25f1/Pipeline 연결을 확인했다. package/Editor 버전은 변경하지 않았다.
- pink-girl-turnaround-v01에서 새 얼굴/일체형 귀, 주황 앞머리/닫힌 후두부, 실제 세 가닥 양갈래, 입체 보닛/앞뒤 리본, 퍼프 소매·맨팔·3겹 페티코트·분홍 겉치마·맨다리·구두를 제작했다. 원화와 Blender/Unity 정면·측면·후면을 비교했다. 측면의 보닛 카드형 틈은 닫힌 안감/입체 접힘으로 재구성하고 귀 주변 머리를 정리했다.
- Computer Use의 Blender 편집 모드 G Z/비례 G X로 꼭대기/양갈래 끝을 직접 수정했고 MCP에서 전후 정점780개를 기록했다. Windows GPU 캡처는 옛 모델/흰 화면에 고정되어 회복됐다고 판단하지 않았다. 관찰된 포커스/모달/실제 정점 좌표와 갱신되는 MCP look을 함께 사용했다. manual-refinements.json과 관련 함정 문서에 한계를 보관했다.
- 정상44,760tri/55본/52 Humanoid, 정상+4몸체+4사지. 머리/귀·머리카락 각각 연결성분1/열린 경계0. 무가중치/4본초과/소유 부위 오류0. private CharacterArt/PinkGirl에 제작본/리소스, 공개 PinkGirl.prefab/Gameplay와 카탈로그 **분홍 소녀**를 추가했다. 기존4슬롯 기본값과 기존 조작/애니메이션/4의상/0Clean·1~2Light·3이상/사망Heavy 계약 보존.
- 실제 렌더에서 발견한 허리 위 허벅지 노출, 발목/구두의 Foot 가중치 불일치, 절단 코트 접기의 치마 반전, 임의 UV에 의한 삼각형 혈흔을 수정했다. 단추 접촉은 실제 옷 정점 추가와 동일 가중치로 보정. 겹치마는 단조 높이 압축/Hips 연결, 패널 UV는 연속 좌표를 쓴다.
- 최종 PlayMode 분홍 소녀1/1 통과,300포즈·16단면gap0·4독립 분리본/2Rigidbody·1Joint 통과.840단추 표본 최대1.00mm, 발목48끝점 구두 안쪽 최소6.09mm와 Unity 정상/분리사지96가중치 일치. 최종 Avatar유효/Humanoid, 셰이더 메시지0.
- 실제 프레임980→1251→3266→4211에서0Clean→1Light→3Heavy→0Clean과 복구 확인. 정상/부상 및 네 방향 렌더, 편집/런타임 blend 두 정본, 재현 스크립트와 보고서를 Source~/README.md에 정리했다. Unity PinkGirlGameplay 편집 상태/dirty=false/runInBackground=false로 복원했다. 온라인2클라이언트·동시4인 성능·극단적 치마 자기겹침·최종 미술 승인은 별도이며 소녀 변경은 아직 커밋하지 않았다.
- check-work 최초 검사는 기존 캐릭터 함정 문서에 추가한1줄이60줄 제한을 넘어 문서 단계에서 중단됐다. 새 겹치마 함정을 별도 문서로 분리하고 인덱스를 연결한 뒤 재검사한다.

## 2026-10-30 삐에로 v03 전체 재제작
- 사용자가 v02 귀 분리·열린 머리판·가느다란 손목을 지적하고 전체 재제작 요청. 기존 메시를 재활용하지 않고 얼굴/귀, 닫힌 머리 뭉치, 몸통·소매·연결 장갑·바지·발목·부츠·프릴을 새로 제작했다. 둥근 단발 초안도 원화와 다르다는 피드백을 받아 비대칭 뾰족한 실루엣·불규칙한 뒷머리·붉은 앞머리로 수정했다.
- Blender MCP와 Computer Use의 측면 전환·원화 패널 비교를 사용. 머리/귀 한 성분, 머리카락 한 성분, 장갑 각각 한 성분이며 열린 경계0. 손목 지름98mm/팔꿈치170mm, 발목 휴지 겹침은 부츠31mm·바지43mm. 봉제 마름모 패치와 닫힌 주름 프릴을 추가했다.
- 정상28,168tri/55본/52 Humanoid, 새9메시·프리팹을 같은 GUID로 갱신. 접지 데이터의 동일 위치/가중치 중복만 제거했다. 기존 조작·Controller·절단 소유 부위·4의상·0Clean/1~2Light/3이상·사망Heavy 계약 및 사용자의 슬롯 기본값을 유지한다.
- 300포즈·16단면gap0,4분리 스킨 독립본/2Rigidbodies·1Joint,840단추 접촉 표본 최대0.60mm 확인. 최종 PlayMode 삐에로1/1 회귀 통과. 실제 게임 렌더에서 발견한 얼굴 페인트 역방향을 수정하여 정상/4몸체 각각708개 얼굴 삼각형 중 역방향0을 확인했다. Blender 양면 표시만으로 합격 판단하지 않는다.
- 제작 정본 `bluepierrot-rebuild-v03.blend`, 부품별 `bluepierrot-construction-v03.blend`, 이전 v02 보존. Unity CLEAN/LIGHT/HEAVY 화면과 원화 비교 렌더·보고서는 private Source~에 보관. 동시4인 성능, 온라인2클라이언트 및 최종 미술 승인은 별도 검토이며 이번 수정은 커밋/푸시하지 않는다.

## 2026-10-30 삐에로 v02 얼굴·머리카락 피드백
- 얼굴 앞쪽 깊이가 이마~턱에 걸쳐 거의 같던 링 배치를 수정했다. 볼 앞면은 y=-.136→-.210m, 턱/이마는 완만히 이어지며 눈·코·입·눈썹을 새 표면에 맞췄다. 옆/뒷머리·정수리에 층을 세운 종이 머리카락과 중앙3덩이를 만들고 귀 주변을 드러내며 직선 단발 끝을 보완했다.
- 머리 타일13의 atlas 밝기 재사용이 봉제 무늬 원인. Blender/Unity에 독립 안료를 적용하고 혈흔 상세 마스크에서도 의상 자국이 투영되지 않도록 수정했다. 몸체 정점·55본/52 Humanoid·단추 접합·기존 의상/부상 계약을 보존. 정상9,967tri, 머리카락361tri.
- 정본/FBX/정상+절단9메시를 같은 GUID로 갱신. Blender 사선·정측면·후방 정수리 확인, 실제 Unity 프레임1316→1941 진행 후 사선/측면 캡처. 300포즈·16단면gap0·4분리 본 검사와 삐에로 PlayMode1/1 회귀 통과, 셰이더 메시지0. 이전 전체19/19 결과는 v01 기록으로 구분한다.
- 수정 전 라이브 Blender는 Temp/Task078BluePierrotIntermediate/before-head-volume-v02.blend로 사본 보관. v02 제작 스크립트·최종 이미지/보고서는 private Source~에 기록했다. 확인 후 Play를 끄고 background=false, 깨끗한 BluePierrotGameplay 편집 씬으로 복원했다.

## 2026-10-30 파란 삐에로 플레이어 추가
- 사용자 PR239 병합 후 origin/dev cf8bcab4에서 feat/blue-pierrot-player, private origin/main에서 resource/blue-pierrot-player 시작. doctor1회는 기존 Task077/078/079 중복 외 정상. 이 제작에 대한 새 커밋/푸시 요청은 없어 변경으로 남긴다.
- Blender MCP 5.2.3/addon1.8과 Unity6000.3.25f1/Pipeline 연결 확인. 원화의 걱정스러운 얼굴·붉은 코/뺨·접힌 남색 머리·크림 프릴/리본을 제작하고 토끼와 동일 atlas/봉제·종이 면 음영을 적용했다. 허리와 상하체 연결, 단추/발목 접합을 보존한 9,949tri/55본/52 Humanoid와 정상+절단9메시 구성.
- 리소스와 원본은 private CharacterArt/BluePierrot, 게임 prefab/연습 씬/회귀는 공개 경로. 카탈로그에 파란 삐에로 추가, 기존4의상·0Clean/1~2Light/3이상·사망Heavy 및 분리 조각 스냅샷 계약 유지. 작업 중 관찰한 플레이어2 곰/녹색 기본값 변경은 보존했다.
- Avatar valid/human, 9메시 인덱스/본 배열, 300포즈와16단면gap0, 4분리 스킨 독립본/2Rigidbodies·1Joint 검사 통과. 20,160단추 접촉 표본에서 최대1.65mm. 전체 PlayMode19/19, 최종 허리 보정 후 삐에로1/1 통과. 셰이더 메시지0.
- 실제 PlayMode 프레임 진행과 Idle·1절단LIGHT·3절단HEAVY·복원을 확인하고 캡처를 보관했다. 시험 종료 후 runInBackground=false 복원, BluePierrotGameplay 편집 씬과 Blender 정본을 열어 두었다. 독립 씬 발바닥 높이 .082m 중 .08m는 기존 CharacterController 바닥 간격이며 모델 접지 오차 .002m와 구분한다.
- 초기 Pipeline 임포트5초 timeout 뒤 실제 산출물이 완료됨을 확인했다. CLI beta.15의 공통 초 단위 timeout과 eval 밀리초 인수는 -- 경계로 구분해 해결하고 기존 함정 문서를 보완했다. Editor/패키지 버전은 유지했다.
- 정본 bluepierrot-buttons-fixed.blend와 제작/임포트/검증 스크립트·보고서·4최종 화면은 Source~/README.md에 정리. 생성 중간 blend와 자동 백업은 Temp/Task078BluePierrotIntermediate로 복구 보관. 온라인2클라이언트와 사람의 최종 미술/연속 접점 플레이는 별도 검토 항목이다.

## 2026-10-30 곰 플레이어·단추 밀착·허리 연결
- Blender MCP 5.2.3/addon1.8, Unity Pipeline 연결 확인. doctor는 착수 때1회 실행했으며 기존 Task077/078/079 번호 중복 경고 외 환경 정상. feat/modeling/characters에서 진행.
- bear-turnaround-v01 원화의 둥근 귀/주둥이·녹색 눈 패치·작은 모자, 짧고 넓은 체형을 기존 토끼의 종이 음영/봉제 atlas 제작 방식으로 구성. 독립55본/52 Humanoid, 9,918tri, 정상+4몸체+4사지. `_ThirdParty/CharacterArt/Bear` 리소스와 공개 Bear.prefab을 추가하고 카탈로그에 곰 선택을 등록했다. 기존 4슬롯 기본값은 유지.
- 두 캐릭터의 단추는 고정 깊이 때문에15.6~32mm 떠 있었다. 의상 투영/가중치 보간만으로는 굴곡에서 묻혀, 단추 아래 옷 정점36개를 추가하고 같은 가중치로 변형하도록 했다. 토끼9,994tri. Unity 정상/4절단 × 기본상태14 ×4시점에서 총30,800정점 접촉 표본: 최대 토끼1.40mm/곰1.83mm(단추 두께 포함), 2mm허용 내 통과.
- 실제 Idle 렌더에서 머리 위로 뜨는 팔 봉제선 발견. 양 모델의 오른팔18정점이 LeftLowerArm/REGION_LeftArmCut에 속하던 기존 오류를 수정하고, 절단 소유 부위까지 다시 생성했다. 최종 양팔 분리 메시가 올바른 쪽 정점만 포함하는지 확인.
- 사용자 허리 피드백 반영: 곰 복부 폭 최대18%/깊이12% 보강, 허리 바로 아래 바지 폭15%/코트 부착부20% 축소, 높이에 따라 완만히 연결. 뼈/스킨 가중치 보존, 단추 접촉 좌표와 절단9메시/FBX 재생성. Blender·Unity 최종 화면 확인.
- 실제 Player 이동/달리기/앉기/점프, 4의상, 0Clean/1~2Light/3이상·사망Heavy, 4절단의 물리·분리본·복구/모델 교체를 곰 PlayMode 검사로 추가. 전체18/18 통과 후 허리 최종 수정의 곰1/1 재검사 통과. 각각300포즈 접지·유한성/16단면gap0/4BakePiece 독립본 검사 통과. 원격2클라이언트/최종 미술과 연속 접점 플레이는 미검증.
- 시험 작성 중 색상 float 완전동등 비교와 수동 SetVariant가 실제 presentation의 network outfit에 덮이는 시험 설정 오류를 수정했다. 실제 appearanceOutfit NetworkVariable 값을 바꾸고 프레임을 진행해 검사한다.
- 긴 Unity 임포트의 dispatcher5초timeout은 작업 실패와 구분하고 최종JSON/AssetDatabase 결과를 확인했다. PreviewScene 수동 Camera.Render는 빈 이미지를 생성해 증거로 사용하지 않고 실제 BearGameplay 카메라 캡처로 교체했다. Blender 복사 씬 정리 중 프로세스가 재시작되어 이미 저장된 최종 파일을 다시 열어 수정 속성·본 가중치를 확인하고 일반 main .blend로 저장했다.
- 최종 정본: Bear/Source~/bear-buttons-fixed.blend, Rabbit/Source~/rabbit-buttons-fixed.blend. 이전 미저장 세션은 previous-session-before-bear.blend로 보존. 이번 작업의 중간 렌더/내보내기/자동blend1은 Temp/Task078BearIntermediate로 이동, 생성된 빈 Bear 1 폴더/meta 제거. 곰 제작/이번 피드백에 대한 별도 커밋·푸시는 하지 않았다.
- 사용자가 직접 확인할 BearGameplay.unity와 절단 안내 문서를 추가/보완했다. Flags의 Everything=-1은 현재 고정 순서 모델에서 지원하지 않으므로 네 부위를 순서대로 체크하도록 명시했다. 별도 게임 규칙 수정은 하지 않았다.

과거 제작·바리에이션·리소스 정리 기록: [History/007-before-bear-log.md](History/007-before-bear-log.md).

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 문서 길이 제한으로 중단된 검사는 과거 로그 원문을 History로 이동한 뒤 재실행한다.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-30 분리 저장·제출 요청
- 기존 PR238이 dev에 병합된 것을 확인했다. 기존 작업 기준과 origin/dev bea4c1aa의 트리가 같아 변경을 보존하고 feat/bear-player를 새로 만들었다.
- private main 624a1a01에서 토끼 교정 8a9e80bd와 곰 추가 b91eef97을 각각 커밋·푸시했다. 리소스 PR: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/14 (resource/third-party/bear-player → main).
- 상위 토끼 커밋 82965d44에는 Rabbit.prefab과 토끼 리소스 포인터만 포함한다. 곰 프리팹·카탈로그·시험 씬·회귀 검사·제작 기록과 곰 리소스 포인터는 별도 곰 커밋에 포함한다.
- 사용자가 비공개 PR을 병합하는 기존 방침을 유지한다. 상위 pre-push가 private main 포함을 요구하므로 PR14 병합 확인 후 상위 작업 브랜치를 푸시하고 dev PR 1개로 제출한다. 검사 결과는 위의 최종 check-work 및 별도 PlayMode 결과를 재사용한다.
- 제출 스크립트의 단일 슬래시 브랜치 규칙에 맞춰 feat/bear-player로 이름을 정리했고 PR 미리보기가 통과했다. 곰 상위 커밋은 67c4ede8.
- PR14 최초 CI는 Unity 비임포트 Source~ 보관 파일에 meta를 요구해 실패했다. private 94c98cb9에서 검사기의 tilde 경로 제외를 바로잡고 일반 게임 에셋 meta 누락은 계속 검출하는 회귀 2개를 연결했다. 로컬 회귀 2/2 및 실제 전체 리소스 규칙 검사 통과; 같은 PR에 푸시했고 CI update도 PASS(실행40955254259).
- 사용자가 PR14를 Merge commit 8135a714로 병합했다. 최종 리소스94c98cb9가 origin/main에 포함됨을 확인했고 상위 pre-push ThirdParty OK 후 feat/bear-player를 푸시했다. 토끼82965d44/곰67c4ede8/검사기 포인터428476cb로 구분하며, 아래 dev PR에 함께 제출했다. 검토·병합은 사람에게 남긴다.

- 2026-10-30 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/239 (feat/bear-player → dev)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
