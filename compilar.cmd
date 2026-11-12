@echo off
setlocal
pushd "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:MotaPantalla.exe /win32manifest:MotaPantalla.manifest /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll MotaPantalla.cs
set "result=%errorlevel%"
popd
exit /b %result%
