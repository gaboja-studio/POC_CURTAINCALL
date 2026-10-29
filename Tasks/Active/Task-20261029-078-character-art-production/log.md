# Log

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
