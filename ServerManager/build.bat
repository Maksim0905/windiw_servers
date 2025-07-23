@echo off
echo Building Server Manager for Windows...

REM Очистка предыдущих сборок
if exist "bin\" rmdir /s /q bin
if exist "obj\" rmdir /s /q obj

REM Восстановление пакетов
echo Restoring packages...
dotnet restore

REM Сборка проекта
echo Building project...
dotnet build --configuration Release

REM Публикация как single-file executable
echo Publishing as single-file executable...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ./publish/

REM Копирование конфигурации
copy appsettings.json .\publish\

echo.
echo Build completed! Executable file: .\publish\ServerManager.exe
echo.
echo To run on Windows Server:
echo ServerManager.exe
echo.
echo API will be available at: http://server-ip:8080
echo Swagger documentation: http://server-ip:8080/swagger

pause