# Windows·Mac: 기록 위치와 설문 다시 열기

일상적인 확인은 대시보드에서 합니다. 아래는 오류를 담당자에게 전달하거나 설문을 다시 확인할 때만 필요합니다.
현재 회사 이름은 `DefaultCompany`, 제품 이름은 `POC_CURTAINCALL`입니다. 이름을 바꾸면 폴더도 달라질 수 있습니다.

## Unity 에디터에서 가장 쉽게 초기화하기

Windows·Mac 공통입니다. 기본 플레이어와 MPPM 보조 플레이어의 재생을 모두 멈춥니다.

1. 상단 **CurtainCall → 설문 캐시 초기화**를 엽니다.
2. **기본 플레이어만** 또는 **MPPM 보조 플레이어 포함**을 고릅니다. 두 번째는 기본 플레이어와 이 PC에 저장된 모든 보조 플레이어를 함께 초기화합니다.
3. **초기화**를 누르고 완료 안내를 확인합니다. 재생·컴파일 중에는 메뉴가 비활성화됩니다.
4. 해당 버전이 설문 대상인지 확인하고 **설문 사용** 태그를 켜면 첫날 밤·종료 설문이 다시 열립니다.

설문 열림 기록만 지웁니다. 기존 로그·전송 대기 기록·Google 설문 응답은 유지됩니다.
같은 PC 사용자 저장공간을 공유하는 게임 빌드에도 초기화가 적용됩니다. 아래 폴더 수동 삭제는 에디터 메뉴를 사용할 수 없을 때의 방법입니다.

## Windows에서 기록 폴더 열기

1. 파일 탐색기를 엽니다.
2. 상단 주소칸에 아래 경로를 복사하고 Enter를 누릅니다.

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\POC_CURTAINCALL\TelemetryPipeline
```

## Mac에서 기록 폴더 열기

1. Finder를 엽니다. **⌘+Shift+G**로 ‘폴더로 이동’을 엽니다.
2. Unity 재생으로 확인했다면 다음 경로를 붙여 넣습니다.

```text
~/Library/Application Support/DefaultCompany/POC_CURTAINCALL/TelemetryPipeline
```

3. 게임 `.app`으로 확인했고 위 폴더가 없다면 다음 경로를 확인합니다.

```text
~/Library/Application Support/unity.DefaultCompany.POC_CURTAINCALL/TelemetryPipeline
```

Mac 게임은 기존 Unity용 저장 폴더가 있으면 그 폴더를 사용할 수 있습니다. 폴더가 없다면 로그 태그·실행한 게임 이름부터 확인하세요.
[경로 기준: Unity 6.3 공식 안내](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html).

## 폴더를 보고 알 수 있는 것

파일이 생기거나 수정되면 PC 안에 기록된 것입니다. 이것만으로 Google 저장까지 성공했다고 판단하지 않습니다.
전송 확인은 대시보드 **지금 갱신** 후 수집 건수·세션·행동으로 합니다. 파일이 남았다는 이유로 중복 실패라고 단정하지 마세요.
문제가 생기면 폴더를 보존하고 담당자에게 시작 시각·환경·버전과 함께 알려 주세요. 직접 JSON 파일을 편집할 필요는 없습니다.
같은 PC에서 Unity 가상 플레이어를 여러 개 띄운 경우 `clients/구분값` 아래 각자 기록이 있습니다.

## 이미 열린 설문을 다시 확인하기

Windows·Mac 모두 같은 순서입니다. 다른 게임 기록까지 지우지 않도록 **SurveyCache만** 대상으로 합니다.

1. 게임과 Unity 재생을 종료합니다.
2. 위 기록 폴더 안의 **SurveyCache** 폴더만 지웁니다. PC 전체 캐시나 TelemetryPipeline 전체를 지우지 않습니다.
3. Unity 가상 플레이어는 `clients/구분값/SurveyCache`가 별도입니다. 다시 확인할 플레이어의 폴더를 고릅니다.
4. **0.1.0 설문 켬** 게임으로 다시 첫날 밤에 도착하거나 ESC → 나가기를 확정합니다.
5. 브라우저가 열리는지 확인합니다. 연결 코드는 그대로 두고, 실제 응답을 남길 때만 제출합니다.

캐시를 지우면 두 설문 모두 다시 열릴 수 있습니다. **기존 Google 응답이나 로그는 삭제되지 않습니다.**
게임을 재시작하거나 같은 버전을 다시 빌드한 것만으로 설문 캐시는 초기화되지 않습니다.

## Mac 빌드 담당자 개인 설정

전용 실행 파일은 프로젝트의 `.harness/local/telemetry-mac/token`에 개인 설정을 저장합니다.
Git에는 올라가지 않지만 이 파일을 포함해 프로젝트 폴더 전체를 외부에 공유하지 마세요.
설정값을 바꿔야 할 때는 Unity 종료 후 이 **token 파일만** 지우고 전용 파일을 다시 실행해 새 값을 입력합니다.
일반 플레이어에게는 이 파일을 전달하지 않습니다. 완성된 빌드만 전달합니다.
