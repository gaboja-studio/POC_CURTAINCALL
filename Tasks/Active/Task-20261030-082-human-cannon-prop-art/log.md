# Log

## 2026-10-30 — v01 제작·적용

- 사용자 요청: 컨셉 2장·기존 캐릭터·대기실·공연 배경을 고려한 정적 소품 제작 및 실제 더미 교체. Task 담당 @MoHoDu/전용 기존 브랜치 연결. commit/push 요청 없음.
- doctor 1회 FAIL: 기존 Task-077/078/079 ID 중복, 해당 브랜치 Task 미등록, private 저장소·Unity 설치 경로 접근 제한. Task 미등록은 이번 생성으로 해결. 샌드박스 밖에서 private clean·Unity 6000.3.25f1 ready를 확인하여 접근 오판을 분리했다. CLI 업데이트 조회는 네트워크 ENOTFOUND로 불가, 설치된 beta.15 사용.
- Blender MCP 5.2.3 연결. 기존 PinkGirl 파일을 덮어쓰지 않고 독립 소품 씬 제작. 이전 전체 세션은 로컬 무시 경로 `.harness/local/art-recovery/character-session-before-props.blend`에 복사 보존. 현재 Blender는 새 construction.blend를 연 상태.
- 제작: 버건디/아이보리/황동/남색, 패드·봉제·스프링·중앙 별, 패딩 포구·줄무늬 통·스포크 바퀴·별 차대·계단. 대포 16,350tri/1 renderer, 트램펄린 23,526tri/3 visible renderers, 1024×512 atlas 2장/URP Lit 1재질. 별도 리그·클립·Collider 없음.
- 경로: `Assets/Resources/Models/Objects/HumanCannonProps/`의 Models/Textures/Materials/Prefabs/Source~. 참조 이미지는 References/, 시각 증거는 Previews/. 재현법 Source~/readme.md.
- 실제 변경: HumanCannon.unity 대포10·트램펄린1, WaitingRoomBase.unity 활성 대포2·트램펄린3. 기존 Renderer만 숨기고 새 시각 prefab 추가. 기존 source prefab/앵커/Collider/게임코드 보존. Task-075/079와 대기실 씬이 겹치므로 병합 시 이 시각 교체 부분을 확인한다.
- 기존 컴포넌트·Transform 38,835개 비교 PASS, 기능/충돌/기존 Transform 값 변화0. `Source/contract-verification.json`. 큰 임시 원문은 Temp/cc-prop-contract-before.json에 둔다.
- UV 한글 이름 join 충돌, Blender RGB 저장의 metallic/smoothness 알파 유실을 수정. 실제 Unity/Blender 이미지 재확인. Harness Engine/Unity/Pitfalls/Blender에 기록.
- 첫 Editor 검사14개 중 옛 숨겨진 프레임 조회1 실패 → 새 실물 조회로 갱신. 두 번째 검사에서 별 펜던트 반경2mm 초과를 발견해 모델 수정. UV/알파 회귀 검사 추가 후 15/15 통과.
- 첫 PlayMode2개 실패: 새 손잡이가 접촉 시작 높이를 .018m 올림, 기존 테스트는 현재 whole-device 접촉과 달리 코너 비반동을 가정. Git 원본 미리보기에서 ContactBounds·cornerTouch를 비교하여 원인 분리. 손잡이 높이·폭을 원본 AABB와 정확히 맞추고 현재 동작 기준으로 테스트 갱신. 게임 동작 코드 미수정. 후속 실제 LAN호스트 중앙/코너 반동2/2 통과.
- 접촉 경계는 Surface 상대 min=(-2.875,-.5,-2.4753408), max=(2.4753408,.15,2.4753408). 원형 천 표면 규격과 별개로 실제 전체 접촉 경계도 보존. Project/Pitfalls/Human-cannon 기록.
- 배경 로드·38,835개 비교 eval은 5초 응답 제한에 걸렸으나 작업 완료와 결과 파일을 별도로 확인. 실패 응답을 성공으로 취급하지 않고 실제 완료 증거를 사용했다.
- 원래 PinkGirlGameplay 씬은 렌더 시 URP LightData2개가 자동 추가됐다. 해당 자동 추가만 제거 후 디스크 원본과 byte-identical 확인, 저장 없이 정리. 해당 씬 파일 변경 없음.
- 남은 것: 최종 아트 감상·4인 온라인·최종 플레이 성능은 사람 확인. 검사 결과는 아래에 추가.
- Source~를 임포트 대상으로 오인한 `.meta` 검사 오류는 Scripts/Lib/common.ps1에서 수정. Source~ 이전의 일반 상위 폴더 검사는 유지, 정상 모델 경로/무시 경로 계약 확인 PASS. 새 Assets/Art는 프로젝트 허용 경로가 아니어서 Unity MoveAsset으로 `Assets/Resources/Models/Objects/HumanCannonProps`로 GUID 보존 이동. 코드·원본 이미지 링크·문서 경로를 함께 갱신했다.
- 최종 Editor15/15 PASS(`Source/editor-tests.json`), 실제 LAN호스트 반동2/2 PASS(`Source/playmode-tests.json`), 10대 대포 각각 탑승·취소1/1 PASS(`Source/cannon-playmode-tests.json`). 이들은 4인 온라인 검사가 아니다.
- 최종 check-work: 안전성 PASS, 문서/규칙 PASS, 컴파일 PASS(up_to_date). 통합 테스트 재실행은 선택하지 않았고 위 별도 집중 검사 결과를 사용한다. 기존 문서 크기 경고33개/hard0은 수정 범위 밖. 최종 커밋·푸시 없음.

- 2026-10-30 검사: 안전성=FAIL; 문서/규칙=SKIPPED(선행 실패); 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-10-30 — 저장·dev 제출 요청
- 사용자 명시 요청에 따라 관련 변경을 저장·푸시하고 dev PR을 제출한다. gh 로그인/커밋 작성자 @MoHoDu 일치, private 에셋 저장소 clean. 최신 origin/dev와 작업 시작 HEAD는 b19189eb로 일치. 관련 없는 변경 없음.
- Editor15·PlayMode3 통과 증거를 재사용하며, 최종 미술·4인 온라인/성능 확인은 PR의 남은 항목으로 공개한다. 병합은 요청되지 않았다.
- 저장·푸시 완료: `76e43aae` (`feat: 인간 대포와 트램펄린 Blender 모델 적용`). 저장 시 check-work 안전성·문서/규칙·컴파일 PASS, 집중 테스트 기존 증거 사용. LFS 업로드 완료.
- dev 제출: [PR #241](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/241). 검토 대기이며 병합하지 않았다. new-pr.ps1의 중첩 브랜치 이름 검증 제한으로 공통 PR 템플릿을 생성해 gh로 제출했다. 리뷰어 계정은 임의 지정하지 않았다.
