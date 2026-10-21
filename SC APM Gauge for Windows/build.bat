@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] .NET Framework 4.x compiler ^(csc.exe^) not found.
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /utf8output ^
    /out:APM.exe ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    APM.cs

if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo [OK] APM.exe built. Double-click APM.exe to run.
pause
