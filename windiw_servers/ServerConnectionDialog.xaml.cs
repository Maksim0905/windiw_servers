using System;
using System.Threading.Tasks;
using System.Windows;
using windiw_servers.Services;
using System.Text.Json;

namespace windiw_servers
{
    public partial class ServerConnectionDialog : Window
    {
        private ServerApiService? _apiService;

        public string ServerUrl { get; private set; } = "";
        public bool AutoConnect { get; private set; }
        public bool AutoRefresh { get; private set; } = true;

        public ServerConnectionDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        public ServerConnectionDialog(string currentUrl, bool autoConnect, bool autoRefresh) : this()
        {
            ServerUrlTextBox.Text = currentUrl;
            AutoConnectCheckBox.IsChecked = autoConnect;
            AutoRefreshCheckBox.IsChecked = autoRefresh;
        }

        private void LoadSettings()
        {
            try
            {
                var settingsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RemoteServerManager", "server-connection.json");

                if (System.IO.File.Exists(settingsPath))
                {
                    var json = System.IO.File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<ConnectionSettings>(json);
                    
                    if (settings != null)
                    {
                        ServerUrlTextBox.Text = settings.ServerUrl;
                        AutoConnectCheckBox.IsChecked = settings.AutoConnect;
                        AutoRefreshCheckBox.IsChecked = settings.AutoRefresh;
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки загрузки настроек
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new ConnectionSettings
                {
                    ServerUrl = ServerUrlTextBox.Text.Trim(),
                    AutoConnect = AutoConnectCheckBox.IsChecked == true,
                    AutoRefresh = AutoRefreshCheckBox.IsChecked == true
                };

                var settingsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RemoteServerManager", "server-connection.json");

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath)!);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // Игнорируем ошибки сохранения настроек
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ServerUrlTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(url))
            {
                ConnectionStatusText.Text = "Введите URL сервера";
                ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Content = "Проверка...";
            ConnectionStatusText.Text = "Проверка подключения...";
            ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Orange;

            try
            {
                _apiService?.Dispose();
                _apiService = new ServerApiService(url);

                var isConnected = await _apiService.TestConnectionAsync();
                
                if (isConnected)
                {
                    ConnectionStatusText.Text = "✅ Подключение успешно";
                    ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Green;

                    // Получаем информацию о сервере
                    await LoadServerInfo();
                }
                else
                {
                    ConnectionStatusText.Text = "❌ Сервер недоступен";
                    ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    ServerInfoPanel.Visibility = Visibility.Collapsed;
                    NoServerInfoText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = $"❌ Ошибка: {ex.Message}";
                ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
                ServerInfoPanel.Visibility = Visibility.Collapsed;
                NoServerInfoText.Visibility = Visibility.Visible;
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Content = "Проверить подключение";
            }
        }

        private async Task LoadServerInfo()
        {
            try
            {
                if (_apiService == null) return;

                // Получаем базовую информацию о сервере
                var response = await _apiService.TestConnectionAsync();
                if (response)
                {
                    ServerVersionText.Text = "🚀 Server Manager API v1.0.0";
                    ServerStatusText.Text = "✅ Сервер работает";

                    // Получаем статистику серверов
                    try
                    {
                        var stats = await _apiService.GetStatisticsAsync();
                        var total = stats.GetValueOrDefault("Total", 0);
                        var online = stats.GetValueOrDefault("Online", 0);
                        var offline = stats.GetValueOrDefault("Offline", 0);
                        
                        ServersCountText.Text = $"📊 Серверов: {total} (онлайн: {online}, оффлайн: {offline})";
                    }
                    catch
                    {
                        ServersCountText.Text = "📊 Статистика недоступна";
                    }

                    ServerInfoPanel.Visibility = Visibility.Visible;
                    NoServerInfoText.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Игнорируем ошибки получения дополнительной информации
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ServerUrlTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введите URL сервера", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем формат URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                MessageBox.Show("Введите корректный URL (например: http://localhost:8080)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ServerUrl = url;
            AutoConnect = AutoConnectCheckBox.IsChecked == true;
            AutoRefresh = AutoRefreshCheckBox.IsChecked == true;

            SaveSettings();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _apiService?.Dispose();
            base.OnClosed(e);
        }
    }

    public class ConnectionSettings
    {
        public string ServerUrl { get; set; } = "http://localhost:8080";
        public bool AutoConnect { get; set; } = false;
        public bool AutoRefresh { get; set; } = true;
    }
}