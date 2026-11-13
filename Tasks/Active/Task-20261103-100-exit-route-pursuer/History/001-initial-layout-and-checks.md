## 2026-11-03 A 실측·크기/배치 검토·Confluence 기획서
- 사용자 요청으로 A 실측과 배치/맵 수정 검토에 착수했다. Unity 종료 상태에서 origin fetch 및 origin/dev 반영 확인(차이0/0,6491a570). 기존 AGENTS/Task 미커밋 변경 보존. doctor는 같은 작업에서 이미 실행해 재사용.
- 사용자 추가 방향: 전용 공간/경로 허용, 현재 A 변경 가능,3.2m보다 큰 압도감. 최종 키는 미정이며 5m 중심/6m 비교안으로 확대했다.
- 실제 A 4,973 Transform/활성 비트리거 Collider1,590 조사. 처음은3.2m 캡슐,이어5/6m 실제 외형 및5m 주요5곳 정지 AABB 확인. 컨베이어 레일/반동 영역과 후반 접이교3곳·바닥 없는 후보를 발견해 외측정비로/협동북측/하부우회 예약으로 구체화했다. 범위와 한계는 layout-review.md.
- Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapePursuerLayout.unity를 additive 검토용으로 생성. EditorOnly/Collider0/MonoBehaviour0. 별도 편집 도구로 열기·앵커 위치 갱신 지원. 원본 A SHA256 30f32464…97c497c 일치·dirty0,원본 GUID/기믹·빌드 설정 보존. 게임 AI·피해·실제 벽 절개·연속 통로는 미구현.
- 5m 외형(높이5/폭2.87/깊이1.18),6m(높이6/폭3.45/깊이1.41) 비교와 5m 주요 장면6장 캡처. Review/에 저장. 초기 layout-validation.json은 과거3.2m 이력이며 최종은 size-upgrade/review-validation.json.
- 주형락 작업 노트(5046324,현재 Atlassian 계정과 작성자 일치) 아래 live doc [탈출로 거구 추격자 기획](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/33161228) 생성. 사용자 확정·추천·미검증을 분리하고 맵 변경/5m·6m/모션·맵 수정·AI/협동·다음 작업을 작성했다. 크기 비교·P1 캡처2개 첨부,미리보기 링크와 저장 본문 재조회 확인. 이미지 직접 삽입은 업로드 응답에 collection이 없어 검증 거부됐으며,기존 본문을 보존해 지원되는 첨부 미리보기 링크로 연결했다.
- 도구 메모: 이 Pipeline의 eval_file은 문장 본문용이며 전체 C# 클래스는 run_script로 실행했다. 최초 strict recompile은 오류0/기존경고17로 종료1; 신규 코드 오류는 없었다. 이후 자동 재컴파일도 오류0. 최종 check-work는 아래 기록.
- 부수 변경 정리: Unity가 동적 escape-labels 글리프 캐시를 저장했다. Unity API로 캐시만 정리하려다 ClearFontAssetData가 원래 폰트 feature table도 비워,HEAD 원본을 외부 검토 파일로 읽고 Unity SerializedObject로 해당 feature table을 복구했다. 폰트 git diff0/원본 A 불변 확인. 원본 YAML 수동 편집·reset/discard·commit/push 없음.
- 다음: 변경된 A 기준5/6m 전체 이동·회전/숙이기/공격·도약·출입구,4인 필수 조작 시간을 재검사하고 구간 사이 전용 연결과 후반 승강 방식을 확정한다. 피해/지연 수단·화염 역할 및 사람의 압도감 판단은 미정이다.
- 최종 검토 씬 재열기: dirty0/누락 재질0/누락 스크립트0/Collider0/EditorOnly 확인. check-work 종료0: 안전성·문서/규칙·컴파일 PASS,자동 테스트 SKIPPED. 원본 A와 동적 폰트 git diff0. Confluence 최종 본문 재조회에서5m 실측·큰 모델5곳 정지 배치·맵 변경 원칙·첨부2개 링크 확인. commit/push 없음.

## 2026-11-03 브랜치·Task 준비
- 담당 @RagSpirin. 사용자 승인으로 feat/exit-route/design-map의 f9d5db54에서 feat/exit-route/pursuer를 생성했다. Unity 프로세스 없음/Pipeline 연결 0, 주 저장소 및 Assets/_ThirdParty 작업 트리 clean 확인 뒤 origin을 fetch했다.
- origin/dev의 6491a570(PR #272 병합)을 fast-forward로 반영했다. 기존 A 작업이 dev에 포함되어 있어 별도 커밋·충돌 해결은 발생하지 않았다. 커밋·push·PR 제출 없음.
- scaffold-task WhatIf 후 Task-20261103-098 3문서 생성. 기존 대화의 연구·장면 배치안은 후보로 기록하고, 확정된 탈출로 전용·그레이박스·배치/연출 우선 방침과 구분했다. 게임 코드·에셋·씬은 수정하지 않았다.
- 사용자 추가 지시: 작업 시작·재개 전 및 PR 제출 직전 최신 origin/dev를 반영한다. AGENTS.md에 반영하고 동기화로 코드/에셋이 바뀌면 필요한 검사 후 제출하도록 기록했다.
- 이번 대화 착수의 doctor 결과를 재사용해 반복 실행하지 않았다. 기존 Task ID 중복으로 FAIL, unity-cli 버전/skill-links WARN을 확인했으나 이번 브랜치 생성·문서 준비를 막지는 않는다. 기존 중복은 변경하지 않았다.
- 다음: 현재 A 씬에서 등장 후보·거구 통과 공간·우회 경로·4인 필수 조작 시간을 확인하고 맵 수정안을 구체화한다. 모델/AI 구현 전 배치와 필요한 모션부터 정리한다.
- 착수 도구 제약: start-work.ps1은 Scripts/Lib/common.ps1의 단일 슬래시 TaskBranchPattern 때문에 기존 관례의 중첩 이름 feat/exit-route/pursuer를 거부했다. 브랜치 생성/Task 생성은 정상 완료됐으며 현재 브랜치·plan 연결과 origin/dev 및 기존 f9d5db54 포함을 직접 확인했다. 다른 Task에 영향이 있는 공용 도구는 이번 준비 묶음에서 수정하지 않았다.
- 준비 묶음 check-work.ps1 -Base origin/dev 종료0: 안전성 PASS, 문서/규칙 PASS, 컴파일 SKIPPED(Unity 변경 없음), 테스트 SKIPPED(자동 테스트 선택 안 함). 기존 문서 길이 WARN 44개/hard 0. HEAD와 origin/dev 차이0/0.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

