---
summary: 게임 문구의 기준 CSV와 시트 변경분을 가져오는 방법
status: active
updated: 2026-11-27
source: Task-20261127-207
---

# 게임 문구를 수정할 때

- 게임은 `Assets/Resources/Localization/`의 공간·콘텐츠별 CSV를 함께 읽는다. 각 파일명은 시트 탭 이름과 같다. 네 열은 `key,source,ko,en`이다. 키 형식은 `TAB_SUBCATEGORY_ITEM`다. 첫 밑줄 앞이 탭 이름이다. 대문자 A–Z를 쓰고 단어 사이는 하이픈으로 연결한다. 소분류는 한두 개이며, 같은 항목의 변형은 모두 끝에 _0, _1 순으로 붙인다. 같은 용도의 키도 전체 번역표에서 고유해야 한다. 한번 연결한 키와 원문은 번역 수정 시 유지한다.
- 공동 시트는 `key` 바로 오른쪽에 `key description`이 있다. 이 칸에 표시 상황과 치환 자리의 뜻을 적는다. 설명만 고치면 가져오기 결과는 0행이며 게임 파일과 동기화 기준 모두 그대로다.
- 예를 들어 `REHEARSAL_OBJECTIVE_CROSS-LANE`의 문장에 들어간 `{SETTINGS_INPUT_SIDE-STEP}`은 SETTINGS 탭의 같은 키를 가리킨다. 기본 좌클릭/우클릭과 뒤돌기의 기본 휠 클릭은 각 행의 설명에서 확인한다. 화면에는 플레이어가 지정한 버튼이 표시된다. ‘INDEX → 치환 코드 찾기’에서 키를 누르면 해당 행으로 이동한다.
- 마우스 왼쪽 버튼은 두 소분류를 쓴 `SETTINGS_BUTTON_MOUSE_LEFT`다. 상점의 기록 시각 세 가지는 `SHOP_TEXT_RECORDED-TIME_0`부터 `_2`까지다. 숫자는 맨 끝 변형 번호에만 쓴다.
- 기물 이름은 `{SHOP_VALUE_ITEM-NAME}`, 가격은 `{SHOP_VALUE_PRICE}`, 닉네임은 `{COMMON_VALUE_PLAYER-NAME}`이다. 번역할 때 게임에서 채울 자리의 순서는 옮길 수 있다. 한국어 조사는 `{SHOP_VALUE_ITEM-NAME}(을/를)`과 같이 적는다. 값 행에서 `{COMMON_VALUE_CURRENT}` 앞뒤의 표기를 바꾸면 그 행을 참조하는 문구에 적용된다.
- 번역에서 고정 문구도 `{TAB_SUBCATEGORY_ITEM}`로 참조할 수 있다. 저장 전에 참조 대상의 존재와 서로 되돌아가는 연결을 검사한다. 기존 게임 데이터 및 이전 시트의 키는 `LocalizationKeyAliases.txt`, 기존 게임 코드가 넘기는 값 이름은 `LocalizationArguments.txt`로 연결한다.
- Unity의 **Curtain Call → Localization → Open translation sheet**로 **INDEX**를 연다. 원하는 탭을 선택해 `ko` 또는 `en`을 고친다. 위치를 모르면 **수정 → 찾기 및 바꾸기 → 모든 시트**에서 찾는다. 새 키는 해당 로컬 CSV와 같은 이름의 시트 탭에 추가한다.
- **Sync from Google Sheets**는 전체 통합 문서를 한 번에 읽는다. 비공개 시트는 브라우저에서 로그인해 XLSX를 내려받은 뒤 **Import downloaded workbook or CSV**로 연다. 개별 탭의 CSV나 예전에 받은 통합 CSV도 같은 메뉴에서 문구 코드로 연결한다.
- 에디터는 열 순서와 코드 중복을 확인한 뒤, 마지막 동기화 이후 시트에서 달라진 행의 원문·빈 번역·치환 자리·서식 태그를 검사한다. 확인창에 표시된 행 수를 보고 반영하면 다음 플레이부터 적용된다. 행 순서를 바꾸거나 동일한 시트를 다시 불러와도 번역은 그대로다.
- 로컬에서만 고친 행은 보존한다. 같은 행을 양쪽에서 다르게 고치면 해당 코드를 표시하고 저장을 중단한다. 두 번역을 대조해 최종 값을 일치시킨 뒤 다시 불러온다. `Scripts/Localization/sheet-baseline.tsv`는 각 행을 마지막으로 동기화한 상태를 기록하므로 번역을 반영할 때 로컬 CSV와 함께 버전 관리한다.
- 새 고정 문구는 TMP 또는 Text 컴포넌트에 `@loc:문구코드`를 지정한다. 기존 화면은 원문을 표의 코드에 연결한다. 같은 원문이 여러 코드에 있으면 원문 연결은 첫 번째 행을 사용하므로, 문맥별 번역이 필요한 새 화면에는 코드를 직접 지정한다. 자료 수집 시 `catalog_key`로 현재 연결을 확인한다.
- 분할할 때 원문이 같은 행은 한 파일에 기존 순서대로 모았다. 이미 값이 채워진 안내가 여러 치환 문장에 들어맞는 경우에는 `LocalizationTemplateOrder.txt`의 기존 순서를 사용한다.
- 플레이어는 **설정 → 기타 → 언어**에서 기기 언어·한국어·영어 중 선택하고 적용한다. 각 PC에 저장한다. 첫 실행은 한국어 기기면 `ko`, 나머지는 `en`이다. 플레이 중에는 시트에 접속하지 않는다.
- 씬 전환 때 비활성 글자를 포함해 연결한다. 실행 중 생성하는 TMP는 첫 텍스트 갱신 때 연결된다. 코드로 UGUI Text나 월드 TextMesh를 생성할 때는 `LocalizedDisplays.BindLegacy(label)`를 호출한다. 입력란의 실제 입력값은 그대로 표시한다.
- 수집을 다시 실행하려면 저장소 루트에서 `python Scripts/Localization/collect.py <결과.csv>`를 사용한다. PowerShell 7과 ripgrep이 필요하다. 별도 CSV에 씬·프리팹 및 덮어쓰기, 안내 데이터, 코드 후보의 파일과 위치를 기록한다. 코드 후보에는 시험 화면과 개발용 문구도 포함된다.

[글로벌 대응 작업 기록](https://hrjoo122770.atlassian.net/wiki/spaces/bYmkRlnpusng/pages/52625410) · [번역 시트 분류 안내](https://docs.google.com/spreadsheets/d/1Qs0xnPnU4R-gWUDGhLk56h2RYMBIbh6IxJ-Qi0OFgKo/edit#gid=278991085)

