# v6 — 4인 진입 수정·한 구역 관객 대비

## 범위
- 기존 팝업/저음 사람 원음/개별 음량 설정 유지. 군중 72명·메시/재질/Renderer 각 1개·864삼각형 유지.
- 환호는 높은 V형 팔과 따뜻한 금빛으로 약 0.18초 구역 파동, 야유는 낮게 앞으로 밀며 거의 동시에 숙이는 탁한 적색. 왕복 떨림을 없애고 색과 실루엣을 함께 구분한다.
- 실제 머티리얼의 색만 변하며 추가 조명·그림자·네트워크 메시지·게임 벌점은 없다. 1.9초 동작 간격과 120ms 공유 사건 묶음은 유지한다.

## 실제 화면
- [외줄 약8초](Previews/Runtime/tightrope-crowd-motion-v6.mp4), [대포 약8초](Previews/Runtime/cannon-crowd-motion-v6.mp4): 정지 → 환호 → 야유 순서. 1920×1080 일반 카메라의 실제 프레임이며 AI 생성/확대 카메라가 아니다. 소리는 별도 녹음하지 않았다.
- 비교 구도를 유지하려고 로컬 이동을 일시 정지했다. 프레임 간 실제 시간을 보존해 영상을 인코딩했다. 캡처 중 성능은 비용 측정에 사용하지 않는다.
- 시각 검사에서는 이전보다 금빛 V/낮은 적색 형태를 구분하기 쉽다. 관객은 여전히 상단의 작은 구역이므로 본인/관전 대상 팝업과 사운드가 주 피드백이다. 실제 조작 중 사람의 판독률을 자동 검사 결과로 주장하지 않는다.

## 4인 진입 조사
- 기존 진단 경로는 Start에서 LAN 접속 후 호스트만 DevStartLauncher로 공연에 들어가 로비의 SpaceLinkHub 초기화를 생략했다.
- SpaceLinkDirector의 sceneLoaded 구독이 CampaignPerformanceEntry.Prepare를 호출한다. 이 관리자가 없는 클라이언트는 출발점 배치를 하지 않는다. 호스트 CampaignTightropeAdapter는 모든 연결자의 출발점 도착 전 보상/공연을 열지 않는다.
- 진단의 모든 peer에 일반 로비와 같은 CampaignRun/SpaceLinkDirector 초기화를 추가했다. 게임 본체의 배치 판정·대기 조건을 완화하지 않는다.
- 동일 빌드의 `-audience-legacy-entry` 부정 대조군과 수정 경로를 비교한다. 준비 로그에서 씬/UI/보상/코스/로컬 캐릭터/입장 관리자/배치 여부를 각각 기록한다.
- **원인 확정:** 부정 대조군의 클라이언트 3개 모두 씬/UI/코스/Local=true, director/entry/placed/rewards=false로 timeout 재현. 수정 경로는 호스트+3클라이언트 모두 공연 진입, 각4구간 총16구간 valid=true. 이전의 “씬 로딩 실패” 추정은 폐기한다.
- 증거: [이전 초기화 로그](Previews/Runtime/entry-legacy-v6-client1.txt), [수정 진입 로그](Previews/Runtime/entry-fixed-v6-client1.txt). 진입 약2.50–3.33초는 공연 시작 요청 이후이며 관객만의 추가 로딩 시간이 아니다.

## 실측
- 별도 Windows Development 실행 파일. 같은 PC에서 호스트+3클라이언트, 각 1280×720. 로컬 이동을 멈춰 같은 코스/카메라를 비교하며 기존 장애물은 작동한다. 8초 예열 후 off/on/off/on 각4초, 첫0.5초는 제외한다. 전체 조작 플레이의 절대 FPS 보장이 아니다.
- 같은 PC 4개 프로세스는 CPU/GPU를 공유하므로 각자 다른 PC의 절대 프레임 성능을 대신하지 않는다. 콜드 디스크 로딩과 인터넷 지연 보장도 포함하지 않는다.
- Ryzen 7 9700X / RTX5070Ti / RAM31GB. 1인4구간 및 4인2회×4화면×4구간=32구간 **모두 valid**. 4인 재실행도 네 명의 공연 진입과 공유 관객 반응/각자의 개인 반응 재생을 확인했다. 종료 시 테스트 프로세스 모두 정상 종료.

| 항목 | 결과 | 해석 |
|---|---|---|
| 1인 CPU p95 off → on | 5.79/5.60 → 5.57/5.62ms | 뚜렷한 악화 없음; 개선 효과라는 뜻은 아님 |
| 1인 GPU p95 off → on | 1.47/1.39 → 1.42/1.40ms | 같은 구간 탐색 비교 |
| 4인 CPU p95 쌍별 on-off | -4.175~+0.682ms, 중앙값 -0.175ms | 같은 PC 자원 공유·한 구간 일시 지연 포함 |
| 4인 GPU p95 쌍별 on-off | -0.361~+0.406ms, 중앙값 +0.070ms | 0.3ms 상한의 확정 PASS로 처리하지 않음 |
| 준비된 구역 생성 | 0.045~0.107ms | 디스크 콜드 로딩 시간 아님 |
| 메시/렌더러/삼각형 | 163,520B / 1개 / 864개 | v5와 동일 |
| 반응 처리 Observe 130회 | 모든 실행 0B, 총0.203~0.355ms(4인) | 준비한 스냅샷 이후 함수만 측정. 전체 게임 GC=0 아님 |
| 마지막 관객 반응 지연 | 120~127ms | 의도한120ms 사건 묶음 포함, 전체 지연 분포 아님 |
| 추가 관객 네트워크 | RPC/NetworkObject 추가 없음 | 기존 호응 상태 사용. 전체 transport 수신량은 움직임·시간에 따라 변동 |

- [전체 수치 요약](Previews/Runtime/benchmark-v6-summary.json), [1인 원본](Previews/Runtime/benchmark-single-v6.json), [4인 호스트](Previews/Runtime/benchmark-four-v6-host.json), [4인 반복 호스트](Previews/Runtime/benchmark-four-v6-repeat-host.json). 나머지 client1~3도 같은 폴더에 보존했다. Render Thread 카운터는 미지원(null).
- **한 구역 시험 활성화:** 공통 GameSettings의 CrowdTrial=true. 이번 요청 범위인 한 구역만 켰고 확장은 하지 않았다. 짧은 로컬 측정의 판정 한계를 보존하며 다른 장비/장시간/인터넷 조건은 별도 확인 대상이다.

## 검사
- 진단 빌드 오류0/경고32, 100.700초. 측정 define/Frame Timing 값은 원래 값으로 복구했다. 일반 빌드에는 측정 클래스가 들어가지 않는다.
- 실제 외줄/대포 화면 확인과 4인 수정 전후 재현 완료. 한 구역 기본 활성화 후 `Audience` PlayMode **6/6 PASS(47.87초)**: 두 공연 배경/일회성 소리, 음량 저장/취소/초기화, 외줄 호응 관련 회귀. [검사 결과](Previews/Runtime/tests-audience-v6.json). 묶음 검사 결과는 log에 기록한다.
