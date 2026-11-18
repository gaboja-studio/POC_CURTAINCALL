# Log

- 2026-11-18: doctor 1회 실행. 초기 sandbox의 서브모듈 Git 메타데이터 접근 제한은 승격한 상태 조회로 해소. Unity 종료 상태에서 origin/dev fetch, HEAD=origin/dev=14ca2f3b 확인. 작업 브랜치 fix/main-stage/background, private HEAD 119cb7d2, 기존 변경 없음.
- 서브모듈 검색 가능 파일 18,647개(.meta 제외), 프리팹 4,303개, FBX 4,975개를 .harness/local/task159/asset-inventory.csv로 조사. HorrorCarnival 488, StylizedFactory 100, Conveyor Works 206개 프리팹 등 후보 확인. 개별 후보 8개 AssetDatabase 로드 성공; 전체 호환 검증은 아님.
- Unity 6000.3.25f1 / URP PC_RPAsset. 공용 원본 광원100개(활성72), runtime Scale=1/3, 광원 세기=1/9. 기존 결승 추적등은 플레이어 옆을 이동함. 외줄/대포 실제 카메라 기준 PNG 저장.
- 개발 런처의 UDP7777 바인딩 실패(콘솔 오류3개)는 미리보기 HostLanAsync(17779)로 해소. 프레임 진행과 두 공연 로드 성공 확인. 설정 에셋 변경 없이 Play 종료, Start 씬 clean, private clean.
- 원화 A 생성: .harness/local/task159/concept-a.png. 실제 적용 결과가 아닌 사전 원화. 사용자가 방향 승인하고 스폿 번짐 감소·한글 툴팁 설정·약한 객석 조명을 추가 요청.
- Unity 에디터 API로 33_DeathCircusRefit 저장. 고정 천장 추적등12, 코스 천장등6, 객석 wash36·전구108, 기계/철창 섬6, 대형 천막2·입구·깃발 배치. 장식 콜라이더159 비활성 확인. Dzendor 및 Conveyor 전용 파생 URP 재질을 사용하며 원본은 보존.
- GameSettings.PerformanceLighting 연결. 전 필드 한국어 Tooltip; 배경/표면/발광/객석/안개/스폿 반경·색·추적·인계/빛기둥/결승 배율 노출. 모든 공연 동안 고정 천장 광원에서 생존자 추적. 넓은 기존 빛기둥 농도는 .12배. 안전지대 초록 바닥은 기존 의도이므로 보존.
- 컴파일 오류0, 기존 경고52. 실제 외줄14초·인간대포12초 녹화(.harness/local/task159/*-final.mp4). 외줄0.7m 일반 이동 입력, 장착부 위치 변화0m, 최대 조준 오차 .143도. 영상 후반 카메라는 기존 게임 자유 시점 API로 객석을 보여주며 실제 렌더링을 녹화.
- live-validation.txt: 12등/장식 충돌체/한국어 Tooltip/반경·객석 밝기 라이브 변경/조명 disable-enable/파생 재질 복원 검사 PASS. 두 공연 로드와 대기실 전환 확인. 다중 클라이언트·최대 인원·대상 PC 성능/빌드는 미검증.
- 환경/별도 기능 로그: 최초 UDP7777 바인딩 실패3개, 자동 대기실 튜토리얼의 RuntimeNavMeshBuilder Source mesh SM_Bld_Base_Stairs_01 read access 오류. 이 작업의 조명 코드 스택은 아님. 오류를 삭제하지 않음. 미리보기는17779 사용. Play 중 생긴 기존 재질8개의 _Color float 반올림만 SerializedObject로 원래 값 복구.
- 지식 갱신 및 verify-knowledge PASS. HTML 보고서와 최종 check-work/제출 전 origin/dev 확인 진행. 최종 미감 사용자 피드백 대기. commit/push 하지 않음.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 후속 피드백: 공연 영역이 어두워 간접 보조등 요청. Unity 종료 뒤 origin/dev 5커밋을 fast-forward(0b8f5e8f)로 반영했고 작업 변경은 보존했다.
- Performance Indirect Gallery: 실물 측면 조명22개, 공연 전용 Rendering Layer7(bit128), 실게임 범위30m·밝기배율 .55·110도·그림자/빔 없음. 객석과 배경 수광 대상0개, 코스 renderer 외줄601/대포2665개 포함을 실게임 검사했다. GameSettings에 켜기/밝기/거리/색/각도/결승 유지율과 한국어 설명 추가.
- 동일 카메라 보조등 OFF/ON PNG와 수정 영상 tightrope-fill.mp4/cannon-fill.mp4 생성, HTML 상단에 피드백 비교 추가. 수정 영상 외줄0.6m 이동·고정 장착부0m 변화·최대 추적오차 .124도. disable/enable 시 재질 및 rendering bit 복원 검사 PASS. 장식 콜라이더 총181개 비활성. 수정 묶음 check-work 재실행 예정.

- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 수정 보고서 브라우저 확인: 가로 넘침 없음, 수정 영상 readyState4(외줄13.93초/대포10.5초), 로컬 미디어 참조9개 누락0. 보고서 .harness/local/task159/report.html.
- 최종 종료 후 동기화에서 origin/dev 신규 변경을 발견해 e5f772c3으로 fast-forward, private 참조22e55ec1도 반영. 배경 씬·조명 변경과 겹침 없음. 새 코드 반영으로 컴파일 재확인.
- 자동 승인 검토가 Play 중 변경된 TMP 폰트 캐시4개를 HEAD로 복원하는 동작을 discard 승인 조건으로 거절했다. 해당 파일은 그대로 보존하고 사용자 승인 대기. 대상: Fonts/CheerGauge/Pretendard-Bold SDF.asset, Fonts/CommonButtons/Pretendard-SemiBold SDF.asset, Fonts/Graffiti/Graffiti_Brush.asset, Sounds/EscapeRouteMaterials/escape-labels.asset (모두 Assets/Resources 아래). commit/push 없음.
- 동기화 후 check-work-synced.txt: 안전성/문서/컴파일/공통 설정 PASS, 자동 테스트 SKIPPED. 실제 녹화는 간접 보조등 수정 완료 시점이며 최신 upstream 공연 기물 배치 변경 반영 전 촬영이다. Unity를 저장 누락 없이 종료했다. 최종 사용자 미감 피드백 및 폰트 캐시 복원 승인만 남음.
- 마지막 fetch에서도 HEAD=origin/dev=e5f772c3 유지, 양 저장소 diff --check 통과. 마지막 import/종료 과정에서 GripYellow.mat와 pole-gate-black.mat의 _Color float 반올림 각1줄도 다시 변경됨. 이를 포함한 자동 변경6개는 원복하지 않고 승인 대기한다.
- 사용자 커밋·푸시·dev PR 요청 수신. Unity 종료 확인 후 origin/dev=3a27bc18과 private a9570530을 fast-forward 반영했다. 최신 대포·로프 아트와 이번 배경 변경은 겹침 없이 보존됨. private 작업 브랜치 resource/slaughter-circus-lighting 생성. 자동 변경6개는 승인 없이 복원하거나 커밋하지 않고 작업 트리에 보존한다.
- submit-work DryRun으로 dev 대상·템플릿 확인. 도구는 작업 트리 전체 clean을 요구하므로 무관한 자동 변경을 보존하기 위해 명시 경로 stage와 gh PR 제출을 사용한다. 로컬 보고서/영상은 .harness/local/task159에 보존하며 공개 PR에 private 에셋 원문이나 녹화 파일을 업로드하지 않는다.
- 2026-11-18 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함). 최신3a27bc18 통합 기준, 공통 설정 PASS. 종료 후 재fetch에서도 최신 유지.
- private 커밋28e0a6b3을 resource/slaughter-circus-lighting에 push 완료. 공개 PR에서 해당 gitlink를 참조하므로 씬·파생 재질을 함께 받을 수 있다.
