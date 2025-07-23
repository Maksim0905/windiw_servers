# Server Manager API

Серверная часть для управления удаленными серверами. Этот сервис предоставляет REST API для мониторинга, управления и выполнения команд на удаленных серверах.

## Возможности

- ✅ **CRUD операции с серверами** - создание, чтение, обновление, удаление
- ✅ **Мониторинг статуса** - автоматическая проверка доступности серверов
- ✅ **Системная информация** - CPU, память, диск, время работы
- ✅ **Выполнение команд** - удаленное выполнение команд через SSH
- ✅ **Фильтрация** - поиск серверов по имени, адресу, тегам, статусу
- ✅ **Статистика** - общая статистика по всем серверам
- ✅ **REST API** - полноценный REST API с документацией Swagger

## Требования

- .NET 8.0 Runtime (для запуска)
- .NET 8.0 SDK (для сборки)
- SQLite (встроено)

## Быстрый старт

### 1. Сборка

```bash
chmod +x build.sh
./build.sh
```

### 2. Запуск на сервере

```bash
cd publish
./ServerManager
```

Сервер будет доступен по адресу: `http://0.0.0.0:8080`

### 3. Документация API

После запуска откройте в браузере: `http://your-server-ip:8080/swagger`

## API Endpoints

### Управление серверами

- `GET /api/servers` - Получить список серверов (с фильтрацией)
- `GET /api/servers/{id}` - Получить сервер по ID
- `POST /api/servers` - Создать новый сервер
- `PUT /api/servers/{id}` - Обновить сервер
- `DELETE /api/servers/{id}` - Удалить сервер

### Мониторинг

- `POST /api/servers/{id}/check-status` - Проверить статус сервера
- `POST /api/servers/check-all-status` - Проверить статус всех серверов
- `GET /api/servers/statistics` - Получить статистику серверов

### Выполнение команд

- `POST /api/servers/{id}/execute` - Выполнить команду на сервере

### Служебные

- `GET /` - Информация о сервисе
- `GET /health` - Проверка здоровья сервиса

## Примеры использования

### Создание сервера

```bash
curl -X POST "http://your-server:8080/api/servers" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Web Server 1",
    "address": "192.168.1.100",
    "port": 22,
    "username": "admin",
    "password": "password",
    "description": "Production web server",
    "tags": "production,web,nginx"
  }'
```

### Получение списка серверов с фильтрацией

```bash
# Все серверы
curl "http://your-server:8080/api/servers"

# Фильтрация по статусу
curl "http://your-server:8080/api/servers?statusFilter=Online"

# Фильтрация по тегам
curl "http://your-server:8080/api/servers?tagFilter=production"

# Поиск по имени
curl "http://your-server:8080/api/servers?nameFilter=web"
```

### Выполнение команды

```bash
curl -X POST "http://your-server:8080/api/servers/1/execute" \
  -H "Content-Type: application/json" \
  -d '{"command": "df -h"}'
```

### Получение статистики

```bash
curl "http://your-server:8080/api/servers/statistics"
```

Ответ:
```json
{
  "Total": 10,
  "Online": 8,
  "Offline": 1,
  "Error": 1,
  "Unknown": 0
}
```

## Конфигурация

Настройки находятся в файле `appsettings.json`:

```json
{
  "Urls": "http://0.0.0.0:8080",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=servers.db"
  },
  "MonitoringSettings": {
    "CheckIntervalMinutes": 5,
    "TimeoutSeconds": 10
  }
}
```

## Безопасность

⚠️ **Важно**: Этот сервис пока не имеет аутентификации. В production окружении рекомендуется:

1. Настроить HTTPS
2. Добавить аутентификацию (JWT)
3. Настроить firewall
4. Использовать SSH ключи вместо паролей

## Логирование

Логи сохраняются в папку `logs/` с ротацией по дням.

## Автоматический мониторинг

Сервис автоматически проверяет статус всех серверов каждые 5 минут (настраивается в конфигурации).

## Системные требования сервера

- Linux x64
- 100 MB свободного места
- 512 MB RAM (минимум)
- Открытый порт 8080

## Troubleshooting

### Проблемы с подключением SSH

1. Убедитесь, что SSH сервис запущен на целевом сервере
2. Проверьте правильность учетных данных
3. Убедитесь, что порт SSH доступен

### Проблемы с базой данных

База данных SQLite создается автоматически. Если возникают проблемы:

```bash
rm servers.db  # Удалить и пересоздать БД
./ServerManager
```

### Проблемы с производительностью

Для большого количества серверов увеличьте интервал проверки в конфигурации.