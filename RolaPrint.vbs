Option Explicit
Dim shell, fso, folder, script, command
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
folder = fso.GetParentFolderName(WScript.ScriptFullName)
script = fso.BuildPath(folder, "RolaPrint.exe")
shell.CurrentDirectory = folder
command = Chr(34) & script & Chr(34)
shell.Run command, 0, False
