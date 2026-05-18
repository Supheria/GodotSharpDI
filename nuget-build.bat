@echo off
echo =========================================
echo Packing GodotSharpDI NuGet Package
echo =========================================

set PROJECT=GodotSharpDI\GodotSharpDI.csproj
set OUTPUT=.\nuget_output


if exist %OUTPUT% rmdir /s /q %OUTPUT%

echo.
echo Cleaning previous builds...
dotnet clean GodotSharpDI.sln -c Release

echo.
echo Removing bin/obj directories...
for %%p in (GodotSharpDI GodotSharpDI.SourceGenerator GodotSharpDI.CodeFixes GodotSharpDI.Shared GodotSharpDI.Abstractions GodotSharpDI.Runtime) do (
    if exist %%p\bin rmdir /s /q %%p\bin
    if exist %%p\obj rmdir /s /q %%p\obj
)

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
