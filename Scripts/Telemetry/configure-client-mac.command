#!/bin/zsh
# Launch Unity with private local settings; never put the token in arguments/history.
set -eu
if [[ "$(uname -s)" != Darwin ]]; then
  print '이 파일은 Mac 전용입니다.'; exit 1
fi
projectDirectory="${0:A:h:h:h}"
editorVersion="$(awk '/m_EditorVersion:/ {print $2; exit}' "$projectDirectory/ProjectSettings/ProjectVersion.txt")"
editorExecutable="/Applications/Unity/Hub/Editor/$editorVersion/Unity.app/Contents/MacOS/Unity"
if [[ ! -x "$editorExecutable" ]]; then
  print "Unity Hub에서 프로젝트 버전 $editorVersion 에디터를 먼저 설치해 주세요."; exit 1
fi
print '이 프로젝트의 Unity를 먼저 종료했는지 확인해 주세요.'
read -r 'ready?종료했다면 Enter를 눌러 주세요: '
umask 077
privateDirectory="$projectDirectory/.harness/local/telemetry-mac"
mkdir -p "$privateDirectory"
chmod 700 "$privateDirectory"
tokenFile="$privateDirectory/token"
trap 'unset clientToken CURTAINCALL_TELEMETRY_TOKEN' EXIT
if [[ -f "$tokenFile" ]]; then
  clientToken="$(<"$tokenFile")"
else
  read -rs 'clientToken?관리자가 전달한 로그 전송 설정값을 붙여 넣고 Enter (표시 안 됨): '
  print ''
fi
if (( ${#clientToken} < 32 )); then print '설정값이 너무 짧습니다. 관리자에게 확인해 주세요.'; exit 1; fi
printf '%s' "$clientToken" > "$tokenFile"
chmod 600 "$tokenFile"
export CURTAINCALL_TELEMETRY_URL='https://script.google.com/macros/s/AKfycbyBQxQU6XRZXS1qPEs11K8U_i8cwkJEi68-yHgIL6XHe9yCYzyXf3MFjsw6m2cYKEKD3A/exec'
export CURTAINCALL_TELEMETRY_TOKEN="$clientToken"
"$editorExecutable" -projectPath "$projectDirectory" >/dev/null 2>&1 &!
print '로그 전송 설정과 함께 Unity를 열었습니다. 로그 수집 체크는 Unity 메뉴에서 따로 켜 주세요.'
print '다음에도 이 파일로 Unity를 여세요. 일반 플레이어는 이 설정이 필요 없습니다.'
