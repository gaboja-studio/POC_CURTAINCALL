# Log

## 2026-11-04 재개용 저장
- 사용자 요청: 현재 작업을 브랜치에 보관하고 나중에 재개. 추가 검증 없이 바로 저장하라는 후속 지시에 따라 doctor/check-work/Unity/자동 테스트를 다시 실행하지 않는다.
- 최신 기준은 v5 원화와 실제 FBX 비교 렌더. v3 의상은 유지하며 v4의 큰 머리/손/체형은 제외한다. 원본 FBX10종/CharacterArt 실제 캡처가 형태와 재질 기준이다.
- 재개 순서: plan → todo → design-brief, private Concepts/JointSeams/fbx-proportions-{male,female}-v5.png 확인. M02 추가 목 봉제·F06 상완 길이·가려진 관절을 먼저 교정하고 사용자 원화 검토 후 다방향 제작 도면/Blender 파생 모델로 진행한다.
- private 원화·프롬프트·측정 자료는 resource/pursuit-joint-seams-v5 작업 브랜치에 저장하고 공개 feat/pursuit/enhancement에서 해당 gitlink를 보관한다. private main 또는 공개 dev 직접 반영은 하지 않는다.

## 2026-11-04 원화 작업 및 체형 피드백
- 최종 검사: verify-knowledge OK(16폴더), 원본 FBX10/10 해시 동일. check-work -SkipUnity 1회: 안전성/문서 PASS, Unity 컴파일 SKIPPED(-SkipUnity), 자동 테스트 SKIPPED. 전체 완료 PASS로 표기하지 않는다. 기존 문서 길이 경고51건, private 미커밋 변경 안내가 남았으며 요청 없는 commit/push는 하지 않았다.
- doctor 1회: Unity 지정 버전 미설치, third-party 상태/기존 Task 중복/브랜치 미연결 실패. Task102를 현재 별도 작업 브랜치에 등록했고 원화 단계는 계속 진행했다. 기존 로그의 별도 저장 작업 기록은 보존한다.
- 착수 fetch 후 HEAD=origin/dev(dac0f1bd). 재개 후 외부 저장/동기화로 HEAD20b9062e, 최신 origin/dev 대비4 ahead/0 behind를 확인했다. Unity 프로세스 없음, 기존 작업 보존, 이번 작업에서 commit/push 없음.
- 실제 Unity 캡처와 공통 셰이더에서 세로 봉제 원인을 확인. 사용자의 혈흔 증가·때/긁힘·리본 제거·다른 단원/인형 세계관·기존 체형 유지 지시를 순차 반영했다.
- v3 의상 방향 평가를 유지하고 v4 과장된 머리/손/체형은 제외. CharacterArt 네 실제 캐릭터 캡처와 README를 읽고 v5를 직접 FBX 렌더 기반으로 재제작했다.
- Blender5.2.3 설치 확인. 별도 --background --factory-startup으로 기존 FBX10종을 import/render/측정만 했다. 원본10개 SHA256 일치, blend/FBX 저장·내보내기나 리그 편집 없음.
- 최신 결과: private Concepts/JointSeams/fbx-proportions-male-v5.png 및 female-v5.png. Source~에 실제 FBX 비교 화면/치수/본/해시/재현 스크립트/내장 image_gen 프롬프트 보관.
- 그림은 2D 검토 초안. M02 추가 목 봉제, 의상에 가려진 관절, F06 상완 결손 길이 등 생성 오차를 design-brief에 명시했다. 기존 FBX를 원화의 과장에 맞춰 변형하지 않는다.
- 다음: 사용자 외형 검토, 관절 배치·다방향 도면 정리 후 Blender 파생 제작. Unity 코드·프리팹·셰이더·게임 동작 변경 없음.

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-04 작업 보관
- 사용자 요청으로 현재 글꼴 에셋 변경과 Task 문서를 feat/pursuit/enhancement에 보관한다. 원격보다 앞선 추격자 수정 이력도 함께 push한다.
- 사용자 지시로 doctor/check-work/Unity/자동 테스트는 실행하지 않는다. Git 저장·업로드 보호 훅은 유지한다. 관절 봉제선 작업 자체의 완료를 뜻하지 않는다.
- 다음 작업: 최신 origin/dev에서 통합 WaitingRoom의 추격자·리허설 반영 상태 확인.


- 추가 보관: 비공개 CURTAINCALL_PaidAssets의 관절 봉제선 원화 3장과 .meta 4개를 resource/pursuit-joint-seams의 aadf0c66에 커밋·push(LFS 3개 업로드 완료). 개인 저장소의 기존 별도 작업 브랜치에서 미추적 상태였으므로 공개 저장만으로 보관되지 않았었다.
- 비공개 main 직접 push는 자동 승인 검토가 요청 범위 밖으로 거부하여 실행되지 않았다. 작업 브랜치 보관으로 완료했고 게임 gitlink는 변경하지 않는다.


## 2026-11-04 비공개 main 반영·게임 연결 갱신
- 사용자가 비공개 main 반영과 feat/pursuit/enhancement의 gitlink 갱신·push를 명시 승인했다. 이전 직접 main push 거부로 남았던 상태를 해소한다.
- 원화 3장과 메타를 담은 aadf0c66을 비공개 origin/main에 일반 fast-forward push 완료(a5747288 → aadf0c66).
- 최신 origin/dev 295b5bec를 feat/pursuit/enhancement에 병합했고, Assets/_ThirdParty 연결 대상을 aadf0c66으로 갱신한다. 공개 dev는 변경하지 않는다.
- 사용자 지시로 doctor/check-work/Unity 컴파일/자동 테스트를 실행하지 않았다. Git 저장·업로드 보호 훅은 유지한다.


- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
