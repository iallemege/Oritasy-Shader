@echo off
setlocal EnableExtensions
set "GAME=d:\Steam\steamapps\common\Nuclear Option"
set MANAGED=%GAME%\NuclearOption_Data\Managed
set BEP=%GAME%\BepInEx\core
set PLUGINS=%GAME%\BepInEx\plugins
set VER=0.1.0
set OUT=%~dp0OritasyShader.dll
set NAMED=OritasyShader_%VER%.dll
set CSC=%~dp0..\_csc9.cmd

if not exist "%CSC%" (
  echo csc.exe not found
  exit /b 1
)

call "%CSC%" /noconfig /nostdlib /nologo /optimize+ /target:library /platform:anycpu /langversion:10 ^
  /out:"%OUT%" ^
  /r:"%MANAGED%\mscorlib.dll" ^
  /r:"%MANAGED%\netstandard.dll" ^
  /r:"%MANAGED%\System.dll" ^
  /r:"%MANAGED%\System.Core.dll" ^
  /r:"%BEP%\BepInEx.dll" ^
  /r:"%BEP%\0Harmony.dll" ^
  /r:"%MANAGED%\Assembly-CSharp.dll" ^
  /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
  /r:"%MANAGED%\UnityEngine.dll" ^
  /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
  /r:"%MANAGED%\UnityEngine.UIModule.dll" ^
  /r:"%MANAGED%\UnityEngine.InputLegacyModule.dll" ^
  /r:"%MANAGED%\UnityEngine.PhysicsModule.dll" ^
  /r:"%MANAGED%\UnityEngine.TextRenderingModule.dll" ^
  /r:"%MANAGED%\UnityEngine.AssetBundleModule.dll" ^
  /r:"%MANAGED%\Mirage.dll" ^
  /r:"%MANAGED%\Unity.RenderPipelines.Core.Runtime.dll" ^
  /r:"%MANAGED%\Unity.RenderPipelines.Universal.Runtime.dll" ^
  "%~dp0src\GlobalUsings.cs" ^
  "%~dp0src\Opt.cs" ^
  "%~dp0src\Clock.cs" ^
  "%~dp0src\Sky.cs" ^
  "%~dp0src\Hide.cs" ^
  "%~dp0src\Menu.cs" ^
  "%~dp0src\Grade.cs" ^
  "%~dp0src\Plugin.cs"

if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)

for %%F in ("%PLUGINS%\OritasyShader*.dll") do del /f /q "%%F" 2>nul
copy /Y "%OUT%" "%PLUGINS%\%NAMED%"
if errorlevel 1 (
  echo INSTALL FAILED - close the game and rebuild
  exit /b 1
)
if not exist "%PLUGINS%\OritasyShader" mkdir "%PLUGINS%\OritasyShader"
copy /Y "%~dp0fx.bundle" "%PLUGINS%\OritasyShader\fx.bundle"
echo Installed: %PLUGINS%\%NAMED%
endlocal
