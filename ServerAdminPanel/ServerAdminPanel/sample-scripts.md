# 📜 Примеры скриптов для Server Admin Panel

## 🔧 Системное администрирование

### Проверка состояния сервера (PowerShell)
```powershell
Write-Host "=== Информация о сервере ===" -ForegroundColor Green

# Основная информация
$computerInfo = Get-ComputerInfo
Write-Host "Имя компьютера: $($computerInfo.CsName)"
Write-Host "ОС: $($computerInfo.WindowsProductName) $($computerInfo.WindowsVersion)"
Write-Host "Время запуска: $($computerInfo.CsBootupState)"

# Память
$totalRAM = [math]::Round($computerInfo.TotalPhysicalMemory / 1GB, 2)
$freeRAM = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2)
Write-Host "Общая память: $totalRAM GB"
Write-Host "Свободная память: $freeRAM GB"

# Диски
Write-Host "`n=== Диски ===" -ForegroundColor Green
Get-CimInstance -ClassName Win32_LogicalDisk | Where-Object {$_.DriveType -eq 3} | ForEach-Object {
    $size = [math]::Round($_.Size / 1GB, 2)
    $free = [math]::Round($_.FreeSpace / 1GB, 2)
    $used = $size - $free
    $percent = [math]::Round(($used / $size) * 100, 1)
    Write-Host "Диск $($_.DeviceID) - Размер: $size GB, Использовано: $used GB ($percent%), Свободно: $free GB"
}

# Сетевые адаптеры
Write-Host "`n=== Сетевые адаптеры ===" -ForegroundColor Green
Get-NetAdapter | Where-Object {$_.Status -eq "Up"} | ForEach-Object {
    Write-Host "Адаптер: $($_.Name) - Скорость: $($_.LinkSpeed)"
}
```

### Мониторинг производительности (JavaScript)
```javascript
const os = require('os');
const fs = require('fs');

console.log('=== Мониторинг системы ===');

// Информация о CPU
const cpus = os.cpus();
console.log(`CPU: ${cpus[0].model}`);
console.log(`Количество ядер: ${cpus.length}`);

// Загрузка системы
const loadAvg = os.loadavg();
console.log(`Средняя загрузка: 1 мин: ${loadAvg[0].toFixed(2)}, 5 мин: ${loadAvg[1].toFixed(2)}, 15 мин: ${loadAvg[2].toFixed(2)}`);

// Память
const totalMem = os.totalmem();
const freeMem = os.freemem();
const usedMem = totalMem - freeMem;
const memUsagePercent = ((usedMem / totalMem) * 100).toFixed(2);

console.log(`\n=== Память ===`);
console.log(`Общая: ${(totalMem / 1024 / 1024 / 1024).toFixed(2)} GB`);
console.log(`Использовано: ${(usedMem / 1024 / 1024 / 1024).toFixed(2)} GB (${memUsagePercent}%)`);
console.log(`Свободно: ${(freeMem / 1024 / 1024 / 1024).toFixed(2)} GB`);

// Время работы
const uptime = os.uptime();
const days = Math.floor(uptime / 86400);
const hours = Math.floor((uptime % 86400) / 3600);
const minutes = Math.floor((uptime % 3600) / 60);
console.log(`\nВремя работы: ${days} дней, ${hours} часов, ${minutes} минут`);

// Сетевые интерфейсы
console.log('\n=== Сетевые интерфейсы ===');
const networkInterfaces = os.networkInterfaces();
for (const [name, interfaces] of Object.entries(networkInterfaces)) {
    interfaces.forEach(iface => {
        if (!iface.internal && iface.family === 'IPv4') {
            console.log(`${name}: ${iface.address}`);
        }
    });
}
```

## 🌐 Настройка веб-сервера

### Установка и настройка IIS (PowerShell)
```powershell
Write-Host "Установка IIS..." -ForegroundColor Green

# Проверяем, установлен ли IIS
$iisFeature = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole

if ($iisFeature.State -eq "Disabled") {
    # Установка IIS
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-CommonHttpFeatures -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpErrors -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpLogging -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-Security -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-RequestFiltering -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-StaticContent -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-DefaultDocument -All
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-DirectoryBrowsing -All
    
    Write-Host "IIS успешно установлен!" -ForegroundColor Green
} else {
    Write-Host "IIS уже установлен." -ForegroundColor Yellow
}

# Проверяем статус IIS
$iisService = Get-Service -Name W3SVC -ErrorAction SilentlyContinue
if ($iisService) {
    Write-Host "Статус службы IIS: $($iisService.Status)" -ForegroundColor Green
    if ($iisService.Status -ne "Running") {
        Start-Service W3SVC
        Write-Host "Служба IIS запущена." -ForegroundColor Green
    }
} else {
    Write-Host "Служба IIS не найдена." -ForegroundColor Red
}

# Создание тестового сайта
$siteName = "TestSite"
$sitePath = "C:\inetpub\wwwroot\testsite"
$port = 8081

if (!(Test-Path $sitePath)) {
    New-Item -ItemType Directory -Path $sitePath -Force
    $indexContent = @"
<!DOCTYPE html>
<html>
<head>
    <title>Test Site</title>
</head>
<body>
    <h1>Тестовый сайт работает!</h1>
    <p>Время: $(Get-Date)</p>
</body>
</html>
"@
    Set-Content -Path "$sitePath\index.html" -Value $indexContent
}

# Создаем сайт в IIS
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Get-Website -Name $siteName -ErrorAction SilentlyContinue) {
    Remove-Website -Name $siteName
}
New-Website -Name $siteName -Port $port -PhysicalPath $sitePath

Write-Host "Тестовый сайт создан: http://localhost:$port" -ForegroundColor Green
```

### Настройка балансировщика нагрузки (PowerShell)
```powershell
Write-Host "Настройка балансировщика нагрузки..." -ForegroundColor Green

# Установка необходимых компонентов
$features = @(
    "IIS-WebServerRole",
    "IIS-WebServer",
    "IIS-ApplicationDevelopment",
    "IIS-ASPNET45"
)

foreach ($feature in $features) {
    $featureState = Get-WindowsOptionalFeature -Online -FeatureName $feature
    if ($featureState.State -eq "Disabled") {
        Write-Host "Устанавливаем $feature..." -ForegroundColor Yellow
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -All
    }
}

# Настройка Application Request Routing
Write-Host "Настраиваем ARR для балансировки..." -ForegroundColor Yellow

# Создаем конфигурацию серверов
$servers = @(
    @{Name="Server1"; Address="192.168.1.10:80"; Weight=50},
    @{Name="Server2"; Address="192.168.1.11:80"; Weight=50}
)

# Создаем файл конфигурации для ARR
$arrConfig = @"
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
    <system.webServer>
        <rewrite>
            <rules>
                <rule name="LoadBalance" stopProcessing="true">
                    <match url=".*" />
                    <action type="Rewrite" url="http://ServerFarm/{R:0}" />
                </rule>
            </rules>
        </rewrite>
    </system.webServer>
</configuration>
"@

$configPath = "C:\inetpub\wwwroot\web.config"
Set-Content -Path $configPath -Value $arrConfig

Write-Host "Балансировщик настроен!" -ForegroundColor Green
Write-Host "Не забудьте установить Application Request Routing из Web Platform Installer" -ForegroundColor Yellow
```

## 🔄 Массовые операции

### Пакет для настройки нового сервера
```powershell
# Задача 1: Обновление системы
Write-Host "=== Обновление системы ===" -ForegroundColor Green
Install-Module PSWindowsUpdate -Force -Confirm:$false
Get-WUInstall -AcceptAll -IgnoreReboot

# Задача 2: Установка базового ПО
Write-Host "=== Установка Chocolatey ===" -ForegroundColor Green
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Базовые программы
choco install -y googlechrome firefox 7zip notepadplusplus git nodejs python3

# Задача 3: Настройка брандмауэра
Write-Host "=== Настройка брандмауэра ===" -ForegroundColor Green
New-NetFirewallRule -DisplayName "Allow HTTP" -Direction Inbound -Protocol TCP -LocalPort 80
New-NetFirewallRule -DisplayName "Allow HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443
New-NetFirewallRule -DisplayName "Allow Admin Panel" -Direction Inbound -Protocol TCP -LocalPort 8080

Write-Host "Настройка сервера завершена!" -ForegroundColor Green
```

### Мониторинг службы (JavaScript)
```javascript
const { exec } = require('child_process');
const fs = require('fs');

// Конфигурация служб для мониторинга
const services = [
    'W3SVC',      // IIS
    'MSSQLSERVER', // SQL Server
    'Spooler',    // Диспетчер печати
    'Themes'      // Темы
];

console.log('=== Мониторинг служб ===');

services.forEach(serviceName => {
    exec(`sc query ${serviceName}`, (error, stdout, stderr) => {
        if (error) {
            console.log(`❌ Служба ${serviceName}: ОШИБКА - ${error.message}`);
            return;
        }
        
        if (stdout.includes('RUNNING')) {
            console.log(`✅ Служба ${serviceName}: РАБОТАЕТ`);
        } else if (stdout.includes('STOPPED')) {
            console.log(`⭕ Служба ${serviceName}: ОСТАНОВЛЕНА`);
        } else {
            console.log(`⚠️  Служба ${serviceName}: НЕИЗВЕСТНОЕ СОСТОЯНИЕ`);
        }
    });
});

// Проверка свободного места на дисках
exec('wmic logicaldisk get size,freespace,caption', (error, stdout, stderr) => {
    if (!error) {
        console.log('\n=== Свободное место на дисках ===');
        const lines = stdout.trim().split('\n').slice(1);
        lines.forEach(line => {
            const parts = line.trim().split(/\s+/);
            if (parts.length >= 3) {
                const caption = parts[0];
                const freeSpace = parseInt(parts[1]);
                const size = parseInt(parts[2]);
                
                if (freeSpace && size) {
                    const freeGB = (freeSpace / 1024 / 1024 / 1024).toFixed(2);
                    const totalGB = (size / 1024 / 1024 / 1024).toFixed(2);
                    const usedPercent = ((size - freeSpace) / size * 100).toFixed(1);
                    console.log(`Диск ${caption}: ${freeGB} GB свободно из ${totalGB} GB (использовано ${usedPercent}%)`);
                }
            }
        });
    }
});
```

## 🚀 Развертывание приложений

### Развертывание .NET приложения (PowerShell)
```powershell
param(
    [string]$AppName = "MyWebApp",
    [string]$SourcePath = "C:\Deploy\Source",
    [string]$TargetPath = "C:\inetpub\wwwroot\$AppName",
    [string]$BackupPath = "C:\Backup"
)

Write-Host "=== Развертывание $AppName ===" -ForegroundColor Green

# Создание резервной копии
if (Test-Path $TargetPath) {
    $backupFolder = "$BackupPath\$AppName`_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "Создание резервной копии в $backupFolder..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $backupFolder -Force
    Copy-Item -Path "$TargetPath\*" -Destination $backupFolder -Recurse -Force
}

# Остановка пула приложений
$poolName = $AppName
if (Get-IISAppPool -Name $poolName -ErrorAction SilentlyContinue) {
    Write-Host "Остановка пула приложений $poolName..." -ForegroundColor Yellow
    Stop-IISAppPool -Name $poolName
}

# Остановка сайта
if (Get-IISSite -Name $AppName -ErrorAction SilentlyContinue) {
    Write-Host "Остановка сайта $AppName..." -ForegroundColor Yellow
    Stop-IISSite -Name $AppName
}

# Копирование новых файлов
Write-Host "Копирование файлов из $SourcePath..." -ForegroundColor Yellow
if (!(Test-Path $TargetPath)) {
    New-Item -ItemType Directory -Path $TargetPath -Force
}
Copy-Item -Path "$SourcePath\*" -Destination $TargetPath -Recurse -Force

# Запуск пула приложений и сайта
if (Get-IISAppPool -Name $poolName -ErrorAction SilentlyContinue) {
    Write-Host "Запуск пула приложений $poolName..." -ForegroundColor Yellow
    Start-IISAppPool -Name $poolName
}

if (Get-IISSite -Name $AppName -ErrorAction SilentlyContinue) {
    Write-Host "Запуск сайта $AppName..." -ForegroundColor Yellow
    Start-IISSite -Name $AppName
}

Write-Host "Развертывание завершено!" -ForegroundColor Green

# Проверка работоспособности
Start-Sleep -Seconds 5
$testUrl = "http://localhost/$AppName"
try {
    $response = Invoke-WebRequest -Uri $testUrl -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Приложение работает корректно ($testUrl)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Приложение отвечает с кодом $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Ошибка при проверке приложения: $($_.Exception.Message)" -ForegroundColor Red
}
```

## 🔍 Диагностика и устранение неполадок

### Диагностика сети (PowerShell)
```powershell
Write-Host "=== Диагностика сетевых подключений ===" -ForegroundColor Green

# Проверка базовых сетевых настроек
Write-Host "`n--- Сетевые адаптеры ---" -ForegroundColor Yellow
Get-NetAdapter | Format-Table Name, Status, LinkSpeed, MediaType -AutoSize

# Проверка IP конфигурации
Write-Host "`n--- IP конфигурация ---" -ForegroundColor Yellow
Get-NetIPAddress | Where-Object {$_.AddressFamily -eq "IPv4" -and $_.IPAddress -ne "127.0.0.1"} | 
    Format-Table IPAddress, InterfaceAlias, PrefixLength -AutoSize

# Проверка DNS серверов
Write-Host "`n--- DNS серверы ---" -ForegroundColor Yellow
Get-DnsClientServerAddress | Where-Object {$_.AddressFamily -eq 2} | Format-Table InterfaceAlias, ServerAddresses -AutoSize

# Тестирование подключения к ключевым ресурсам
$testHosts = @("8.8.8.8", "google.com", "microsoft.com")
Write-Host "`n--- Тестирование подключений ---" -ForegroundColor Yellow

foreach ($host in $testHosts) {
    try {
        $result = Test-NetConnection -ComputerName $host -CommonTCPPort HTTP -InformationLevel Quiet
        $status = if ($result) { "✅ OK" } else { "❌ FAIL" }
        Write-Host "$host : $status"
    } catch {
        Write-Host "$host : ❌ ERROR - $($_.Exception.Message)"
    }
}

# Проверка портов
Write-Host "`n--- Прослушиваемые порты ---" -ForegroundColor Yellow
Get-NetTCPConnection | Where-Object {$_.State -eq "Listen"} | 
    Select-Object LocalAddress, LocalPort, @{Name="Process";Expression={(Get-Process -Id $_.OwningProcess).ProcessName}} |
    Sort-Object LocalPort | Format-Table -AutoSize
```

Эти скрипты помогут вам эффективно использовать админ панель для управления серверами!