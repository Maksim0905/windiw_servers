@echo off
setlocal enabledelayedexpansion

echo ===============================================
echo    Remote Server Manager - Клиентская сборка
echo ===============================================
echo.

rem Проверяем наличие .NET 8
echo [1/4] Проверка .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: .NET 8 SDK не найден!
    echo Скачайте и установите .NET 8 SDK с https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

rem Очистка предыдущих сборок
echo [2/4] Очистка предыдущих сборок...
if exist "bin\Release" rmdir /s /q "bin\Release"
if exist "obj" rmdir /s /q "obj"

rem Восстановление пакетов
echo [3/4] Восстановление пакетов NuGet...
dotnet restore
if errorlevel 1 (
    echo ОШИБКА: Не удалось восстановить пакеты!
    pause
    exit /b 1
)

rem Публикация приложения
echo [4/4] Сборка приложения (режим Release)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
if errorlevel 1 (
    echo ОШИБКА: Сборка завершилась с ошибкой!
    pause
    exit /b 1
)

rem Проверяем результат
set "OUTPUT_FILE=bin\Release\net8.0-windows\win-x64\publish\windiw_servers.exe"
if not exist "%OUTPUT_FILE%" (
    echo ОШИБКА: Исполняемый файл не найден!
    echo Ожидаемое расположение: %OUTPUT_FILE%
    pause
    exit /b 1
)

rem Получаем размер файла
for %%A in ("%OUTPUT_FILE%") do set "FILE_SIZE=%%~zA"
set /a "FILE_SIZE_MB=%FILE_SIZE% / 1048576"

echo.
echo ===============================================
echo                СБОРКА ЗАВЕРШЕНА!
echo ===============================================
echo.
echo Файл: %OUTPUT_FILE%
echo Размер: !FILE_SIZE_MB! MB
echo.
echo Это клиентское приложение для управления удаленными серверами.
echo Для работы необходимо:
echo 1. Запустить сервер на удаленной машине
echo 2. Указать IP адрес и порт сервера в приложении
echo 3. Нажать "Подключиться"
echo.
echo Особенности:
echo - Не требует административных прав
echo - Работает без установки
echo - Автоматические обновления через GitHub
echo - Библиотека скриптов в %APPDATA%\RemoteServerManager
echo.

rem Спрашиваем о запуске
choice /c YN /m "Запустить приложение сейчас? (Y/N)"
if errorlevel 2 goto :end
if errorlevel 1 (
    echo Запуск приложения...
    start "" "%OUTPUT_FILE%"
)

:end
echo.
echo Готово! Нажмите любую клавишу для выхода...
pause >nul