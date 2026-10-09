# 외부 에셋(유료·무료) 넣는 법 (에디터·GitHub Desktop 기준)

마지막 갱신: 2026-10-09 · 결정 근거: `Harness/Project/Decisions/third-party-assets.md`
컨플루언스에 공유할 때는 이 문서를 최신으로 고친 뒤 그대로 옮긴다.

## 한눈에

- Asset Store 등 **밖에서 받은 에셋은 무료·유료 상관없이** 비공개 저장소 `CURTAINCALL_PaidAssets`에만 둔다.
- 이 비공개 저장소는 게임 프로젝트 안의 `Assets/_ThirdParty` 폴더로 연결되어 있다(서브모듈). 겉보기엔 그냥 폴더지만 **저장(커밋)하는 곳이 따로**다.
- 그래서 저장을 **두 번** 한다. ① 비공개 저장소에 에셋 저장 → ② 게임 저장소에 "에셋 저장소가 바뀌었다"는 표시 저장. **순서를 바꾸면 올리기가 거절된다.**
- 공개 저장소(`POC_CURTAINCALL`)에 외부 에셋이 들어가면 자동 검사가 막는다. 라이선스 위반이 되기 때문이다.

## 누가 하나

- PM(담당자 확대는 검토 중). `CURTAINCALL_PaidAssets` **쓰기 권한**이 필요하다(팀원은 읽기 권한).
- 에셋을 **구매한 Unity 계정**으로 로그인한 사람이 넣는다. 다른 팀원은 저장소로 받으므로 따로 살 필요가 없다(아래 "라이선스" 예외 확인).

## 준비물 (처음 한 번)

1. GitHub Desktop 설치 후 GitHub 계정으로 로그인.
2. 게임 프로젝트가 GitHub Desktop에 등록되어 있어야 한다(`File > Add local repository` → 프로젝트 폴더).
3. 에셋 저장소도 따로 등록한다: `File > Add local repository` → `<프로젝트 폴더>\Assets\_ThirdParty` 선택.
   - 이제 왼쪽 위 **Current repository** 목록에 `POC_CURTAINCALL`과 `CURTAINCALL_PaidAssets` 두 개가 보인다.
   - `_ThirdParty` 폴더가 비어 있으면 PM에게 읽기 권한을 받은 뒤 AI에게 "시작해줘"라고 한다.

## 넣기 전 확인

| 확인 | 방법 |
|---|---|
| 라이선스 종류 | Asset Store 에셋 페이지의 **License type**. `Extension Asset`(에디터 도구)은 **쓰는 사람 수만큼 구매**가 필요할 수 있다. 모델·텍스처 같은 아트(`Single Entity`)는 팀 공유 가능 |
| 렌더 파이프라인 | 이 프로젝트는 **URP**. 페이지의 "Render pipeline compatibility"에 URP가 있는지 본다 |
| 용량 | 수 GB 단위면 PM과 먼저 상의(저장소 대용량 파일 용량 한도) |
| 이미 있는지 | 비공개 저장소 README의 "포함 목록" 표 |

## 순서

### 1. 게임 저장소: 작업 브랜치 만들기

1. Unity를 **닫는다**.
2. GitHub Desktop에서 Current repository = `POC_CURTAINCALL`.
3. Current branch → `dev` 선택 → **Fetch origin** → **Pull origin**.
4. Current branch → **New branch** → 이름 `resource/third-party/<에셋이름>`(예: `resource/third-party/stage-props`), "Create branch based on" = `dev`.

### 2. 에셋 저장소: 최신으로 맞추기

1. Current repository = `CURTAINCALL_PaidAssets`.
2. Current branch가 **Detached HEAD**로 보이면 `main`을 선택한다(정상이다. 평소엔 "특정 시점"에 고정되어 있다).
3. **Fetch origin** → **Pull origin**. 변경 목록(Changes)이 **비어 있어야** 한다. 비어 있지 않으면 멈추고 PM에게 알린다.

### 3. Unity에서 받기(import)

1. Unity를 연다. 오른쪽 위 계정이 **구매한 계정**인지 확인.
2. `Window > Package Manager` → 왼쪽 **My Assets** → 에셋 선택 → **Download** → **Import**.
3. 가져오기 창에서:
   - 데모·예제(`Demo`, `Example`, `Sample` 폴더)는 필요 없으면 체크 해제.
   - "Project Settings를 덮어쓴다"는 경고가 나오면 **가져오지 않고** PM과 상의(입력·태그·레이어가 바뀐다).
4. Import 후 `Console`에 빨간 오류가 없는지 본다.

### 4. `_ThirdParty`로 옮기기 (가장 중요)

1. Project 창에서 새로 생긴 폴더를 찾는다. 보통 `Assets/<퍼블리셔 또는 에셋 이름>/`에 생긴다.
2. `Assets/_ThirdParty/` 아래에 **퍼블리셔 폴더**를 만들고(우클릭 `Create > Folder`), 에셋 폴더를 그 안으로 **Unity Project 창에서 드래그**한다.
   - 결과 모양: `Assets/_ThirdParty/<퍼블리셔>/<에셋>/` (예: `_ThirdParty/Demigiant/DOTween/`)
   - **Windows 탐색기로 옮기지 않는다.** Unity 안에서 옮겨야 연결(`.meta`)이 유지되어 다른 씬·프리팹이 깨지지 않는다.
3. 원래 자리에 빈 폴더가 남으면 Unity에서 삭제한다.
4. 분홍색(마젠타) 재질이 보이면: `Window > Rendering > Render Pipeline Converter` → `Built-in to URP` → **Material Upgrade**만 체크 → Initialize And Convert.
5. (선택) 에셋 폴더에 `.note` 파일을 만들어 첫 줄에 비고를 적는다. 예: `유료, seat 3` / `URP 변환함`.
6. 옮긴 뒤 다시 `Console` 오류 확인. 경로가 고정된 에디터 도구가 가끔 오류를 낸다 → PM에게 알린다.
7. `File > Save Project`.

### 5. 에셋 저장소에 저장·올리기 (①)

1. GitHub Desktop, Current repository = `CURTAINCALL_PaidAssets`, branch = `main`.
2. Changes에 새 에셋 파일이 모두 보이는지 확인(`.meta` 포함). **`_ThirdParty` 밖 파일은 여기 보이지 않는 것이 정상.**
3. 아래 Summary에 `add: <에셋이름> <버전>` → **Commit to main** → **Push origin**.
   - 큰 파일(모델·이미지·소리·.dll)은 저장소 설정(`.gitattributes`)에 따라 **자동으로 LFS**로 올라간다. 따로 할 일은 없다. "Git LFS를 초기화할까요?" 창이 뜨면 **Initialize Git LFS**.
   - 파일이 수천 개면 Commit·Push에 수 분~수십 분 걸린다. 창을 닫지 말고 기다린다.
4. 몇 분 뒤 GitHub의 저장소 README 표에 에셋이 자동으로 추가된다(직접 고치지 않는다).

### 6. 게임 저장소에 표시 저장·올리기 (②)

1. Current repository = `POC_CURTAINCALL`, branch = 1에서 만든 브랜치.
2. Changes에 **`Assets/_ThirdParty` 한 줄만** 있어야 한다(서브모듈 변경으로 표시됨).
   - 다른 파일(`Assets/<에셋이름>/`, `Packages/manifest.json`, `ProjectSettings/` 등)이 보이면 커밋하지 말고 PM에게 알린다. 4번 이동이 빠졌거나 에셋이 설정을 바꾼 것이다.
3. Summary `chore: 외부 에셋 <에셋이름> 추가` → **Commit** → **Publish branch**(또는 Push origin).
4. **Create Pull Request** → 받는 브랜치(base)를 **`dev`**로 → PR 등록. 자동 검사(`verify-third-party`)가 통과하면 PM이 병합한다.

### 7. 팀원이 받기

- `dev`에 병합된 뒤 각자 AI에게 "시작해줘"(또는 GitHub Desktop에서 Pull). `_ThirdParty`가 자동으로 새 에셋까지 받아진다.

## 이럴 땐 이렇게

| 상황 | 할 일 |
|---|---|
| ②를 Push할 때 "포인터가 private 레포 main에 없음" | ①의 Push를 빠뜨림. 5번부터 다시 |
| PR 검사 "허용되지 않은 Assets 최상위 경로" | 에셋이 `_ThirdParty` 밖에 있음. 4번대로 Unity에서 옮긴 뒤 다시 커밋 |
| PR 검사 "외부 라이브러리·패키지 파일(.dll 등)" | 같은 원인. `_ThirdParty` 안으로 옮긴다 |
| 에셋 저장소 Changes에 모르는 변경이 있음 | 커밋하지 말고 PM에게. 다른 사람 작업일 수 있다 |
| 에셋을 업데이트(새 버전)하고 싶음 | 같은 순서. Import 창에서 이전 위치(`_ThirdParty/...`)로 덮이는지 확인. 새 폴더가 생기면 4번처럼 옮긴다 |
| 에셋을 지우고 싶음 | 씬·프리팹이 쓰고 있을 수 있다. PM과 먼저 상의 |
| 팀원에게 `_ThirdParty`가 비어 있음 | PM에게 `CURTAINCALL_PaidAssets` 읽기 권한 요청 → "시작해줘" |

## 하지 말 것

- 에셋을 `Assets/Resources/`나 `Assets/` 바로 아래에 두고 게임 저장소에 커밋.
- `.unitypackage` 파일 자체를 저장소에 올리기(풀어서 넣은 결과만 저장).
- 에셋 저장소 내용을 공개 저장소·메신저·외부 드라이브에 복사.
- ②를 ①보다 먼저 올리기.
