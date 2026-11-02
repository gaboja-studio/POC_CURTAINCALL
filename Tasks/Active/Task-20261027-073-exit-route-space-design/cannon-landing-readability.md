# 3인칭 대포 착지 가독성 (2026-11-02)

> 후속 사용자 요청으로 공중 상하 제한은±89도로 해제했다. [현재6항목 변경](route-six-fixes.md) 우선.

- 기존30파일 해시 백업·에디터 정상 종료 후 dev/작업 브랜치 fetch. origin/dev8c64b454 포함/Already up to date,동일 Task doctor 재사용. commit/push·Play/MPPM 없음.
- A HumanCannonSystem.airborneView에 escape-airborne-third-person 프리셋을 연결. 자유3인칭 거리6.5m/높이8.5m/주시높이0.65m/LookAhead0/FOV78/수직 공전±10도/전환0.35초. 수평 마우스 시선·공중 조작/탄도 유지. 공중에만 적용,탑승/평상시/운반은 기존 구도. 착지 복귀도0.35초.
- 원본 GroundMarkers에 opt-in showSafeLandingFeedback/feedbackRopes11개 연결. 실제 발밑 면이 공용 IsSafeLandingSurface를 통과하고 중간 장애물이 없으면 초록 테두리. 일반 바닥은 기존 흰 테두리,구멍은 표시하지 않는다. 현재 수직 정렬 표시이며 미래 궤적/위험물 안전을 보장하는 예측은 아니다.
- 로컬 참가자의 안전 외줄 면에만 최대2.4m×0.24m 초록 강조. 기존 지지 범위 안에서 끝을 잘라 표시,착지 후/비활성/이탈 때 숨긴다. 물리 Collider를 추가하지 않는다.
- 원본 표시 재질/Shader 재사용,색은 MaterialPropertyBlock. 공용 Shader 기본 색은 기존과 동일,다른 씬은 opt-in 기본false/공중 프리셋null로 기존 동작. 대기실 씬/원본 인벤토리·카메라 프리셋은 수정하지 않는다.
- 기존 Transform/물리 문서6149개 ID/내용 비교: 변경·추가·삭제0. 외줄 폭/착지 허용 범위/발판/탄도 불변. 초기 InstanceID 스냅샷은 도메인 리로드 후 달라져 영속 Scene 문서 ID 비교로 확인했다. 편집 검사 전후 원래 지형도 동일.
- EditMode: 신규 피드백4/4·비행 카메라2/2·기존 원본 표시3/3,합계9/9. 초기 MPB 필드 초기화 예외는 실제 사용 시 생성하도록 수정 후 통과. 컴파일 중 조기 호출한 검사는 중단됐으며 리로드 완료 후 정상 실행했다.
- 실제 A 외줄11개×3위치의 지지/가림33/33. 정적 구도9개(높이2/8/18m×pitch−10/0/10) 발/목표 동시 표시. 실제 벽 충돌 적용33표본도33/33 동시 표시(8표본 스프링 암 당김). FOV76에서 소실 외줄 고도18m4표본이 하단−0.004여서78로 조정 후 통과.
- 네이티브 편집 렌더 before/after는 캐릭터 크기의 임시 캡슐을 사용했다. 실제 플레이 입력/완주/네트워크·재미 검증이 아니다. 실제 체감·다인 화면은 다음 사용자 검토에 남긴다.
- 증거: .harness/local/cannon-rope-readability-20261102/backup-manifest.json,physical-document-diff.json,final-audit.json,wall-framing.json,feedback-tests.json,camera-tests.json,original-marker-tests.json,before/after.png.
- A 누락 스크립트0·dirty0·Play stopped. 통합 check-work 결과는 log 참조.
