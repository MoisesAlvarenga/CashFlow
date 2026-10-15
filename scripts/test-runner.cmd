@echo off
if not defined TMP set "TMP=%USERPROFILE%\AppData\Local\Temp"
if not defined TEMP set "TEMP=%USERPROFILE%\AppData\Local\Temp"
C:\Progra~1\dotnet\dotnet.exe test tests\CashFlow.UnitTests\CashFlow.UnitTests.csproj --logger trx --collect:"XPlat Code Coverage"
exit /b %ERRORLEVEL%
