# Server Manager для Windows Server

Система управления Windows серверами с игровыми бэкэндами на C#.

## Быстрый старт

### 1. Сборка
```cmd
build.bat
```

### 2. Установка как Windows Service
```cmd
# От имени администратора
install-windows-service.bat
```

### 3. Проверка
- Откройте http://localhost:8080
- Проверьте сервис в Services.msc

## Настройка серверов

### Включите WinRM на каждом сервере:
```powershell
Enable-PSRemoting -Force
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "192.168.*" -Force
Restart-Service WinRM
```

### Создайте пользователя:
```powershell
$Password = ConvertTo-SecureString "YourPassword123!" -AsPlainText -Force
New-LocalUser -Name "ServerManager" -Password $Password
Add-LocalGroupMember -Group "Administrators" -Member "ServerManager"
```

## API Examples

### Добавить сервер:
```bash
curl -X POST "http://localhost:8080/api/windowsservers" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Game Server 1",
    "address": "192.168.1.100",
    "username": "ServerManager", 
    "password": "YourPassword123!",
    "tags": "game,backend"
  }'
```

### Управление сервисами:
```bash
# Получить сервисы
curl "http://localhost:8080/api/windowsservers/1/services"

# Перезапустить сервис
curl -X POST "http://localhost:8080/api/windowsservers/1/services/YourGameService/restart"
```

### Выполнить команду:
```bash
curl -X POST "http://localhost:8080/api/windowsservers/1/execute" \
  -H "Content-Type: application/json" \
  -d '{"command": "Get-Process | Where-Object {$_.Name -like \"*Game*\"}"}'
```

## Возможности

- ✅ WMI мониторинг (CPU, RAM, диск)
- ✅ PowerShell Remoting
- ✅ Управление Windows сервисами
- ✅ Автоматический мониторинг
- ✅ REST API с Swagger документацией
- ✅ Фильтрация и теги

Документация: http://localhost:8080/swagger