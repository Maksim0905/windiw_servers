using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace windiw_servers
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timeTimer;
        private readonly HttpClient _httpClient;
        private readonly ObservableCollection<ServerInfo> _servers;
        private readonly string _serversConfigPath;
        private readonly string _scriptsPath;
        private readonly UpdateService _updateService;
        private ServerInfo? _currentServer;

        public MainWindow()
        {
            InitializeComponent();
            
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _servers = new ObservableCollection<ServerInfo>();
            _serversConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteServerManager", "servers.json");
            _scriptsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteServerManager", "Scripts");
            _updateService = new UpdateService();

            // Создание директорий
            Directory.CreateDirectory(Path.GetDirectoryName(_serversConfigPath)!);
            Directory.CreateDirectory(_scriptsPath);

            // Настройка данных
            ServersList.ItemsSource = _servers;

            // Таймер для времени
            _timeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timeTimer.Tick += UpdateTime;
            _timeTimer.Start();

            // Инициализация при загрузке
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            UpdateTime(null, EventArgs.Empty);
            LoadServers();
            LoadScripts();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                
                // Загружаем стартовую страницу
                LoadWelcomePage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации WebView2: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _timeTimer?.Stop();
            _httpClient?.Dispose();
            SaveServers();
        }

        // Управление серверами
        private void LoadServers()
        {
            try
            {
                if (File.Exists(_serversConfigPath))
                {
                    var json = File.ReadAllText(_serversConfigPath);
                    var servers = JsonSerializer.Deserialize<List<ServerInfo>>(json) ?? new List<ServerInfo>();
                    
                    _servers.Clear();
                    foreach (var server in servers)
                    {
                        _servers.Add(server);
                    }
                }
                
                UpdateServerCount();
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Ошибка загрузки серверов: {ex.Message}";
            }
        }

        private void SaveServers()
        {
            try
            {
                var json = JsonSerializer.Serialize(_servers.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_serversConfigPath, json);
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Ошибка сохранения серверов: {ex.Message}";
            }
        }

        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddServerDialog();
            if (dialog.ShowDialog() == true)
            {
                var server = new ServerInfo
                {
                    Name = dialog.ServerName,
                    Address = dialog.ServerAddress,
                    Port = dialog.ServerPort,
                    Description = dialog.ServerDescription,
                    StatusColor = "#e74c3c" // Offline by default
                };
                
                _servers.Add(server);
                SaveServers();
                UpdateServerCount();
                
                StatusBarText.Text = $"Сервер '{server.Name}' добавлен";
            }
        }

        private void ImportServersButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Импорт серверов"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var servers = JsonSerializer.Deserialize<List<ServerInfo>>(json) ?? new List<ServerInfo>();
                    
                    foreach (var server in servers)
                    {
                        if (!_servers.Any(s => s.Address == server.Address && s.Port == server.Port))
                        {
                            _servers.Add(server);
                        }
                    }
                    
                    SaveServers();
                    UpdateServerCount();
                    StatusBarText.Text = $"Импортировано {servers.Count} серверов";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ServersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServersList.SelectedItem is ServerInfo server)
            {
                ServerAddressBox.Text = server.Address;
                ServerPortBox.Text = server.Port.ToString();
                _currentServer = server;
                
                // Автоматическое подключение к выбранному серверу
                await ConnectToServer();
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToServer();
        }

        private async Task ConnectToServer()
        {
            var address = ServerAddressBox.Text.Trim();
            var portText = ServerPortBox.Text.Trim();

            if (string.IsNullOrEmpty(address) || !int.TryParse(portText, out int port))
            {
                MessageBox.Show("Введите корректный адрес и порт сервера", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConnectionStatusText.Text = "Подключение...";
            ConnectionStatus.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12"));

            try
            {
                var url = $"http://{address}:{port}";
                var response = await _httpClient.GetAsync($"{url}/api/server-info");
                
                if (response.IsSuccessStatusCode)
                {
                    // Успешное подключение
                    ConnectionStatusText.Text = "Подключен";
                    ConnectionStatus.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"));
                    
                    // Обновляем статус сервера в списке
                    if (_currentServer != null)
                    {
                        _currentServer.StatusColor = "#27ae60";
                    }
                    
                    // Загружаем админ панель в WebView
                    InitialMessage.Visibility = Visibility.Collapsed;
                    WebView.CoreWebView2.Navigate(url);
                    WebViewUrlBox.Text = url;
                    
                    StatusBarText.Text = $"Подключен к {address}:{port}";
                }
                else
                {
                    throw new Exception($"Сервер вернул код {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = "Ошибка подключения";
                ConnectionStatus.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
                
                if (_currentServer != null)
                {
                    _currentServer.StatusColor = "#e74c3c";
                }
                
                StatusBarText.Text = $"Ошибка подключения: {ex.Message}";
                
                MessageBox.Show($"Не удалось подключиться к серверу:\n{ex.Message}", "Ошибка подключения", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            var address = ServerAddressBox.Text.Trim();
            var portText = ServerPortBox.Text.Trim();

            if (string.IsNullOrEmpty(address) || !int.TryParse(portText, out int port))
            {
                MessageBox.Show("Введите корректный адрес и порт сервера", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var url = $"http://{address}:{port}";
                var response = await _httpClient.GetAsync($"{url}/api/server-info");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var serverInfo = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    var message = $"✅ Сервер доступен!\n\n" +
                                 $"Имя: {serverInfo.GetProperty("serverName").GetString()}\n" +
                                 $"ОС: {serverInfo.GetProperty("os").GetString()}\n" +
                                 $"Процессоры: {serverInfo.GetProperty("processorCount").GetInt32()}\n" +
                                 $"Пользователь: {serverInfo.GetProperty("userName").GetString()}";
                    
                    MessageBox.Show(message, "Тест подключения", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"❌ Сервер недоступен\nКод ответа: {response.StatusCode}", "Тест подключения", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка подключения:\n{ex.Message}", "Тест подключения", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Управление скриптами
        private void LoadScripts()
        {
            try
            {
                ScriptsTree.Items.Clear();
                
                // Создаем базовые папки если их нет
                var jsFolder = Path.Combine(_scriptsPath, "JavaScript");
                var psFolder = Path.Combine(_scriptsPath, "PowerShell");
                var templatesFolder = Path.Combine(_scriptsPath, "Templates");
                
                Directory.CreateDirectory(jsFolder);
                Directory.CreateDirectory(psFolder);
                Directory.CreateDirectory(templatesFolder);
                
                // Создаем примеры скриптов если их нет
                CreateSampleScripts(jsFolder, psFolder, templatesFolder);
                
                // Загружаем структуру папок
                LoadScriptFolder(_scriptsPath, ScriptsTree.Items);
                
                UpdateScriptCount();
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Ошибка загрузки скриптов: {ex.Message}";
            }
        }

        private void LoadScriptFolder(string path, ItemCollection items)
        {
            try
            {
                // Добавляем папки
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var folderItem = new TreeViewItem
                    {
                        Header = $"📁 {Path.GetFileName(dir)}",
                        Tag = dir
                    };
                    
                    LoadScriptFolder(dir, folderItem.Items);
                    items.Add(folderItem);
                }
                
                // Добавляем файлы скриптов
                foreach (var file in Directory.GetFiles(path, "*.js").Concat(Directory.GetFiles(path, "*.ps1")))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    var icon = ext == ".js" ? "📜" : "💠";
                    
                    var fileItem = new TreeViewItem
                    {
                        Header = $"{icon} {Path.GetFileName(file)}",
                        Tag = file
                    };
                    
                    items.Add(fileItem);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки папки {path}: {ex.Message}");
            }
        }

        private void CreateSampleScripts(string jsFolder, string psFolder, string templatesFolder)
        {
            // JavaScript примеры
            if (!File.Exists(Path.Combine(jsFolder, "system_info.js")))
            {
                var jsScript = "// Системная информация\n" +
                               "const os = require('os');\n\n" +
                               "console.log('=== Информация о системе ===');\n" +
                               "console.log('Платформа:', os.platform());\n" +
                               "console.log('Архитектура:', os.arch());\n" +
                               "console.log('Имя хоста:', os.hostname());\n" +
                               "console.log('Процессоров:', os.cpus().length);\n" +
                               "console.log('Общая память:', Math.round(os.totalmem() / 1024 / 1024 / 1024) + ' GB');\n" +
                               "console.log('Свободная память:', Math.round(os.freemem() / 1024 / 1024 / 1024) + ' GB');\n\n" +
                               "const uptime = os.uptime();\n" +
                               "const days = Math.floor(uptime / 86400);\n" +
                               "const hours = Math.floor((uptime % 86400) / 3600);\n" +
                               "const minutes = Math.floor((uptime % 3600) / 60);\n" +
                               "console.log(`Время работы: ${days} дней, ${hours} часов, ${minutes} минут`);";
                
                File.WriteAllText(Path.Combine(jsFolder, "system_info.js"), jsScript);
            }

            // PowerShell примеры
            if (!File.Exists(Path.Combine(psFolder, "disk_check.ps1")))
            {
                var psScript = "# Проверка дискового пространства\n" +
                               "Write-Host \"=== Проверка дисков ===\" -ForegroundColor Green\n\n" +
                               "Get-CimInstance -ClassName Win32_LogicalDisk | Where-Object {$_.DriveType -eq 3} | ForEach-Object {\n" +
                               "    $size = [math]::Round($_.Size / 1GB, 2)\n" +
                               "    $free = [math]::Round($_.FreeSpace / 1GB, 2)\n" +
                               "    $used = $size - $free\n" +
                               "    $percent = [math]::Round(($used / $size) * 100, 1)\n" +
                               "    \n" +
                               "    $status = if ($percent -gt 90) { \"🔴\" } elseif ($percent -gt 80) { \"🟡\" } else { \"🟢\" }\n" +
                               "    \n" +
                               "    Write-Host \"$status Диск $($_.DeviceID) - $size GB (использовано: $used GB, $percent%)\"\n" +
                               "}";
                
                File.WriteAllText(Path.Combine(psFolder, "disk_check.ps1"), psScript);
            }
        }

        private void NewScriptButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewScriptDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var fileName = dialog.ScriptName;
                    var scriptType = dialog.ScriptType;
                    var extension = scriptType == "JavaScript" ? ".js" : ".ps1";
                    var folder = scriptType == "JavaScript" ? "JavaScript" : "PowerShell";
                    
                    var filePath = Path.Combine(_scriptsPath, folder, fileName + extension);
                    
                    if (File.Exists(filePath))
                    {
                        MessageBox.Show("Файл с таким именем уже существует", "Ошибка", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    var template = scriptType == "JavaScript" 
                        ? "// Новый JavaScript скрипт\nconsole.log('Hello from Remote Server Manager!');\n"
                        : "# Новый PowerShell скрипт\nWrite-Host 'Hello from Remote Server Manager!' -ForegroundColor Green\n";
                    
                    File.WriteAllText(filePath, template);
                    LoadScripts();
                    
                    // Открываем файл в редакторе
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    
                    StatusBarText.Text = $"Создан скрипт: {fileName}{extension}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания скрипта: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _scriptsPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия папки: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScriptsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is string filePath && File.Exists(filePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusBarText.Text = $"Ошибка открытия файла: {ex.Message}";
                }
            }
        }

        // WebView управление
        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            WebViewUrlBox.Text = e.Uri;
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                StatusBarText.Text = "Ошибка загрузки страницы";
            }
        }

        private void RefreshWebViewButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.CoreWebView2?.Reload();
        }

        private void HomeWebViewButton_Click(object sender, RoutedEventArgs e)
        {
            LoadWelcomePage();
        }

        private void DevToolsButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.CoreWebView2?.OpenDevToolsWindow();
        }

        // Обновления
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusBarText.Text = "Проверка обновлений...";
                var hasUpdate = await _updateService.CheckForUpdatesAsync();
                
                if (hasUpdate)
                {
                    var result = MessageBox.Show(
                        $"Доступна новая версия: {_updateService.LatestVersion}\n\nОбновить сейчас?",
                        "Обновление", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        await _updateService.DownloadAndInstallUpdateAsync();
                    }
                }
                else
                {
                    MessageBox.Show("У вас установлена последняя версия", "Обновление", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки обновлений: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StatusBarText.Text = "Готов к подключению";
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            settingsDialog.ShowDialog();
        }

        // Дополнительные кнопки
        private void ShowDocumentationButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDocumentationPage();
        }

        private void QuickStartButton_Click(object sender, RoutedEventArgs e)
        {
            LoadQuickStartPage();
        }

        // Вспомогательные методы
        private void LoadWelcomePage()
        {
            var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Remote Server Manager</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; background: #f8f9fa; margin: 0; padding: 40px; }
        .container { max-width: 800px; margin: 0 auto; background: white; padding: 40px; border-radius: 10px; box-shadow: 0 5px 15px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; text-align: center; margin-bottom: 30px; }
        .welcome { text-align: center; color: #6c757d; margin-bottom: 40px; }
        .features { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
        .feature { padding: 20px; background: #f8f9fa; border-radius: 8px; border-left: 4px solid #3498db; }
        .feature h3 { color: #2c3e50; margin: 0 0 10px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🖥️ Remote Server Manager</h1>
        <div class='welcome'>
            <p>Добро пожаловать в приложение для управления удаленными серверами!</p>
            <p>Подключитесь к серверу для начала работы.</p>
        </div>
        <div class='features'>
            <div class='feature'>
                <h3>📜 Выполнение скриптов</h3>
                <p>JavaScript и PowerShell скрипты на удаленных серверах</p>
            </div>
            <div class='feature'>
                <h3>📁 Файловый менеджер</h3>
                <p>Управление файлами и папками на сервере</p>
            </div>
            <div class='feature'>
                <h3>⚙️ Управление процессами</h3>
                <p>Мониторинг и управление процессами</p>
            </div>
            <div class='feature'>
                <h3>🔄 Массовые задачи</h3>
                <p>Автоматизация операций на серверах</p>
            </div>
        </div>
    </div>
</body>
</html>";
            
            WebView.NavigateToString(html);
            InitialMessage.Visibility = Visibility.Collapsed;
        }

        private void LoadDocumentationPage()
        {
            // TODO: Загрузить страницу документации
            StatusBarText.Text = "Загрузка документации...";
        }

        private void LoadQuickStartPage()
        {
            // TODO: Загрузить страницу быстрого старта
            StatusBarText.Text = "Загрузка быстрого старта...";
        }

        private void UpdateTime(object? sender, System.EventArgs e)
        {
            TimeText.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        }

        private void UpdateServerCount()
        {
            ServersCountText.Text = _servers.Count.ToString();
        }

        private void UpdateScriptCount()
        {
            try
            {
                var jsCount = Directory.GetFiles(_scriptsPath, "*.js", SearchOption.AllDirectories).Length;
                var psCount = Directory.GetFiles(_scriptsPath, "*.ps1", SearchOption.AllDirectories).Length;
                ScriptsCountText.Text = (jsCount + psCount).ToString();
            }
            catch
            {
                ScriptsCountText.Text = "0";
            }
        }
    }

    // Модели данных
    public class ServerInfo
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; } = 8080;
        public string Description { get; set; } = "";
        public string StatusColor { get; set; } = "#e74c3c";
    }
}