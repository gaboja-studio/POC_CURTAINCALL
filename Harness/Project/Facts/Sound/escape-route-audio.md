---
summary: 탈출로 시트 지정 클립의 private 경로, 공용 카탈로그 연결과 보류 상태
status: active
updated: 2026-11-19
source: Task-20261119-167, 사용자 사운드 요청, 사운드 리소스 데이터 탈출로 전용 gid119171350
---

# 탈출로 사운드

## 언제 읽나

탈출로 효과음·공장 배경음을 교체하거나 사운드 설정 창에서 시트 ID의 재생·음량을 바꿀 때.

## 사실

- 원문은 `Assets/_ThirdParty/Gamemaster Audio - Pro Sound Collection/`. WAV 8,079개와 원본 메타·폴더·PDF를 private 저장소에 보관한다.
- 2026-11-19 사용자 지시로 로컬에는 전체 라이브러리를 유지한다. 제출 시 실제 사용 클립·해당 `.meta`·필요 폴더 메타만 private 커밋에 포함한다.
- 시트의 `탈출로 전용` URL 열에서 선별한 17개 항목·23개 클립을 공용 `Assets/Resources/Settings/Sound/SoundCatalog.asset`에 연결한다. `EscapeRouteSoundSetup.Apply()`가 누락·동명 중복을 확인한 뒤 연결하며 기존 다른 큐와 기존 음량을 보존한다.
- 파일명 선정은 `탈출로 전용` 탭의 URL 열을 따른다. [사운드 리소스 데이터](https://docs.google.com/spreadsheets/d/1hy9U_T6LEUIH_TC7Yb20Vw7FcfMkYVV_fXpCo0thyek/edit?gid=119171350#gid=119171350)
- 설정 창은 `Tools/CurtainCall/사운드 설정`. 시트 ID의 클립·음량·피치·구간·루프·3D 거리와 재생 기록을 확인한다. 재생기는 `SoundManager.PlayAt`으로 위치 음원을, `Play`로 화면 음원을 재생한다. 사용자 효과음 음량은 기존 `LocalAudioController`를 거친다.
- 추격자 벽 붕괴·발소리·포효·착지·팔 예고·포획·투척, 외줄 점멸·소실·복원, 추격불 경고·점화·연소, 접이교 전개·완료, 휴대 톱 발사·출구 개방에 연결한다. 재생은 기존 동기화 상태의 표시 시점에 일어나며 별도 네트워크 메시지를 추가하지 않는다.
- 사용자 청취 후 반복 공장 배경음 `ESC-028`은 제거했다. 추격자 발소리는 유지한다. 외줄 전기음은 플레이어 8m 이내이고 벽에 가리지 않은 지점에서만 재생하며, 재생 중 차폐·거리 이탈에도 종료한다.
- 시트에서 보류인 `ESC-015`, `ESC-016`, `ESC-022`는 클립과 호출 지점만 준비하고 새 카탈로그 항목의 `enabled`를 꺼둔다. 설정 창에서 켜면 적용된다. URL이 빈 17개 항목은 선별 대상에서 제외했다.
- 이동 음원은 각 voice의 별도 Transform을 사용한다. 대상 비활성화·제거 시 루프를 멈추며, UI 음원으로 재사용하면 위치·3D 설정·추적 대상을 초기화한다. 접이교·화염·공장 루프는 완료·재시작·비활성화 때 정리한다.
- 원본 package의 pathname 끝에 불필요한 문자가 있었고 `ImportPackage` 호출은 파일을 생성하지 않았다. WAV와 원본 `.meta`를 외부 임시 폴더에 추출한 뒤 Unity 에디터에서 private 경로로 복사하고 `AssetDatabase.Refresh`로 임포트했다. 파일·폴더 8,172개의 누락과 GUID 불일치가 없음을 대조했다.

## 확인 방법

- `EscapeRouteSoundSetup.Audit()`로 지정 17개 항목의 23개 참조·경로·길이를 확인한다.
- Task-167에서 check-work, 선택 EditMode 82개, UiSoundTest 런타임 19개 검사가 통과했다.
- 청취 수정 후 근접·차폐 회귀 검사 6개와 사운드·기믹 검사 16개가 통과했다. private main edc4b497에 선별 클립 23개와 메타를 저장했다.
- `Assets/Scenes/Tests/Systems/Sound/UiSoundTest.unity`에서 카탈로그 ID를 직접 재생하거나 실제 탈출로에서 해당 이벤트를 확인한다.
- 실제 플레이의 음색·밸런스·루프 이음새와 멀티플레이 청취는 사람 확인이 필요하다.
