---
summary: 새 UI 키트 수신 뒤 공통 타입 중복 CS0101/CS0111/CS0534가 발생할 때
status: active
updated: 2026-10-20
source: Task-20261019-061
---

# UI 키트 동봉 공통 타입 충돌

## 언제 읽나
- `_ThirdParty`에 새 UI 키트를 받은 뒤 `CurtainCall.UI` 공통 타입 중복과 파생 클래스 오류가 대량으로 발생할 때.

## 증상·원인
- shop/settings/unity_only 키트가 같은 assembly에 공통 Button/SelectableVisual/TabVisual을 중복 선언해 오류55개가 발생했다.
- settings Toggle/Tab·shop RowVisual의 CS0534도 함께 발생했지만 override 구현은 이미 있었다. 중복 기본 타입을 격리하자 파생 오류도 해소됐다.
- 과거 Console 버퍼 오류는 현재 컴파일 결과와 다르므로 `console_status.groundTruth`와 재컴파일 결과를 확인한다.

## 대처
- 기존 정본을 수정/삭제하지 말고 새 키트의 Runtime와 Editor namespace를 한 묶음으로 격리한다. 이 사례는 shop9파일의 선언만 `CurtainCall.UI.ShopV1`/`.Editor`로 변경했다.
- 기존 settings의 `SettingsV1.CommonButtons` alias처럼 이미 격리된 참조는 보존한다. `CurtainCall.UI` 문자열 전체 일괄 치환은 피한다.
- 파일명·클래스명·GUID·PNG를 보존하며, 다른 신규 키트의 미사용 타입까지 일괄 정리하지 않는다.
- 외부 원본은 private에만 저장하고 공개 저장소에는 gitlink와 자체 결과/기록만 저장한다.

## 확인 방법
- 수정 전후 SHA 대조: shop/settings534파일 중 shop C#9개만 변경, PNG/meta/settings 변경0.
- 실제 에디터 재컴파일 오류0과 `compilationFailed=false` 확인 후 EditMode/관련 PlayMode를 실행한다. 컴파일 통과는 상점 UI 동작 검증을 의미하지 않는다.
