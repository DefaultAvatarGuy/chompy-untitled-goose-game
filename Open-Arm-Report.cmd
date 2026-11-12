@echo off
if not exist "%~dp0..\Chompy-Arms-Diagnostics.txt" (
 echo Start the game, enter the village, wait a couple of seconds, then try again.
 echo If no report is created, send BepInEx\LogOutput.log from the game folder.
 pause
 exit /b 1
)
start "" notepad.exe "%~dp0..\Chompy-Arms-Diagnostics.txt"
