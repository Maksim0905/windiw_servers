#!/bin/bash

# Скрипт установки Server Manager как системного сервиса

SERVICE_NAME="server-manager"
SERVICE_USER="server-manager"
INSTALL_DIR="/opt/server-manager"
SERVICE_FILE="/etc/systemd/system/server-manager.service"

echo "=== Server Manager Installation Script ==="
echo ""

# Проверка прав root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Пожалуйста, запустите скрипт с правами root (sudo ./install.sh)"
    exit 1
fi

echo "✅ Проверка прав root: OK"

# Проверка наличия .NET Runtime
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET Runtime не найден. Устанавливаем..."
    
    # Определяем дистрибутив
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS=$ID
        VER=$VERSION_ID
    fi
    
    case $OS in
        ubuntu|debian)
            apt-get update
            apt-get install -y wget
            wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            apt-get update
            apt-get install -y aspnetcore-runtime-8.0
            ;;
        centos|rhel|fedora)
            dnf install -y wget
            wget https://packages.microsoft.com/config/fedora/36/packages-microsoft-prod.rpm -O packages-microsoft-prod.rpm
            rpm -Uvh packages-microsoft-prod.rpm
            rm packages-microsoft-prod.rpm
            dnf install -y aspnetcore-runtime-8.0
            ;;
        *)
            echo "❌ Неподдерживаемый дистрибутив: $OS"
            echo "Пожалуйста, установите .NET 8.0 Runtime вручную"
            exit 1
            ;;
    esac
fi

echo "✅ .NET Runtime: OK"

# Создание пользователя для сервиса
if ! id "$SERVICE_USER" &>/dev/null; then
    echo "📝 Создаём пользователя $SERVICE_USER..."
    useradd --system --home-dir $INSTALL_DIR --shell /bin/false $SERVICE_USER
fi

echo "✅ Пользователь сервиса: OK"

# Создание директории установки
echo "📁 Создаём директорию $INSTALL_DIR..."
mkdir -p $INSTALL_DIR
mkdir -p $INSTALL_DIR/logs

# Копирование файлов
echo "📦 Копируем файлы приложения..."
if [ -f "./publish/ServerManager" ]; then
    cp ./publish/* $INSTALL_DIR/
    chmod +x $INSTALL_DIR/ServerManager
else
    echo "❌ Файл ./publish/ServerManager не найден!"
    echo "Пожалуйста, сначала выполните сборку: ./build.sh"
    exit 1
fi

# Настройка прав доступа
echo "🔒 Настраиваем права доступа..."
chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR
chmod 755 $INSTALL_DIR
chmod 644 $INSTALL_DIR/appsettings.json

# Создание файла сервиса systemd
echo "⚙️  Создаём системный сервис..."
cat > $SERVICE_FILE << EOF
[Unit]
Description=Server Manager API
After=network.target

[Service]
Type=notify
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/ServerManager
Restart=always
RestartSec=10
SyslogIdentifier=server-manager
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Ограничения безопасности
NoNewPrivileges=yes
PrivateTmp=yes
ProtectSystem=strict
ReadWritePaths=$INSTALL_DIR
ProtectHome=yes

[Install]
WantedBy=multi-user.target
EOF

# Перезагрузка systemd и запуск сервиса
echo "🔄 Перезагружаем systemd..."
systemctl daemon-reload

echo "🚀 Запускаем сервис..."
systemctl enable $SERVICE_NAME
systemctl start $SERVICE_NAME

# Проверка статуса
sleep 3
if systemctl is-active --quiet $SERVICE_NAME; then
    echo ""
    echo "✅ Server Manager успешно установлен и запущен!"
    echo ""
    echo "📊 Статус сервиса:"
    systemctl status $SERVICE_NAME --no-pager
    echo ""
    echo "🌐 Сервис доступен по адресу: http://$(hostname -I | awk '{print $1}'):8080"
    echo "📚 Документация API: http://$(hostname -I | awk '{print $1}'):8080/swagger"
    echo ""
    echo "🔧 Управление сервисом:"
    echo "  sudo systemctl start $SERVICE_NAME     # Запуск"
    echo "  sudo systemctl stop $SERVICE_NAME      # Остановка"
    echo "  sudo systemctl restart $SERVICE_NAME   # Перезапуск"
    echo "  sudo systemctl status $SERVICE_NAME    # Статус"
    echo "  sudo journalctl -u $SERVICE_NAME -f    # Логи"
    echo ""
    echo "📁 Файлы приложения: $INSTALL_DIR"
    echo "📄 Логи: $INSTALL_DIR/logs/"
else
    echo ""
    echo "❌ Ошибка при запуске сервиса!"
    echo "Проверьте логи: sudo journalctl -u $SERVICE_NAME"
    exit 1
fi