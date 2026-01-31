@echo off
echo =========================================
echo Packing GodotSharp.DI.Generator NuGet Package
echo =========================================

set PROJECT=GodotSharp.DI\GodotSharp.DI.csproj
set OUTPUT=.\nuget_output


if exist %OUTPUT% rmdir /s /q %OUTPUT%

echo.
echo Cleaning previous builds...
dotnet clean %PROJECT%

echo.
echo Packing...
dotnet pack %PROJECT% -c Release -o %OUTPUT% || goto error

echo.
echo =========================================
echo Pack Complete!
echo Output: %OUTPUT%
echo =========================================
goto end

:error
echo.
echo Pack Failed!
pause
exit /b 1

:end
pause
