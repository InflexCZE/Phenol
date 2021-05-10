@echo off
msbuild ReleaseBuild.csproj -target:ReleaseBuild

set /p version="Enter new version (v1.0.0):"
if not []==[%version%] (
git tag %version%
git push --tags
)

rem clear version variable, it confuses nuget in subsequent runs
set "version="
pause