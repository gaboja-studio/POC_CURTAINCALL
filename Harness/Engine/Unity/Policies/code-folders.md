# Code Folders

## 배치

| 종류 | 위치 |
|---|---|
| C# 기능 코드 | Assets/Scripts/<Feature>/ |
| 씬 | Assets/Scenes/ |
| 선택 테스트 씬·프리팹 | Assets/Scenes/Tests/<Feature>/ 와 Prefabs/ |
| 외부 에셋 | Assets/_ThirdParty/ (private submodule) |

- 담당자가 필요한 폴더를 만들고 수정한다. PM 사전 생성·구역 배정은 없다.
- 기능별 폴더는 찾기 쉽게 나누는 기준이며 수정 권한 경계가 아니다.
- 다른 기능과 공동 시스템의 코드 읽기·공개 기능 사용·참조·연결은 자유롭다. 목적에 필요한 관련 코드도 고칠 수 있다.
- 연결 Task·공용 파일 소유 표·setup 허용 경로는 요구하지 않는다. 동시에 바꾸는 파일은 담당자와 겹침을 알릴 내용을 기록한다.
- 기존 공개 진입점을 활용하고 불필요하게 내부 구현에 의존하지 않는다.
- Unity 폴더명은 PascalCase. 에셋·폴더 생성·이동은 Unity에서 하고 `.meta`/GUID를 보존한다.
