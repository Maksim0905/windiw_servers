#!/bin/bash

echo "Building Server Manager..."

# Очистка предыдущих сборок
rm -rf bin/
rm -rf obj/

# Восстановление пакетов
dotnet restore

# Сборка проекта
dotnet build --configuration Release

# Публикация как single-file executable
echo "Publishing as single-file executable..."
dotnet publish --configuration Release --runtime linux-x64 --self-contained true --output ./publish/

# Копирование конфигурации
cp appsettings.json ./publish/

# Установка прав на выполнение
chmod +x ./publish/ServerManager

echo "Build completed! Executable file: ./publish/ServerManager"
echo ""
echo "To run on server:"
echo "./ServerManager"
echo ""
echo "API will be available at: http://server-ip:8080"
echo "Swagger documentation: http://server-ip:8080/swagger"