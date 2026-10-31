# 대기실 담당자 전달용 변경 목록

2026-10-31 · 담당 @RagSpirin · `feat/exit-route/design-map` · 최신 반영 `dev7164c818` · 로컬 저장, commit/push 없음.

## 이번 메모리·진입 지연 보수

| 변경 | 적용 위치 | 영향 |
|---|---|---|
| 캐릭터4종 Paper 셰이더의 중첩 loop 전개 제거, 반복 불변 noise 계산 분리 | `_ThirdParty/CharacterArt/{Bear,BluePierrot,PinkGirl,Rabbit}`의4개 shader | 혈흔/의상 샘플 수와 재질을 유지하며 컴파일 메모리 부하 감소 |
| W/A 재질이 참조하는 큰3D 텍스처177개의 mip streaming 활성화 | `_ThirdParty/CharacterArt`, `Synty`, `LeartesStudios`의 해당 png/tga meta | GUID·원본 최대 해상도·압축·색 공간 유지, UI/정적 한글 폰트 제외. 일부 옛 meta는 Unity6 형식으로 자동 갱신 |
| PC 텍스처 스트리밍384MB, 최대4레벨 축소 | `ProjectSettings/QualitySettings.asset` | 가까운 텍스처에 우선 메모리 배정. PC global mip limit은0 유지. 모바일 품질 그대로 |
| 출발 전 대기실의 보이지 않는 렌더·조명·파티클 휴지 | `EscapeWaitingPresentation`, `EscapeEntrance`, `EscapeCampaignContent` | 대기실 게임 로직·충돌·플레이어·연결 계단 유지. 렌더 목록은 로드 때 준비하고1024개/프레임으로 처리. 밖으로 운반한 기물은 숨기지 않음. 종료/복귀 때 원상 복구 |
| 위 처리의 실제 배경 루트 연결 | `WaitingRoomBase.unity`, `EscapeWaitingRoomLinkBuilder` | `WaitingRoom_LayoutA`/기존 Greybox 참조. 별도 `BasementEscapeConnection`은 유지 |
| 대기실/A Play 동안 idle 에셋 임포트 작업자 휴지/종료 복구 | `EscapeMppmReviewConfig` | 에디터 전용. 자동4인 입력/품질 변경은 여전히 명시적 검토 opt-in에서만 실행 |
| A 배경 활성화 분산 | `EscapeSceneryActivation`, `EscapeCampaignContent` | 사전 로드 때 휴지 준비. 출발 뒤 기존 배경 모듈을1개/프레임으로 켠 다음 순수 정적 geometry를256오브젝트/프레임으로 활성화. 제어 스크립트가 있는 기믹 묶음 내부는 쪼개지 않는다. 공용 묘기 시스템·입구 바닥은 즉시 활성화하며 중단/재진입 시 원래 상태 복구 |
| 성능 구간 표시 및 회귀 | `EscapeCampaignContent`, `EscapeWaitingPresentation`, `WaitingPresentationTests`/`SceneryActivationTests` | Profiler의 `Escape.ActivateContent`/`Escape.RetireWaitingGraphics`, 화면/충돌/복귀/운반/분산 활성화 회귀8건 |

**셰이더와 텍스처 meta는 비공개 `_ThirdParty` 저장소 변경이다. 공개 프로젝트 변경과 함께 전달해야 한다.** 원본 모델·텍스처 이미지를 교체하거나 대기실 에셋을 삭제하지 않았다.

- 접속 시 내부 NGO 승인 제한10초를 공용 Join 제한30초 이상으로 맞췄다 (`NetworkSessionManager`, 기존 `StartConnectionTests`). 무거운 첫 씬/셰이더 로드가 공용 접속 제한보다 먼저 연결을 끊는 상황을 보완한다. 더 길게 지정한 기존 값은 유지한다.

## 앞서 연결 작업에서 바뀐 대기실 접점

- 기존 지하 열쇠 문 뒤 개구부와 별도 합류실·두 계단·밀폐 통로를 W에 추가했다. A는 저장 위치를 옮겨 대기실 전체 외곽과9m 간격을 확보했다. 기존 열쇠2개/패드/두 사람 문 조작은 유지한다.
- `EscapeAssemblyState`/`EscapeEntrance`: 첫 진입300초, 부분 이탈 유지, 인원0 초기화, 실제 생존4인과 A 준비 완료 시 출발. 대기실 기존 시계는 합류 중에도 계속 진행한다.
- `SpaceLinkDirector.Escape`: 대기 중 A를 실제 NGO Additive 사전 로드하고 전원 로드 완료를 확인한다. UI의 로컬 Additive 작업과 직렬화한다. 같은 플레이어/소지품/네트워크 세션으로 걸어서 진입한다.
- `EscapeCampaignContent`: A 개발용 플레이어·별도 카메라·낙사 보호를 캠페인에서 제외한다. 계단 끝 이전3인칭/끝 이후 개인별1인칭은 `EscapeEntryFirstPerson`과 카메라 우선순위로 연결했다.
- `CommonMenuUI`/`CommonInventoryUI`/공용 UI 로더/음성 씬 설정: 실제 A를 지원 공간에 포함하고 대기실 전용 HUD를 Escape에서 숨긴다.
- `PropCarry`/`ItemCarryBook`: 리타이어한 참가자의 소지품을 호스트가 정리한다. 최종 성공은 최초4인의 도착/리타이어/이탈 집계가 끝난 뒤 도착자1명 이상인 기존 결정 그대로다.

## 검증 범위

- 별도 빌드 없이 공식 Unity MPPM 메인+보조 에디터3개를 사용했다. 네 클라이언트의 기존 입력/물리/네트워크로 W 스폰→지하 문→합류→계단→A 입구까지 보행했다. 체크포인트·위치 강제 이동 없음.
- 이번 집중 검증에서는 선행 열쇠/계단 문/암호 퍼즐 조건을 준비했다. 탈출 문은 두 플레이어의 실제 F 입력으로 열었다. 열쇠 수집부터의 퍼즐 완주나 A 전체 탈출 완주 검증은 아니다.
- 기본 PC 품질/global mip0, 검토용40FPS 상한으로 비교했다. 5·6차에는 네 실제 참가ID0/1/2/3이 최초 접속에 성공했다. 이전 접속 수동 정리나 낮춘 mip 프리뷰를 사용하지 않았다. 모든 클라이언트의 캐릭터4종 셰이더 오류0을 확인했다. 4차 이후에는 셰이더 오류 메시지/캐시 삭제도 하지 않았다.
- 텍스처 메모리(`Texture.currentTextureMemory`)는 변경 전814~828MB에서 5·6차376~381MB로 약54% 감소했다. 전체 RAM이54% 줄었다는 뜻은 아니다. 에디터4개의 메모리 부담은16GB PC에서 여전히 크다.
- 6차에는 검사 명령 파일 공유 경합과 고정900ms 스냅샷의 복제 지연 때문에 검사 스크립트를 보완해 같은 참가자를 그대로 이어서 보행했다. 게임 상태/위치는 조작하지 않았다. 합류 규칙과 개인별 시점, 전원 정상 상태 A 입구 도착을 확인했다.
- 6차 대기실 렌더 정리는 프레임당11~28ms(5차 일괄160~322ms), 정적 배경 배치1.3~5.3ms/전체2.7~3.5초로 A 입구 도착 전에 완료됐다. 그러나 다른 활성화 작업과 시스템 메모리 압박에 의한 긴 프레임은 남아 있어 무지연 완료로 판단하지 않았다. 최종 반복 결과는 아래에 기록한다.
- DX12의64MB 텍스처 업로드/16MB native 버퍼 경고, 기존 추가 조명 그림자 해상도 경고와 일부 A 라벨 한글 글리프 경고는 별도다. CPU 텍스처 on-demand 옵션은 렌더 패스 오류를 일으켜 원래false로 복구했으며 최종 적용 항목이 아니다. 기존 음성403·실제 다중 PC/Relay·전체 A 탈출 결과는 이번 진입 검증에서 해결/통과로 쓰지 않는다.

## 최종9차 실제 MPPM 결과

- 새 에디터에서 메인39632·보조43940/22268/5844의 실제4개 프로세스, 참가ID0/1/2/3 모두 최초 접속 성공. 강제 위치 이동 없이 W 스폰부터 A 입구까지 연속 보행했다.
- 첫 합류/부분 이탈 때 마감482.251632283628 유지, 인원0에서 초기화, 재진입 마감500.3812421365827.3명일 때 대기하고4명일 때 출발했다. 계단 끝을 지난 사람만1인칭이며 전원 A 입구에서 Normal/1인칭/실제4명 유지.
- 모든 클라이언트 캐릭터4종 셰이더 supported=true/error=false. 새 NullReference/MissingReference/IndexOutOfRange/InvalidOperation/셰이더 OOM/업로드 메모리 해제 예외 검색0건. 기존 경고는 위 제한대로 구분한다.
- 출발 코드 활성화149~228ms (동일하게 새 에디터로 비교한8차331~485ms), W 그래픽 정리5~14ms/프레임, 배경 모듈 최대34~99ms/배치. 배경 전체 준비는4.30~5.56초이며 모두 A 입구 도착 전에 완료됐다. 네 화면 모두 W14423개 렌더가 휴지됐다.
- 텍스처370~381MB, 엔진 allocated1852~1954MB. **전체 프레임 간격 최대0.47~1.11초는 남았다. 분홍색 재발/진입 불능은 관측되지 않았지만 무지연 해결로 보고하지 않는다.**
- 자동 회귀: 탈출143/143 뒤 마지막 변경분인 배경 모듈2/2를 재컴파일 후 다시 확인했다. 실제 Play 전환을 포함한 접속 회귀는5/5(`connection-results.xml`):30초 만료/버튼 복구, 이전 실패 격리, 성공·대기 잠금, 사용자 취소, 네트워크 종료 사유. 도구 연결이 domain reload로 끊긴 첫 호출은 통과로 세지 않고 Unity 공식 결과 콜백/XML로 재검증했다.
- 검토 후 보조 에디터3개 종료, Default Play/DevMode=true/임포트 작업자3/mip0/FPS-1/vSync0 복구. 검토 opt-in과 임시 자동 입력·결과 수집 소스는 제거하고 대기실 씬을 열어 두었다. 실제 제품 수정인 스트리밍 설정은 유지한다.

증거: `.harness/local/escape-entry-performance-20261031/run-{1,2,3,4,5,6,7,8,9}/`의 입력 경로·스냅샷·프레임·셰이더 감사. 최종은 `run-9/result.txt`, `check-01~12`, `entry-audit-*`, `error-audit.json`. 변경 전 사본은 같은 폴더 `before/`, `before-streaming/`에 보존했다.
