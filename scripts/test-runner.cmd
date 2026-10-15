@echo off
if not defined TMP set "TMP=%USERPROFILE%\AppData\Local\Temp"
if not defined TEMP set "TEMP=%USERPROFILE%\AppData\Local\Temp"
"C:\Program Files\dotnet\dotnet.exe" test --logger trx
exit /b %ERRORLEVEL%
