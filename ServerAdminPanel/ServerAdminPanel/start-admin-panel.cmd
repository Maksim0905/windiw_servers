@echo off
title Server Admin Panel
echo.
echo ===============================================
echo    🖥️  Server Admin Panel Launcher
echo ===============================================
echo.
echo Checking for administrator privileges...

:: Проверка прав администратора
net session >nul 2>&1
if NOT %errorLevel% == 0 (
    echo.
    echo ❌ ERROR: This script requires administrator privileges!
    echo Please run this script as Administrator.
    echo.
    pause
    exit /b 1
)

echo ✅ Administrator privileges confirmed
echo.

:: Проверка наличия EXE файла
if not exist "ServerAdminPanel.exe" (
    echo ❌ ERROR: ServerAdminPanel.exe not found!
    echo Please make sure the executable is in the same directory.
    echo.
    pause
    exit /b 1
)

echo ✅ ServerAdminPanel.exe found
echo.

:: Проверка открытого порта 8080
echo Checking if port 8080 is available...
netstat -an | findstr ":8080 " >nul 2>&1
if %errorLevel% == 0 (
    echo ⚠️  WARNING: Port 8080 appears to be in use!
    echo Do you want to continue anyway? (Y/N)
    set /p choice=
    if /i not "%choice%"=="Y" (
        echo Operation cancelled.
        pause
        exit /b 1
    )
) else (
    echo ✅ Port 8080 is available
)

echo.
echo Starting Server Admin Panel...
echo.
echo ===============================================
echo    Access the panel at: http://localhost:8080
echo    Press Ctrl+C to stop the server
echo ===============================================
echo.

:: Запуск приложения
ServerAdminPanel.exe

echo.
echo Server Admin Panel stopped.
pause