@echo off
REM Builds a single-file WebToAppStudio.exe into .\publish  (needs the .NET 9 SDK)
dotnet publish WebToAppStudio.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
echo.
echo Done: publish\WebToAppStudio.exe
pause
