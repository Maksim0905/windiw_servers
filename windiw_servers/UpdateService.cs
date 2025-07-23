using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace windiw_servers
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;
        private readonly string _updateUrl;
        private readonly string _tempPath;

        public string? LatestVersion { get; private set; }

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _currentVersion = GetCurrentVersion();
            _updateUrl = "https://api.github.com/repos/your-username/remote-server-manager/releases/latest"; // Замените на ваш репозиторий
            _tempPath = Path.Combine(Path.GetTempPath(), "RemoteServerManager");
            
            Directory.CreateDirectory(_tempPath);
        }

        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_updateUrl);
                if (!response.IsSuccessStatusCode)
                    return false;

                var content = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<JsonElement>(content);
                
                LatestVersion = release.GetProperty("tag_name").GetString()?.TrimStart('v');
                
                return !string.IsNullOrEmpty(LatestVersion) && 
                       IsNewerVersion(LatestVersion, _currentVersion);
            }
            catch
            {
                return false;
            }
        }

        public async Task DownloadAndInstallUpdateAsync()
        {
            if (string.IsNullOrEmpty(LatestVersion))
                throw new InvalidOperationException("Сначала проверьте наличие обновлений");

            try
            {
                // Получаем информацию о релизе
                var response = await _httpClient.GetAsync(_updateUrl);
                var content = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<JsonElement>(content);
                
                // Находим подходящий файл для загрузки
                var assets = release.GetProperty("assets").EnumerateArray();
                var downloadUrl = "";
                
                foreach (var asset in assets)
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe") && name.Contains("win"))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                    throw new Exception("Не найден файл обновления для Windows");

                // Загружаем файл
                var updateFilePath = Path.Combine(_tempPath, $"RemoteServerManager_v{LatestVersion}.exe");
                await DownloadFileAsync(downloadUrl, updateFilePath);

                // Создаем скрипт обновления
                var updaterScript = CreateUpdaterScript(updateFilePath);
                
                // Запускаем обновление
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{updaterScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
                
                // Закрываем текущее приложение
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка обновления: {ex.Message}");
            }
        }

        private async Task DownloadFileAsync(string url, string filePath)
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            await using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);
        }

        private string CreateUpdaterScript(string newExecutablePath)
        {
            var currentExecutable = Assembly.GetExecutingAssembly().Location;
            var backupPath = currentExecutable + ".backup";
            var scriptPath = Path.Combine(_tempPath, "update.bat");

            var script = "@echo off\n" +
                        "echo Обновление Remote Server Manager...\n" +
                        "timeout /t 2 /nobreak > nul\n\n" +
                        "echo Создание резервной копии...\n" +
                        $"copy \"{currentExecutable}\" \"{backupPath}\" > nul\n\n" +
                        "echo Установка обновления...\n" +
                        $"copy \"{newExecutablePath}\" \"{currentExecutable}\" > nul\n\n" +
                        "if errorlevel 1 (\n" +
                        "    echo Ошибка обновления, восстановление...\n" +
                        $"    copy \"{backupPath}\" \"{currentExecutable}\" > nul\n" +
                        "    echo Обновление не удалось.\n" +
                        "    pause\n" +
                        "    exit /b 1\n" +
                        ")\n\n" +
                        "echo Очистка временных файлов...\n" +
                        $"del \"{newExecutablePath}\" > nul\n" +
                        $"del \"{backupPath}\" > nul\n\n" +
                        "echo Обновление завершено успешно!\n" +
                        "echo Запуск нового приложения...\n" +
                        $"start \"\" \"{currentExecutable}\"\n\n" +
                        $"del \"{scriptPath}\" > nul\n" +
                        "exit /b 0";

            File.WriteAllText(scriptPath, script);
            return scriptPath;
        }

        private string GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version?.Major}.{version?.Minor}.{version?.Build}";
            }
            catch
            {
                return "1.0.0";
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestParts = latest.Split('.').Select(x => int.Parse(x)).ToArray();
                var currentParts = current.Split('.').Select(x => int.Parse(x)).ToArray();

                for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
                {
                    var latestPart = i < latestParts.Length ? latestParts[i] : 0;
                    var currentPart = i < currentParts.Length ? currentParts[i] : 0;

                    if (latestPart > currentPart) return true;
                    if (latestPart < currentPart) return false;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}