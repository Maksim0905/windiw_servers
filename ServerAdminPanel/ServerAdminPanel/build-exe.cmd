@echo off
echo Building Server Admin Panel...

REM Restore packages
echo Restoring NuGet packages...
dotnet restore

REM Build and publish as single EXE file for Windows
echo Building executable...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./bin/Release/win-x64/publish/

echo.
echo Build completed!
echo Executable location: ./bin/Release/win-x64/publish/ServerAdminPanel.exe
echo.
echo To run the admin panel:
echo 1. Copy ServerAdminPanel.exe to your target server
echo 2. Run it as Administrator
echo 3. Open browser and go to http://localhost:8080
echo.
pause