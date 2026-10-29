Option Explicit
Dim shell, fso, root, script, paths, candidate, pwsh, command
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetParentFolderName(WScript.ScriptFullName)
script = fso.BuildPath(root, "Scripts\Asset-helper\launch.ps1")
pwsh = ""
paths = Array(shell.ExpandEnvironmentStrings("%ProgramFiles%\PowerShell\7\pwsh.exe"), shell.ExpandEnvironmentStrings("%LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe"))
For Each candidate In paths
  If fso.FileExists(candidate) Then pwsh = candidate : Exit For
Next
If pwsh = "" Then
  For Each candidate In Split(shell.ExpandEnvironmentStrings("%PATH%"), ";")
    If Len(candidate) > 0 Then
      If fso.FileExists(fso.BuildPath(candidate, "pwsh.exe")) Then pwsh = fso.BuildPath(candidate, "pwsh.exe") : Exit For
    End If
  Next
End If
If pwsh = "" Then
  MsgBox "PowerShell 7 is required. Open the installer page, install it, and double-click this helper again. No commands are needed.", 64, "CURTAINCALL Asset Helper"
  shell.Run "https://aka.ms/powershell-release?tag=stable", 1, False
  WScript.Quit 1
End If
command = """" & pwsh & """ -NoProfile -WindowStyle Hidden -File """ & script & """ -ProjectPath """ & root & """"
shell.CurrentDirectory = root
shell.Run command, 0, False
