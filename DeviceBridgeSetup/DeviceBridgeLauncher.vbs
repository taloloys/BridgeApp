Set objShell = CreateObject("WScript.Shell")
Set objFSO = CreateObject("Scripting.FileSystemObject")

' Get the directory where this script is located
strScriptPath = objFSO.GetParentFolderName(WScript.ScriptFullName)
strExePath = strScriptPath & "\DeviceBridge.exe"

' Check if the exe exists
If objFSO.FileExists(strExePath) Then
    ' Run the application with administrator privileges using PowerShell
    strCommand = "Start-Process -FilePath """ & strExePath & """ -Verb RunAs"
    objShell.Run "powershell.exe -Command """ & strCommand & """", 0, False
Else
    MsgBox "DeviceBridge.exe not found in: " & strScriptPath, vbCritical, "Device Bridge Error"
End If
