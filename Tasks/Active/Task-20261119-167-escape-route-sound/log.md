# Log

- 2026-11-19 사용자 청취 피드백. 반복되는 쿵쾅 소리의 대상은 배경음이며 추격자 발소리는 유지하라는 지시를 받았다. ESC-028 공장 배경음의 전체 화면 루프와 카탈로그 항목을 제거했다. 대체 클립을 지정하지 않았으므로 시트의 파일명 변경 조건은 발생하지 않았다.
- 외줄 전기음은 플레이어 머리 위치에서 외줄까지 8m 이내이면서 고체 차폐가 없을 때만 재생한다. 사라진 외줄은 BoxCollider의 원래 로컬 크기로 위치를 계산하며 현재 재생 지점까지의 거리·차폐도 매 프레임 확인한다. 재시작·분리·비활성화 시 재생을 종료한다.
- 저장·제출 요청에 따라 실제 참조하는 23개 클립과 메타만 선별한다. Unity 자동 임포트가 바꾼 기존 private 7개 파일은 unity-import-metadata-before-sound-submit-20261119 stash에 보존했다. private origin/main의 README 3개 후속 커밋을 fast-forward로 반영했다.
- 새 회귀 테스트가 Assembly-CSharp의 Sound 네임스페이스를 직접 참조해 CS0234로 Safe Mode 진입을 유발했다. 기존 EscapeRoute.EditTests 구성과 같은 reflection 참조로 수정했고, Unity가 Safe Mode에서 정상 Start 씬으로 복귀한 것을 확인했다.
- 청취 수정 후 EscapeRopeSoundTests 6개, SoundCueTests 9개, OriginalMechanicsTests 7개 PASS. 고체 벽 차폐·트리거 통과·8m 거리 제한·사라진 지지대의 원래 위치·청취자 자기 충돌체 제외·가려진 지지대 대신 보이는 지지대 선택과 배경음 삭제·발소리 원래 설정을 확인했다.
- private main에 edc4b497(탈출로 지정 효과음 23개 도입)을 저장·push했다. WAV 23개와 파일·폴더 메타를 합해 59개 파일, LFS 14MB만 포함했다. 전체 라이브러리의 나머지 파일은 로컬에 유지한다.

- 2026-11-19 요청. 다운로드의 Pro Sound Collection.unitypackage(2,121,358,051 bytes) 확인. 패키지의 8,079 WAV 중 지정 24개가 각각 하나씩 존재한다. 원본 GUID를 보존하며 Assets/_ThirdParty 경로로 변환한 로컬 임포트 패키지를 준비했다.
- Unity의 다른 프로젝트 열기 대화상자를 취소한 뒤 열린 escape_route 씬 isDirty=false와 PlayMode=stopped 확인 후 정상 종료했다. 공개·private의 기존 변경을 각각 before-escape-route-sound-20261119 stash에 보존했다.
- origin/dev fetch 후 feat/escape-route-sound 생성. 이전 doctor는 private dirty·기존 Task ID 중복으로 FAIL이었다. 같은 작업의 doctor 재실행은 생략한다.

- 기존 변경 보관 SHA. 공개 stash 3eca8ff69b0f20812bff7457d5d2c97ec1b3d0d4, private stash 26aaf41ef4a62eede6156689ff6e9c0fb37186c0. 원래 브랜치는 feat/escape-route-asset-replacement와 resource/escape-route-machine-meshes이다.
- 최신 dev의 SoundCatalog·SoundManager를 재사용했다. 위치 재생용 PlayAt과 voice별 Transform·대상 종료 처리를 추가했다. 기존 효과음 음량 적용과 UI 재생 경로를 사용한다. 18개 ID를 기존 표시 시점에 연결하고 보류 3개는 초기 enabled=false로 준비한다.
- ImportPackage가 파일을 생성하지 않아 경로 정리 패키지의 소규모 임포트까지 확인했다. 같은 결과여서 원본의 8,079 WAV·4 PDF·89 폴더 메타를 임시 폴더로 추출한 뒤 Unity 내부 스크립트로 private 경로에 복사하고 Refresh를 실행했다. 현재 전체 오디오 임포트 진행 중이다. 8,172개 파일·폴더의 누락과 GUID 불일치는 없었다.
- Pipeline eval의 기본 main-thread 응답 제한 5초 때문에 긴 파일 복사·임포트 명령은 시간 초과를 반환했지만 실제 작업은 계속됐다. 로컬 진행 파일과 실제 파일 수를 확인해 중복 실행을 피했다.
- 사용자 추가 지시를 반영했다. 로컬에서는 전체 라이브러리를 유지하고 제출할 때만 실제 사용 클립과 메타를 선별한다. 파일 정리나 삭제는 실행하지 않았다.
- 전체 오디오 임포트 완료. 카탈로그 연결 확인 결과 18개 항목·24개 클립·누락 0개다. ESC-015·016·022의 초기 재생은 보류 상태를 유지했다. 발소리·포효·착지·투척의 초기 구간과 음량은 원본 길이와 파형을 기준으로 조정했으며 실제 청취 밸런스는 사람 확인 대상이다.
- check-work 결과 안전성·문서/규칙·컴파일 PASS. 선택 EditMode 검사는 SoundCueTests 9, CooperativeUpgradeTests 40, OriginalMechanicsTests 7, PursuerHeavyGaitTests 11, PursuitFireTests 15로 총 82개 PASS다.
- UiSoundTest에서 위치 추적·독립 3D voice·사용자 효과음 음량·소유 대상 비활성화 종료·UI voice 재사용·접이교 구동/완료 단발·루프 종료·보류 3개 비활성 상태 등 런타임 19개 검사 PASS. 실제 두 클라이언트 청취와 플레이 밸런스는 사람 확인 대상이다.
- 검증 후 escape_route 씬 isDirty=false를 확인하고 Unity를 정상 종료했다. origin/dev를 다시 fetch해 HEAD와 차이 0/0을 확인했다. 사용자 테스트 요청에 따라 Start 씬을 다시 열었고 단독 활성·isDirty=false를 확인해 Unity를 전면에 띄웠다. Unity 임포트가 Synty/PolygonGeneric/Models/Base/SM_Bld_Base_Stairs_01.fbx.meta를 자동 갱신한 상태이므로 제출 선별 대상에서 분리해 확인한다.

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
