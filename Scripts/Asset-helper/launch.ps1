[CmdletBinding()]
param([string]$ProjectPath=(Join-Path $PSScriptRoot '../..'))
try { & (Join-Path $PSScriptRoot 'start.ps1') -ProjectPath $ProjectPath }
catch {
    $message='에셋 도우미를 열지 못했습니다. Git·PowerShell 설치와 이 폴더의 접근 권한을 확인해 주세요. 다른 도우미 창이 열려 있으면 먼저 닫아 주세요.'
    if ($IsWindows) { Add-Type -AssemblyName PresentationFramework; [void][Windows.MessageBox]::Show($message,'커튼콜 에셋 도우미') }
    elseif ($IsMacOS) { & osascript -e ('display dialog "'+$message+'" buttons {"확인"} default button 1') }
    exit 1
}
