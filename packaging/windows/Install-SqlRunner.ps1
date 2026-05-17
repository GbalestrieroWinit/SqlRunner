$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "SqlRunner"
$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $sourceDir "*") -Destination $installDir -Recurse -Force

$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$shortcutPath = Join-Path $desktop "SqlRunner.lnk"
$targetPath = Join-Path $installDir "SqlRunner.exe"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetPath
$shortcut.WorkingDirectory = $installDir
$shortcut.Save()

Write-Host "SqlRunner installed to $installDir"
Write-Host "Desktop shortcut created at $shortcutPath"
