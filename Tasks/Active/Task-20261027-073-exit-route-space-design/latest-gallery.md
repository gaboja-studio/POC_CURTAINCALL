# 현재 A 최신 이미지 갤러리

- 요청: 기획서에 최신 버전 이미지를 많이 넣는다.
- 결과: [탈출로 A 기획서](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/33718281), 버전1.2.
- 촬영 기준: 2026-11-03 17:05 KST, 저장된 Assets/Scenes/Levels/escape_route.unity, Edit Mode. 1440×900 실제 렌더24장과 기존 설명도2장, 합계26장.

## 촬영·선정

- 시작홀·공동 전망·첫 대포/착지 외줄·하강·이동 그네·하차장/회수구·복귀 대포·소실 외줄·하부 트램폴린·화염 복도·상승 대포·협동교3종·질주·발사기·배선·출구·갈대밭을 촬영했다.
- 최신 상부 운반선·4종 의상 캐릭터·절단기/받이·회수구 톱날·플레이용 레일과 손잡이 작업등을 실제 씬에서 확인했다. 예전 하부 운반선 사진은 본문에서 교체했다.
- 원안 도식2개는 보존하고 기존 실제 씬 사진3개를24개로 대체했다. 본문 각 구간에 사진·이동/협동 역할 설명을 배치했고 관련 운반선 서술도 현재 배치로 바로잡았다.
- 24장 모두 육안 확인. 최초 22개 카메라 중 운반선/트램폴린/상승대포 등의 구도를 실제 위치로 재조정하고 전체를 다시 촬영했다. 최종 사진은 capture-report.txt의 같은 촬영 묶음이다.

## 보존·검증

- 명시적 프로젝트 경로로 Unity editor_status/list_open_scenes를 확인한 뒤 임시 HideAndDontSave 카메라와 RenderTexture만 사용했다. 저장·기믹 상태 전환·Play/MPPM 없이 finally에서 제거했다. 캡처 완료 당시 A dirty=False/play=False.
- 중간 재촬영은 다른 작업의 순간적인 dirty 상태로1회 거절됐고, 저장/ready를 다시 확인해 촬영했다. 작업 전후 같은 파일 해시 기록 이후에도 병행 변경/미저장이 발생한 것을 확인했으며 자동 저장·복원하지 않았다.
- Confluence native attachment24개 업로드 후 기존 문장/근거를 보존해 갱신했다. 본문 재조회d1:114: 전체26개, 신규24개, 기존 도식2개, 예전 사진3개 제외·핵심 규칙 보존 확인.
- 사용자 IAB는 browser.user unavailable로 추가 시각 확인이 불가했다. 로컬24장 시각 확인과 Confluence 본문 미디어·크기/연결 재조회는 완료했으며 새 페이지 전체의 브라우저 렌더 확인으로 쓰지 않는다.
- Unity 캡처는 편집 상태의 배치를 설명한다. 실제 플레이 위상·체감 난도·연속 완주·성능의 통과 증거가 아니다.

## 로컬 근거

.harness/local/spec-latest-gallery-20261103/: capture.cs, capture-report.txt, objects.txt, 24개 선정 PNG, attachments.json, before-page.html, proposed-page.html, saved-page.html, verification.json. 인증값은 저장·추적하지 않는다.

최종 check-work 결과는 log에 기록한다. 이번 작업의 코드/씬 저장·commit/push는 없다.
