# Run this yourself in a visible PowerShell terminal. Never pass the token as a command argument.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Endpoint)
$ErrorActionPreference = 'Stop'
$targetUri = [uri]$Endpoint
if ($targetUri.Scheme -ne 'https' -or $targetUri.Host -ne 'script.google.com' -or
    $targetUri.AbsolutePath -notmatch '^/macros/s/[A-Za-z0-9_-]+/exec$' -or $targetUri.Query) {
    throw 'Google Apps Script 수신 웹 앱의 /exec 주소를 입력하세요.'
}
$secureToken = Read-Host 'Apps Script INGEST_TOKEN (화면에 표시되지 않음)' -AsSecureString
$tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
try {
    $clientToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer)
    if ($clientToken.Length -lt 32) { throw '토큰은 32자 이상이어야 합니다.' }
    [Environment]::SetEnvironmentVariable('CURTAINCALL_TELEMETRY_URL', $Endpoint, 'User')
    [Environment]::SetEnvironmentVariable('CURTAINCALL_TELEMETRY_TOKEN', $clientToken, 'User')
    Write-Output '현재 Windows 사용자 환경에 설정했습니다. Unity Play 또는 게임을 다시 시작하세요.'
    Write-Output '토큰은 사용자 환경 변수에 저장됩니다. 이 PC 계정에 접근하는 프로세스는 읽을 수 있습니다.'
} finally {
    $clientToken = $null
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
    $secureToken.Dispose()
}
