@ECHO OFF

REM The following directory is for .NET 4.0
set DOTNETFX2=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319
set PATH=%PATH%;%DOTNETFX2%

echo Installing Ayodia-Smart-CardID Win Service...
echo ---------------------------------------------------
InstallUtil "%~dp0\bin\Debug\Ayodia-Smart-CardID.exe"
echo ---------------------------------------------------
pause
echo Done.