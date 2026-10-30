# Log

- 2026-10-31: PR #251 dev 병합(464a0585). 원격 dev와 남은 변경이 겹치지 않음을 확인하고 별도 refactor 브랜치에서 문서 정리.
- 남은 세 TMP 폰트·라벨 에셋은 기존 글자 표/아틀라스가 비워졌거나 텍스처가 1×1로 바뀐 동적 폰트 캐시 변경이다. 카메라 기능상 의도한 글꼴 수정 근거가 없어 커밋에서 제외하고 원본 작업본은 보존한다. [TMP 함정](../../../Harness/Engine/Unity/Pitfalls/tmp-dynamic-font-churn.md).
- 인간 대포 Task-083 미리보기 PNG 14개는 기존 이미지와 다른 LFS 콘텐츠이며 해당 PlayMode 검사 코드가 같은 파일명을 출력한다. 캡처가 의도적으로 갱신됐는지는 확정할 수 없어 이번 커밋에서 제외하고 보존한다. 해당 작업의 검토가 필요하다.
- 카메라 PlayMode 9/9, 설정 PlayMode 1/1은 PR 작업의 검사 결과다. 전체 PlayMode는 중단되어 결과가 없고 실제 키보드로 전 구역을 이동하는 최종 체감은 남았다.
- 검사: 기본 샌드박스에서는 LFS 프로세스 권한 오류로 변경 조회 실패. 재실행 시 안전성·문서/규칙 PASS, 보존 중인 에셋 변경이 함께 감지되어 컴파일은 `-SkipUnity`로 생략. 이번 공유 대상은 문서뿐이며 새 Unity 검사는 실행하지 않았다.
- 제출: [PR #252](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/252), refactor/camera-followup-record → dev. 검토 대기.

- 2026-10-31 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=SKIPPED(-SkipUnity); 테스트=SKIPPED(자동 테스트 선택 안 함)
