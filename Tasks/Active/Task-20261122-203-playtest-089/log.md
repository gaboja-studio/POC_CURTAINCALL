# Log

- 최신 origin/dev 092f6070 및 에셋 9f16297c 기준. 원본 에디터와 수정 중인 에셋을 보존하기 위해 별도 작업 복사본을 만들었다.
- doctor에서 에디터·서브모듈을 찾지 못했으나 사용자 환경에서 Unity 6000.3.25f1과 에셋 커밋을 확인했다. 기존 Task ID 충돌은 빌드 입력과 무관하다.
- Unity 빌드 성공. Builds/CURTAINCALL_LIVE_0.8.9 및 Builds/CURTAINCALL_DEV_0.8.9. 두 배포 폴더에서 0.8.9, 로그 전송 설정과 정비·종료 설문을 확인했다. 각 플레이어 어셈블리의 DevMode 초기값은 LIVE false, DEV true다. 게시자 인증서 미설정으로 두 EXE는 미서명. 실플레이 및 서버 수신 검사는 수행하지 않았다. 최종 fetch 기준 dev 일치.

- 2026-11-22 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)
