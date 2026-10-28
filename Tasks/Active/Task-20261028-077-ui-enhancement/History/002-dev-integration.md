# 2026-10-29 저장·dev 통합

- 사용자 요청: 현재 구현 저장 → origin/dev 통합/충돌 해결 → 통합 검증 후 저장 → origin push/dev PR 제출. dev 직접 병합은 요청 범위가 아니다.
- 초기 공개 dc693dfc, private 36f39888로 기존 구현을 보존했다. gh 계정 MoHoDu 작성자이며 AI attribution 없음.
- 저장 전 doctor FAIL 1(private dirty). check-work는 안전성/문서·규칙 PASS 후 사용자 Play 실행으로 Unity 검사 FAIL(Play Mode 중)이었다. 사용자 종료 허락 후 조회 시 이미 EditMode였다. 이전 성공 결과와 새 실패를 구분한다.
- origin/dev de8258db(설정 v2·월드 폰트·사망 연출)를 통합했다. C#과 씬 자동 병합, 유일 충돌은 Assets/_ThirdParty gitlink다. private main e7765d41을 시계 브랜치에 병합한 f5131050으로 해결해 양쪽 에셋을 보존했다. private LFS pull/46개 66 MB push 완료.
- 비공개 PR: https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/7 (resource/third-party/dayclock-ui → main). 자동 병합하지 않았다.
- 통합 컴파일 PASS. 보조 AI 읽기 전용 검토는 RunState/CampaignSnapshot/BaseGameSettings·설정 v2/HUD 연결에서 구체적 병합 회귀를 발견하지 못했다.
- 전체 EditMode 일괄은 HTTP400 뒤 장시간 running·eval timeout으로 취소했다. 후속 첫 필터 호출에서 전체 결과처럼 보이는 1144건/1143통과/1실패가 섞였으며, 반환된 최근 결과에 실패 상세가 없었다. 정상 PASS로 취급하지 않는다. 이후 대상 클래스를 분리해 실행한다.
- 통합 EditMode 대상: DayClock 26/26, RunTimeLimit 15/15, HudLayout 2/2, NotificationTextLayout 3/3, DurableOutbox 20/20, TransportPolicy 7/7, RunAbandon 6/6, LocalAudioSettings 7/7, InputBindingPreferences 17/17. 합계 103/103 PASS.
- SVGImporter/UI Toolkit 65535 정점 한도 예외 2건은 dev 설정 패키지 Previews/ui_sheet_settings_layers_review@2x.svg(146115 정점), ui_mockup_settings_overview_normal@2x.svg(142059 정점)의 검토용 SVG 썸네일 임포트 문제다. 이전 임포트 로그에도 동일 오류가 있으며 Assets의 코드/프리팹/씬/UI 문서에서 런타임 참조 없음 확인. 실제 설정은 C14_Settings_v2.prefab을 사용한다. 원본 디자인 파일을 임의 변경하지 않았고 컴파일 성공·게임 기능 검사와 구분한다.
- 첫 공통 UI PlayMode 55건은 47통과/8실패였다. 기능 판정을 느슨하게 바꾸지 않고 테스트 계약을 최신 구현과 맞췄다: reflection 선택 인수 명시(2건), 실제 페이드 시작 이후 시간 측정(1건), 2칸 HUD에 3번째 슬롯 부재 검사(1건), SDK 미준비/기준비 양쪽 오디오 초기화·비동기 적용 계약 검사(1건), 전문가 영업 문구·밤 구매 전제·상호작용 위치와 LateUpdate 동기화(3건).
- 상호작용 위치 실패는 수화기 동쪽 카메라에서 실제 Pillar가 가리고, 배송 기물보다 수화기가 가까운 테스트 배치였다. 시야가 열린 서쪽·기물 근처로 fixture를 옮겼다. 차폐 판정은 변경하지 않았다. 상호작용 클래스 개별 재검사 4/4 PASS. 전체 UI 재검사 55건은 54통과/1실패였고 마지막 실패는 종료 연출 테스트가 미사용 legacy title 필드와 커튼 즉시 해제를 기대한 문제였다. 실제 C12 title과 커튼 열림 완료 대기로 맞춘 뒤 해당 클래스 2/2 PASS. 최종적으로 55개 대상 모두 통과 결과를 확보했다(단일 55/55 실행은 아님).
- CommonButtons 동적 TMP 아틀라스 텍스처 갱신도 보존한다. 최종 check-work 안전성/문서·규칙/컴파일 PASS, 길이 경고 19건·hard 0, 씬 dirty 0. 스크립트 전체 자동 테스트는 선택하지 않았고 위 분리 실행 결과로 검증한다. 2인 네트워크 실플레이와 최종 사람 체감은 별도 확인이다.
- 통합 결과를 공개 67ea57fe로 커밋했다. 공개 push는 verify-third-party.ps1 -CheckSubmodule 훅에서 거부됐다: private 포인터 f5131050이 origin/main의 조상이 아니기 때문이다. 작업 브랜치 push/PR7은 완료했지만 main 미병합이라 훅을 통과하지 못한다. 훅 우회·private 자동 병합·공개 dev 직접 push는 하지 않았다.
- private PR7에 gaboja-studio/pm 팀 검토 요청 명령과 API는 성공 응답했으나, 후속 requested_reviewers 조회의 팀/사용자 목록은 비어 있었다. 검토자 지정 완료로 간주하지 않으며 PM의 실제 확인이 필요하다. 공개 new-pr.ps1 -DryRun은 본문 생성 후 최신 브랜치 원격 미게시로 FAIL, 실제 공개 PR은 생성하지 않았다. 사용자 요청 1~3은 완료, 4는 private 검토·main 반영을 기다린다.
- 재개: private PR7 검토·main 반영 후(merge commit이면 f5131050 보존, squash면 main 커밋으로 gitlink 갱신·자산 내용 일치 확인), 공개 작업 브랜치 git push -u origin feat/ui-enhancement → Scripts/new-pr.ps1 -Base dev -Title "HUD 배치·하루 시계 v2.7 및 입력 끊김 개선" -Summary "HUD·하루 시계 및 입력 끊김 개선, 최신 dev 통합. private PR7 및 본 문서의 검사/제약 참고." 순서로 제출하고 링크를 기록한다.
