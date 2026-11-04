# Log

- 2026-11-04: doctor -SkipEditor 1회 실행, FAIL. 기존 외부 에셋 미초기화/서브모듈 접근 제한, 시작 브랜치 Task 미등록, 기존 Task 번호 중복, Unity 6000.3.25f1 미설치. gh 미로그인·skill-links 경고. PNG 제작에는 영향 없음.
- Unity 프로세스 없음 확인. 기본 권한 fetch 실패 후 승인된 권한으로 origin/dev fetch 성공. HEAD와 origin/dev 차이 0/0 확인, feat/pursuit-eye-art 생성. scaffold-task로 101 생성(담당: 로컬 Git 계정 MoHoDu).
- GPT 기본 이미지 생성 도구 사용. 초기 유색·테두리 시안은 사용자 추가 지시에 따라 폐기. 전 프레임 흰색·무테·무발광으로 수정.
- 투명화 생성 결과에 잔상이 반복되어 깨끗한 검은 배경 원본을 채택. 사용자가 코드 배경 제거·정렬을 명시적으로 승인함. 원본은 eye-source-gpt.png, 재현 스크립트는 Artifacts/prepare-sprites.py.
- 8프레임 1024x512 RGBA 시트, 개별 256x256 PNG, 감은/뜬 정적 PNG, 0.5초 열림/닫힘 GIF, 슬라이스 manifest를 Assets 밖에서 준비. 실제 Unity 적용·commit·push 없음.
- PNG 검증: RGB 전 채널 255, 알파 0~255, 1024x512 RGBA, 개별 프레임 256x256 8장. 접촉 시트 시각 확인. GIF 총 2600ms(열림/닫힘 각각 500ms, 양끝 유지 각각 800ms). verify-knowledge 및 diff --check --ignore-submodules 통과.
- 첫 check-work 안전성/문서 검사는 통과했으나 실행 중 ZIP 패키지 추가로 fingerprint가 달라져 최종 결과 무효. 파일 변경을 멈추고 최종 패키지 기준으로 재검사한다. Unity/자동 테스트는 이번 이미지 제작 범위에서 실행하지 않음.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(Unity 변경 없음); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 승인 후 Unity 적용 (2026-11-04)
- 사용자 디자인/적용 승인. 같은 Task 재개, doctor는 같은 작업에서 반복하지 않음. origin/dev 재fetch, HEAD와 차이 0/0. 기존 수정 보존, commit/push 없음.
- 앞선 미설치/미실행 판단은 샌드박스 가시성 문제였다. 권한 확장 조회로 지정 Unity 6000.3.25f1·기존 프로젝트 인스턴스(PID 18572)를 확인. 새로 시작한 중복 PID 17652만 종료했고 기존 에디터를 사용했다. 시작 씬 WaitingRoom, dirty=false. 동기화할 차이가 없어 열린 에디터에서 Git 병합/체크아웃은 하지 않았다.
- PursuitEyeFeedback 자동 연결, 공통 로컬 observer 선택 재사용. 8개 white PNG를 Unity importer로 Single Sprite/FullRect/256 PPU/비압축/알파로 임포트. Resources 셰이더와 스프라이트를 빌드에도 포함. 네트워크 스냅샷/RPC 변경 없음.
- 상태/거리/관전·포획 숨김, 끊김 없는 중간 전환, 카메라 빌보드와 Head 추적, 에디터 색/HDR 발광/번짐/크기/높이/시간 설정 적용. 기존 씬/마네킹 프리팹 변경 없음.
- EditMode PursuitEyeTests 5/5 통과(범위, 로컬 대상, 전환, 설정 실시간 반영, 리소스/셰이더).
- 첫 PlayMode 실행은 기존 커튼 렌더 확인 실패. Game View 활성화로 해소. 두 번째는 MPB 색의 미세 float 차이 때문에 엄격 비교 실패, 테스트 허용 오차 0.0001로 수정. 세 번째 실제 대기실 PlayMode 1/1 통과(20.87초). 실패를 성공으로 숨기지 않고 원인별 기록.
- Unity 임시 프리뷰 씬에서 마네킹과 3상태 렌더 확인. Head가 목 가까이에 있어 초기 offset .28에서 눈이 머리에 가려짐 → .42로 보정. ChaseColor (.55,.025,.045), ChaseEmission .35로 혈색과 약한 번짐 유지. Artifacts/eye-unity-state-review.png. 임시 씬은 종료, 기존 씬 저장 안 함.
- Unity 작업 중 별도 5파일 변경이 관찰됨: GripYellow.mat, LA_DeadScreenGlass.mat, GB_Dark.mat의 색 float 직렬화 및 escape-labels.asset, Pretendard-SemiBold SDF.asset의 동적 폰트 atlas. 눈 기능 편집 대상이 아니므로 되돌리지 않고 보존했다.
- 다음: 다인 실기·최종 사용자 체감 확인. 설정 위치와 표시 반경 해석은 usage.md.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 검사에서 private submodule의 `ithappy/Battle_Stadium/VFX/FX_Materials/FX_Sparks.mat` 수정 1건도 확인. 이번 눈 기능에서 직접 편집한 파일이 아니므로 보존. 검사 중 새 파일 추가 없이 완료했으며 현재 열린 씬 dirty=0. 별도 수행한 EditMode 5/5·PlayMode 1/1 결과는 위 기록과 구분한다.

## 전체 설정 저장·제출 (2026-11-04)
- 사용자: 추격자·조명 설정 포함 모든 변경 저장/push 및 dev PR 요청. 사용자가 전체 검증 완료를 확인했으므로 doctor/check-work/컴파일/테스트/플레이를 추가 실행하지 않았다. 이전 검사 결과는 과거 결과이며 이번 최종 설정의 재검증 PASS로 간주하지 않는다.
- save-work·submit-work 절차를 사용하되 검사를 강제하는 save-work.ps1 대신 사용자 지시에 따라 직접 Git 저장을 수행한다.
- Unity Play stopped·WaitingRoom dirty=false 확인 후 SaveOpenScenes/SaveAssets로 모든 조정값 저장. 저장 후 에디터 정상 종료, origin/dev fetch 후 겹치는 파일 없이 8개 신규 커밋 fast-forward 반영.
- 사용자 조정값: 눈 Width .2/HDR 색/Glow .3, 추격 단계 0/30/60/90초 및 속도 6.5/7/7.5/8m/s, 화면 테두리, 조명 밝기/붉은색/방 깜빡임·포획 암전. 폰트/머티리얼 등 나머지 변경도 요청대로 함께 저장한다.
- 외부 스파크 머티리얼: CURTAINCALL_PaidAssets의 최신 origin/main을 반영한 resource/pursuit-eye-settings 브랜치, commit 11a5483b1d91ee06822dda9f83bc37758e431b63 push 완료. 메인 저장소 gitlink에 같은 버전을 기록한다.
- 작성자는 gh 로그인 계정 MoHoDu. dev 직접 push/병합은 하지 않고 PR로 제출한다.
