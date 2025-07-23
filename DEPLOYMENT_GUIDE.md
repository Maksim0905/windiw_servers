# 🚀 Руководство по развертыванию Server Manager

Полное руководство по установке и настройке системы управления серверами, состоящей из серверной части (API) и клиентского приложения.

## 📋 Архитектура решения

```
┌─────────────────┐    HTTP/REST API    ┌─────────────────┐
│  Клиентское     │ ◄─────────────────► │  Серверная      │
│  приложение     │                     │  часть          │
│  (Windows)      │                     │  (Linux Server) │
└─────────────────┘                     └─────────────────┘
                                               │
                                               ▼ SSH
                                        ┌─────────────────┐
                                        │  Управляемые    │
                                        │  серверы        │
                                        │  (любые Linux)  │
                                        └─────────────────┘
```

## 🔧 Часть 1: Установка серверной части

### Требования для сервера

- **ОС**: Linux (Ubuntu 20.04+, CentOS 8+, Debian 11+)
- **RAM**: минимум 512 MB, рекомендуется 1 GB
- **Диск**: 100 MB свободного места
- **Сеть**: открытый порт 8080
- **Доступ**: sudo права для установки

### Шаг 1: Подготовка сервера

```bash
# Обновляем систему
sudo apt update && sudo apt upgrade -y

# Устанавливаем git и необходимые пакеты
sudo apt install -y git curl wget
```

### Шаг 2: Клонирование проекта

```bash
# Клонируем репозиторий
git clone <your-repository-url>
cd <repository-name>/ServerManager

# Или скачиваем архив и распаковываем
```

### Шаг 3: Сборка серверной части

```bash
# Делаем скрипт исполняемым
chmod +x build.sh

# Собираем проект
./build.sh
```

### Шаг 4: Установка как системный сервис

```bash
# Делаем скрипт установки исполняемым
chmod +x install.sh

# Устанавливаем (требуются права root)
sudo ./install.sh
```

### Шаг 5: Проверка работы

```bash
# Проверяем статус сервиса
sudo systemctl status server-manager

# Проверяем логи
sudo journalctl -u server-manager -f

# Тестируем API
curl http://localhost:8080/health
```

Если всё прошло успешно, сервер будет доступен по адресу: `http://your-server-ip:8080`

### Настройка firewall (если используется)

```bash
# Ubuntu/Debian (ufw)
sudo ufw allow 8080

# CentOS/RHEL (firewalld)
sudo firewall-cmd --permanent --add-port=8080/tcp
sudo firewall-cmd --reload
```

## 💻 Часть 2: Настройка клиентского приложения

### Требования для клиента

- **ОС**: Windows 10/11
- **NET**: .NET 8.0 Runtime (устанавливается автоматически)
- **Память**: 512 MB RAM
- **Сеть**: доступ к серверу управления

### Шаг 1: Запуск клиентского приложения

1. Скачайте и запустите клиентское приложение
2. При первом запуске откроется диалог подключения к серверу

### Шаг 2: Настройка подключения

1. В поле "URL сервера управления" введите: `http://your-server-ip:8080`
2. Нажмите "Проверить подключение"
3. Если подключение успешно, нажмите "Сохранить"

### Шаг 3: Добавление первого сервера

1. Нажмите кнопку "Добавить сервер"
2. Заполните данные:
   - **Имя**: понятное имя сервера
   - **Адрес**: IP или доменное имя
   - **Порт**: 22 (SSH порт)
   - **Логин/Пароль**: учетные данные для SSH
   - **Теги**: для группировки (например: "production,web")

## 🔐 Настройка безопасности

### SSH ключи (рекомендуется)

Вместо паролей лучше использовать SSH ключи:

```bash
# На сервере управления генерируем ключ
ssh-keygen -t rsa -b 4096 -f /opt/server-manager/ssh-key

# Копируем публичный ключ на управляемые серверы
ssh-copy-id -i /opt/server-manager/ssh-key.pub user@target-server

# В клиентском приложении указываем путь к приватному ключу
# Путь: /opt/server-manager/ssh-key
```

### HTTPS (для production)

```bash
# Получаем SSL сертификат (например, через certbot)
sudo apt install -y certbot
sudo certbot certonly --standalone -d your-domain.com

# Обновляем appsettings.json
sudo nano /opt/server-manager/appsettings.json
```

Изменяем настройки:
```json
{
  "Urls": "https://0.0.0.0:8443",
  "Kestrel": {
    "Certificates": {
      "Default": {
        "Path": "/etc/letsencrypt/live/your-domain.com/fullchain.pem",
        "KeyPath": "/etc/letsencrypt/live/your-domain.com/privkey.pem"
      }
    }
  }
}
```

### Firewall настройки

```bash
# Ограничиваем доступ к API только с определенных IP
sudo ufw allow from YOUR_CLIENT_IP to any port 8080

# Или настраиваем reverse proxy через nginx
sudo apt install -y nginx
```

## 📊 Использование системы

### Основные функции

1. **Управление серверами**:
   - ✅ Добавление/удаление серверов
   - ✅ Редактирование параметров
   - ✅ Группировка по тегам

2. **Мониторинг**:
   - ✅ Автоматическая проверка статуса
   - ✅ Системная информация (CPU, RAM, Disk)
   - ✅ Время работы (uptime)

3. **Выполнение команд**:
   - ✅ Удаленное выполнение команд
   - ✅ Просмотр результатов
   - ✅ История команд

4. **Фильтрация и поиск**:
   - ✅ Поиск по имени
   - ✅ Фильтрация по статусу
   - ✅ Фильтрация по тегам

### Примеры API запросов

```bash
# Получить все серверы
curl "http://your-server:8080/api/servers"

# Добавить новый сервер
curl -X POST "http://your-server:8080/api/servers" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Web Server 1",
    "address": "192.168.1.100",
    "username": "admin",
    "password": "password",
    "tags": "production,web"
  }'

# Выполнить команду
curl -X POST "http://your-server:8080/api/servers/1/execute" \
  -H "Content-Type: application/json" \
  -d '{"command": "df -h"}'

# Получить статистику
curl "http://your-server:8080/api/servers/statistics"
```

## 🚨 Troubleshooting

### Проблемы с серверной частью

1. **Сервис не запускается**:
   ```bash
   sudo journalctl -u server-manager --no-pager
   sudo systemctl restart server-manager
   ```

2. **Порт занят**:
   ```bash
   sudo lsof -i :8080
   sudo netstat -tulpn | grep 8080
   ```

3. **Проблемы с правами**:
   ```bash
   sudo chown -R server-manager:server-manager /opt/server-manager
   sudo chmod +x /opt/server-manager/ServerManager
   ```

### Проблемы с клиентским приложением

1. **Не удается подключиться к серверу**:
   - Проверьте URL (должен начинаться с http:// или https://)
   - Убедитесь, что сервер доступен: `telnet server-ip 8080`
   - Проверьте firewall

2. **Ошибки SSH подключения**:
   - Проверьте учетные данные
   - Убедитесь, что SSH сервис запущен на целевом сервере
   - Проверьте SSH порт

### Проблемы с производительностью

1. **Медленная работа**:
   - Увеличьте интервал проверки в настройках
   - Ограничьте количество одновременно мониторимых серверов

2. **Высокая нагрузка на сервер**:
   ```bash
   # Изменяем настройки в appsettings.json
   "MonitoringSettings": {
     "CheckIntervalMinutes": 10,  # увеличиваем интервал
     "TimeoutSeconds": 30         # увеличиваем таймаут
   }
   ```

## 🔄 Обновление системы

### Обновление серверной части

```bash
cd /path/to/source/ServerManager
git pull origin main
./build.sh
sudo systemctl stop server-manager
sudo cp ./publish/* /opt/server-manager/
sudo systemctl start server-manager
```

### Обновление клиентского приложения

1. Скачайте новую версию
2. Закройте старое приложение
3. Запустите новое (настройки сохранятся)

## 📞 Поддержка

При возникновении проблем:

1. Проверьте логи сервера: `sudo journalctl -u server-manager -f`
2. Проверьте статус API: `curl http://your-server:8080/health`
3. Проверьте документацию API: `http://your-server:8080/swagger`

## 🎯 Дальнейшее развитие

Планируемые улучшения:
- 🔐 Аутентификация и авторизация
- 📈 Расширенная аналитика
- 📱 Web интерфейс
- 🔔 Уведомления и алерты
- 📊 Графики мониторинга
- 🔄 Автоматическое обновление