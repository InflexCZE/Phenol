@echo off
msbuild ReleaseBuild.csproj -target:ReleaseBuild

set /p version="Enter new version (v1.0.0):"
if not []==[%version%] (
git tag %version%
git push --tags
)
pause