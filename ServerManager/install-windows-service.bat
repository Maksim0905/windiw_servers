@echo off
echo Installing Server Manager as Windows Service...

REM Проверка прав администратора
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script requires administrator privileges.
    echo Please run as Administrator.
    pause
    exit /b 1
)

set SERVICE_NAME=ServerManager
set SERVICE_DISPLAY_NAME=Server Manager API
set SERVICE_DESCRIPTION=API for managing Windows servers and game backends
set INSTALL_DIR=C:\ServerManager
set EXECUTABLE_PATH=%INSTALL_DIR%\ServerManager.exe

echo Checking if service already exists...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Service already exists. Stopping and removing...
    sc stop "%SERVICE_NAME%"
    timeout /t 5 /nobreak >nul
    sc delete "%SERVICE_NAME%"
    timeout /t 3 /nobreak >nul
)

echo Creating installation directory...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%INSTALL_DIR%\logs" mkdir "%INSTALL_DIR%\logs"

echo Copying files...
if not exist ".\publish\ServerManager.exe" (
    echo Error: ServerManager.exe not found in .\publish\
    echo Please run build.bat first.
    pause
    exit /b 1
)

copy ".\publish\*" "%INSTALL_DIR%\" >nul
if %errorlevel% neq 0 (
    echo Error copying files.
    pause
    exit /b 1
)

echo Creating Windows Service...
sc create "%SERVICE_NAME%" binPath= "\"%EXECUTABLE_PATH%\"" DisplayName= "%SERVICE_DISPLAY_NAME%" start= auto
if %errorlevel% neq 0 (
    echo Error creating service.
    pause
    exit /b 1
)

echo Setting service description...
sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%"

echo Setting service recovery options...
sc failure "%SERVICE_NAME%" reset= 86400 actions= restart/5000/restart/5000/restart/5000

echo Starting service...
sc start "%SERVICE_NAME%"

echo Waiting for service to start...
timeout /t 5 /nobreak >nul

echo Checking service status...
sc query "%SERVICE_NAME%"

echo.
echo Installation completed!
echo.
echo Service Name: %SERVICE_NAME%
echo Install Directory: %INSTALL_DIR%
echo.
echo Service Management Commands:
echo   net start %SERVICE_NAME%      - Start service
echo   net stop %SERVICE_NAME%       - Stop service
echo   sc delete %SERVICE_NAME%      - Remove service
echo.
echo API will be available at: http://localhost:8080
echo Swagger documentation: http://localhost:8080/swagger
echo.
echo Check logs at: %INSTALL_DIR%\logs\
echo.

pause