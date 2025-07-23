using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Настройка CORS для разрешения всех источников
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// Обслуживание статических файлов
app.UseStaticFiles();

// Получение информации о сервере
app.MapGet("/api/server-info", () => 
{
    return new
    {
        ServerName = Environment.MachineName,
        OS = Environment.OSVersion.ToString(),
        ProcessorCount = Environment.ProcessorCount,
        WorkingDirectory = Environment.CurrentDirectory,
        UserName = Environment.UserName,
        DotNetVersion = Environment.Version.ToString(),
        Timestamp = DateTime.Now
    };
});

// Выполнение JS скриптов через Node.js
app.MapPost("/api/execute-js", async (HttpRequest request) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ExecuteScriptRequest>(body);
        
        if (string.IsNullOrEmpty(data?.Script))
        {
            return Results.BadRequest("Script is required");
        }

        var tempFile = Path.GetTempFileName() + ".js";
        await File.WriteAllTextAsync(tempFile, data.Script);

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = tempFile,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        var output = await process!.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        File.Delete(tempFile);

        return Results.Ok(new
        {
            Success = process.ExitCode == 0,
            Output = output,
            Error = error,
            ExitCode = process.ExitCode
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Выполнение PowerShell скриптов
app.MapPost("/api/execute-ps", async (HttpRequest request) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ExecuteScriptRequest>(body);
        
        if (string.IsNullOrEmpty(data?.Script))
        {
            return Results.BadRequest("Script is required");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"{data.Script}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        var output = await process!.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return Results.Ok(new
        {
            Success = process.ExitCode == 0,
            Output = output,
            Error = error,
            ExitCode = process.ExitCode
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Получение списка файлов и папок
app.MapGet("/api/files", (string? path) =>
{
    try
    {
        var targetPath = string.IsNullOrEmpty(path) ? Environment.CurrentDirectory : path;
        
        if (!Directory.Exists(targetPath))
        {
            return Results.BadRequest("Directory does not exist");
        }

        var directories = Directory.GetDirectories(targetPath)
            .Select(dir => new
            {
                Name = Path.GetFileName(dir),
                FullPath = dir,
                Type = "directory",
                Size = 0L,
                Modified = Directory.GetLastWriteTime(dir)
            });

        var files = Directory.GetFiles(targetPath)
            .Select(file => new
            {
                Name = Path.GetFileName(file),
                FullPath = file,
                Type = "file",
                Size = new FileInfo(file).Length,
                Modified = File.GetLastWriteTime(file)
            });

        return Results.Ok(new
        {
            CurrentPath = targetPath,
            Items = directories.Concat(files).OrderBy(x => x.Type).ThenBy(x => x.Name)
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Загрузка файла
app.MapPost("/api/upload", async (HttpRequest request) =>
{
    try
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Invalid content type");
        }

        var form = await request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        var targetPath = form["path"].ToString();

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest("No file uploaded");
        }

        var uploadPath = string.IsNullOrEmpty(targetPath) 
            ? Environment.CurrentDirectory 
            : targetPath;

        var filePath = Path.Combine(uploadPath, file.FileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Results.Ok(new { Message = "File uploaded successfully", Path = filePath });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Скачивание файла
app.MapGet("/api/download", (string filePath) =>
{
    try
    {
        if (!File.Exists(filePath))
        {
            return Results.NotFound("File not found");
        }

        var fileName = Path.GetFileName(filePath);
        var fileBytes = File.ReadAllBytes(filePath);
        
        return Results.File(fileBytes, "application/octet-stream", fileName);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Создание папки
app.MapPost("/api/create-folder", async (HttpRequest request) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<CreateFolderRequest>(body);
        
        if (string.IsNullOrEmpty(data?.Path) || string.IsNullOrEmpty(data.Name))
        {
            return Results.BadRequest("Path and Name are required");
        }

        var folderPath = Path.Combine(data.Path, data.Name);
        Directory.CreateDirectory(folderPath);

        return Results.Ok(new { Message = "Folder created successfully", Path = folderPath });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Удаление файла или папки
app.MapDelete("/api/delete", (string path) =>
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return Results.Ok(new { Message = "File deleted successfully" });
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            return Results.Ok(new { Message = "Folder deleted successfully" });
        }
        else
        {
            return Results.NotFound("File or folder not found");
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Получение содержимого текстового файла
app.MapGet("/api/file-content", async (string filePath) =>
{
    try
    {
        if (!File.Exists(filePath))
        {
            return Results.NotFound("File not found");
        }

        var content = await File.ReadAllTextAsync(filePath);
        return Results.Ok(new { Content = content });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Сохранение содержимого файла
app.MapPost("/api/save-file", async (HttpRequest request) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<SaveFileRequest>(body);
        
        if (string.IsNullOrEmpty(data?.FilePath) || data.Content == null)
        {
            return Results.BadRequest("FilePath and Content are required");
        }

        await File.WriteAllTextAsync(data.FilePath, data.Content);
        return Results.Ok(new { Message = "File saved successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Получение списка запущенных процессов
app.MapGet("/api/processes", () =>
{
    try
    {
        var processes = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.ProcessName))
            .Select(p => new
            {
                Id = p.Id,
                Name = p.ProcessName,
                MemoryUsage = p.WorkingSet64,
                StartTime = p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                WindowTitle = p.MainWindowTitle
            })
            .OrderBy(p => p.Name)
            .ToList();

        return Results.Ok(processes);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Завершение процесса
app.MapDelete("/api/process/{id:int}", (int id) =>
{
    try
    {
        var process = Process.GetProcessById(id);
        process.Kill();
        return Results.Ok(new { Message = "Process terminated successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Массовое выполнение задач
app.MapPost("/api/batch-execute", async (HttpRequest request) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<BatchExecuteRequest>(body);
        
        if (data?.Tasks == null || data.Tasks.Count == 0)
        {
            return Results.BadRequest("Tasks are required");
        }

        var results = new List<object>();

        foreach (var task in data.Tasks)
        {
            try
            {
                ProcessStartInfo startInfo;
                
                if (task.Type.ToLower() == "js")
                {
                    var tempFile = Path.GetTempFileName() + ".js";
                    await File.WriteAllTextAsync(tempFile, task.Script);
                    
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = tempFile,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }
                else // PowerShell
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{task.Script}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }

                using var process = Process.Start(startInfo);
                var output = await process!.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                results.Add(new
                {
                    TaskName = task.Name,
                    Success = process.ExitCode == 0,
                    Output = output,
                    Error = error,
                    ExitCode = process.ExitCode
                });

                if (task.Type.ToLower() == "js")
                {
                    var tempFile = startInfo.Arguments;
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    TaskName = task.Name,
                    Success = false,
                    Output = "",
                    Error = ex.Message,
                    ExitCode = -1
                });
            }
        }

        return Results.Ok(new { Results = results });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Главная страница возвращает HTML интерфейс
app.MapGet("/", () => Results.Content(GetIndexHtml(), "text/html"));

app.Run("http://localhost:8080");

// HTML интерфейс
static string GetIndexHtml() => """
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Server Admin Panel</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }
        
        .container {
            max-width: 1400px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 20px 40px rgba(0,0,0,0.1);
            overflow: hidden;
        }
        
        .header {
            background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
            color: white;
            padding: 30px;
            text-align: center;
        }
        
        .header h1 {
            font-size: 2.5em;
            margin-bottom: 10px;
        }
        
        .server-info {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 10px;
            margin-top: 20px;
            opacity: 0.9;
        }
        
        .info-item {
            background: rgba(255,255,255,0.1);
            padding: 10px;
            border-radius: 8px;
            text-align: left;
        }
        
        .tabs {
            display: flex;
            background: #f8f9fa;
            border-bottom: 1px solid #dee2e6;
        }
        
        .tab {
            padding: 15px 25px;
            cursor: pointer;
            border: none;
            background: none;
            font-size: 16px;
            font-weight: 500;
            color: #6c757d;
            transition: all 0.3s ease;
        }
        
        .tab.active {
            background: white;
            color: #495057;
            border-bottom: 3px solid #007bff;
        }
        
        .tab:hover {
            background: #e9ecef;
        }
        
        .tab-content {
            padding: 30px;
            display: none;
        }
        
        .tab-content.active {
            display: block;
        }
        
        .form-group {
            margin-bottom: 20px;
        }
        
        label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #495057;
        }
        
        input, textarea, select {
            width: 100%;
            padding: 12px;
            border: 2px solid #e9ecef;
            border-radius: 8px;
            font-size: 14px;
            transition: border-color 0.3s ease;
        }
        
        input:focus, textarea:focus, select:focus {
            outline: none;
            border-color: #007bff;
        }
        
        textarea {
            min-height: 200px;
            font-family: 'Courier New', monospace;
            resize: vertical;
        }
        
        button {
            background: linear-gradient(135deg, #007bff 0%, #0056b3 100%);
            color: white;
            border: none;
            padding: 12px 25px;
            border-radius: 8px;
            cursor: pointer;
            font-size: 16px;
            font-weight: 600;
            transition: all 0.3s ease;
            margin-right: 10px;
            margin-bottom: 10px;
        }
        
        button:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(0,123,255,0.3);
        }
        
        button.danger {
            background: linear-gradient(135deg, #dc3545 0%, #c82333 100%);
        }
        
        button.success {
            background: linear-gradient(135deg, #28a745 0%, #1e7e34 100%);
        }
        
        .result {
            margin-top: 20px;
            padding: 20px;
            border-radius: 8px;
            font-family: 'Courier New', monospace;
            white-space: pre-wrap;
            max-height: 400px;
            overflow-y: auto;
        }
        
        .result.success {
            background: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
        }
        
        .result.error {
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
        }
        
        .file-browser {
            border: 2px solid #e9ecef;
            border-radius: 8px;
            max-height: 400px;
            overflow-y: auto;
        }
        
        .file-item {
            display: flex;
            align-items: center;
            padding: 12px;
            border-bottom: 1px solid #f8f9fa;
            cursor: pointer;
            transition: background-color 0.2s ease;
        }
        
        .file-item:hover {
            background: #f8f9fa;
        }
        
        .file-item.directory {
            background: #e3f2fd;
        }
        
        .file-icon {
            margin-right: 10px;
            font-size: 18px;
        }
        
        .file-info {
            flex: 1;
        }
        
        .file-name {
            font-weight: 600;
            color: #495057;
        }
        
        .file-details {
            font-size: 12px;
            color: #6c757d;
            margin-top: 4px;
        }
        
        .current-path {
            background: #f8f9fa;
            padding: 10px;
            border-bottom: 1px solid #dee2e6;
            font-family: 'Courier New', monospace;
            font-size: 14px;
        }
        
        .process-list {
            border: 2px solid #e9ecef;
            border-radius: 8px;
            max-height: 500px;
            overflow-y: auto;
        }
        
        .process-item {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 12px;
            border-bottom: 1px solid #f8f9fa;
        }
        
        .process-info {
            flex: 1;
        }
        
        .process-name {
            font-weight: 600;
            color: #495057;
        }
        
        .process-details {
            font-size: 12px;
            color: #6c757d;
            margin-top: 4px;
        }
        
        .batch-task {
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 8px;
            padding: 15px;
            margin-bottom: 15px;
        }
        
        .batch-task-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 10px;
        }
        
        .loading {
            display: none;
            text-align: center;
            padding: 20px;
            color: #6c757d;
        }
        
        .grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }
        
        @media (max-width: 768px) {
            .grid {
                grid-template-columns: 1fr;
            }
            
            .tabs {
                flex-wrap: wrap;
            }
            
            .tab {
                flex: 1;
                min-width: 120px;
            }
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🖥️ Server Admin Panel</h1>
            <div class="server-info" id="serverInfo">
                <div class="info-item">Загрузка информации о сервере...</div>
            </div>
        </div>
        
        <div class="tabs">
            <button class="tab active" onclick="showTab('scripts')">📜 Скрипты</button>
            <button class="tab" onclick="showTab('files')">📁 Файлы</button>
            <button class="tab" onclick="showTab('processes')">⚙️ Процессы</button>
            <button class="tab" onclick="showTab('batch')">🔄 Массовые задачи</button>
        </div>
        
        <div id="scripts" class="tab-content active">
            <h2>Выполнение скриптов</h2>
            <div class="grid">
                <div>
                    <h3>JavaScript (Node.js)</h3>
                    <div class="form-group">
                        <label for="jsScript">Код JavaScript:</label>
                        <textarea id="jsScript" placeholder="console.log('Hello from Node.js!');&#10;const fs = require('fs');&#10;console.log('Текущая директория:', process.cwd());">console.log('Hello from Server Admin Panel!');
console.log('Текущее время:', new Date().toLocaleString());
console.log('Версия Node.js:', process.version);

// Пример работы с файловой системой
const fs = require('fs');
try {
    const files = fs.readdirSync('.');
    console.log('Файлы в текущей директории:', files.slice(0, 5));
} catch (err) {
    console.log('Ошибка чтения директории:', err.message);
}</textarea>
                    </div>
                    <button onclick="executeScript('js')">▶️ Выполнить JS</button>
                    <div id="jsResult" class="result" style="display: none;"></div>
                </div>
                
                <div>
                    <h3>PowerShell</h3>
                    <div class="form-group">
                        <label for="psScript">Код PowerShell:</label>
                        <textarea id="psScript" placeholder="Get-Date&#10;Get-ComputerInfo | Select-Object WindowsProductName, TotalPhysicalMemory">Write-Host "Hello from Server Admin Panel!"
Get-Date
Write-Host "Имя компьютера: $env:COMPUTERNAME"
Write-Host "Текущий пользователь: $env:USERNAME"

# Информация о системе
$os = Get-CimInstance -ClassName Win32_OperatingSystem
Write-Host "ОС: $($os.Caption)"
Write-Host "Версия: $($os.Version)"

# Доступное место на диске C:
$disk = Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='C:'"
$freeGB = [math]::Round($disk.FreeSpace / 1GB, 2)
Write-Host "Свободно на диске C: $freeGB GB"</textarea>
                    </div>
                    <button onclick="executeScript('ps')">▶️ Выполнить PowerShell</button>
                    <div id="psResult" class="result" style="display: none;"></div>
                </div>
            </div>
        </div>
        
        <div id="files" class="tab-content">
            <h2>Файловый менеджер</h2>
            <div class="grid">
                <div>
                    <h3>Навигация</h3>
                    <button onclick="loadFiles()">🔄 Обновить</button>
                    <button onclick="goUp()">⬆️ Вверх</button>
                    <button onclick="createFolder()">📁 Создать папку</button>
                    
                    <div class="file-browser">
                        <div class="current-path" id="currentPath"></div>
                        <div id="fileList">
                            <div class="loading">Загрузка файлов...</div>
                        </div>
                    </div>
                </div>
                
                <div>
                    <h3>Операции с файлами</h3>
                    <div class="form-group">
                        <label for="fileUpload">Загрузить файл:</label>
                        <input type="file" id="fileUpload" multiple>
                        <button onclick="uploadFiles()">📤 Загрузить</button>
                    </div>
                    
                    <div class="form-group" id="fileEditor" style="display: none;">
                        <label for="fileContent">Редактор файла:</label>
                        <textarea id="fileContent" style="min-height: 300px;"></textarea>
                        <button onclick="saveFile()">💾 Сохранить</button>
                        <button onclick="closeEditor()">❌ Закрыть</button>
                    </div>
                    
                    <div id="fileResult" class="result" style="display: none;"></div>
                </div>
            </div>
        </div>
        
        <div id="processes" class="tab-content">
            <h2>Управление процессами</h2>
            <button onclick="loadProcesses()">🔄 Обновить список</button>
            
            <div class="process-list" id="processList">
                <div class="loading">Загрузка процессов...</div>
            </div>
            
            <div id="processResult" class="result" style="display: none;"></div>
        </div>
        
        <div id="batch" class="tab-content">
            <h2>Массовое выполнение задач</h2>
            <p>Создайте набор задач для выполнения на сервере. Полезно для настройки балансировщиков, развертывания приложений и других массовых операций.</p>
            
            <div id="batchTasks">
                <div class="batch-task">
                    <div class="batch-task-header">
                        <input type="text" placeholder="Название задачи" value="Проверка системы">
                        <select>
                            <option value="js">JavaScript</option>
                            <option value="ps" selected>PowerShell</option>
                        </select>
                        <button onclick="removeTask(this)" class="danger">❌</button>
                    </div>
                    <textarea placeholder="Код задачи">Write-Host "Системная информация:"
Get-ComputerInfo | Select-Object WindowsProductName, TotalPhysicalMemory
Write-Host "Сетевые адаптеры:"
Get-NetAdapter | Where-Object Status -eq "Up" | Select-Object Name, LinkSpeed</textarea>
                </div>
            </div>
            
            <button onclick="addTask()">➕ Добавить задачу</button>
            <button onclick="executeBatch()" class="success">🚀 Выполнить все задачи</button>
            
            <div id="batchResult" class="result" style="display: none;"></div>
        </div>
    </div>

    <script>
        let currentPath = '';
        let editingFile = '';

        // Загрузка информации о сервере
        async function loadServerInfo() {
            try {
                const response = await fetch('/api/server-info');
                const data = await response.json();
                
                const serverInfo = document.getElementById('serverInfo');
                serverInfo.innerHTML = `
                    <div class="info-item">
                        <strong>Сервер:</strong> ${data.serverName}
                    </div>
                    <div class="info-item">
                        <strong>ОС:</strong> ${data.os}
                    </div>
                    <div class="info-item">
                        <strong>Процессоры:</strong> ${data.processorCount}
                    </div>
                    <div class="info-item">
                        <strong>Пользователь:</strong> ${data.userName}
                    </div>
                    <div class="info-item">
                        <strong>.NET:</strong> ${data.dotNetVersion}
                    </div>
                    <div class="info-item">
                        <strong>Время:</strong> ${new Date(data.timestamp).toLocaleString()}
                    </div>
                `;
            } catch (error) {
                console.error('Ошибка загрузки информации о сервере:', error);
            }
        }

        // Переключение вкладок
        function showTab(tabName) {
            // Скрыть все вкладки
            document.querySelectorAll('.tab-content').forEach(tab => {
                tab.classList.remove('active');
            });
            document.querySelectorAll('.tab').forEach(tab => {
                tab.classList.remove('active');
            });
            
            // Показать выбранную вкладку
            document.getElementById(tabName).classList.add('active');
            event.target.classList.add('active');
            
            // Загрузить данные для вкладки
            if (tabName === 'files') {
                loadFiles();
            } else if (tabName === 'processes') {
                loadProcesses();
            }
        }

        // Выполнение скриптов
        async function executeScript(type) {
            const scriptElement = document.getElementById(type + 'Script');
            const resultElement = document.getElementById(type + 'Result');
            
            const script = scriptElement.value;
            if (!script.trim()) {
                showResult(resultElement, 'Введите код для выполнения', false);
                return;
            }
            
            try {
                const endpoint = type === 'js' ? '/api/execute-js' : '/api/execute-ps';
                const response = await fetch(endpoint, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ script })
                });
                
                const data = await response.json();
                
                if (data.success) {
                    showResult(resultElement, data.output, true);
                } else {
                    showResult(resultElement, `Ошибка выполнения:\n${data.error}\n\nВывод:\n${data.output}`, false);
                }
            } catch (error) {
                showResult(resultElement, `Ошибка запроса: ${error.message}`, false);
            }
        }

        // Отображение результата
        function showResult(element, text, success) {
            element.style.display = 'block';
            element.className = `result ${success ? 'success' : 'error'}`;
            element.textContent = text;
        }

        // Загрузка списка файлов
        async function loadFiles(path = '') {
            try {
                const response = await fetch(`/api/files?path=${encodeURIComponent(path)}`);
                const data = await response.json();
                
                currentPath = data.currentPath;
                document.getElementById('currentPath').textContent = currentPath;
                
                const fileList = document.getElementById('fileList');
                fileList.innerHTML = '';
                
                data.items.forEach(item => {
                    const fileItem = document.createElement('div');
                    fileItem.className = `file-item ${item.type}`;
                    fileItem.onclick = () => {
                        if (item.type === 'directory') {
                            loadFiles(item.fullPath);
                        } else {
                            openFile(item.fullPath);
                        }
                    };
                    
                    const icon = item.type === 'directory' ? '📁' : '📄';
                    const size = item.type === 'file' ? formatFileSize(item.size) : '';
                    
                    fileItem.innerHTML = `
                        <div class="file-icon">${icon}</div>
                        <div class="file-info">
                            <div class="file-name">${item.name}</div>
                            <div class="file-details">
                                ${size} • ${new Date(item.modified).toLocaleString()}
                            </div>
                        </div>
                        <button onclick="event.stopPropagation(); deleteItem('${item.fullPath}')" class="danger" style="margin-left: auto;">❌</button>
                    `;
                    
                    fileList.appendChild(fileItem);
                });
            } catch (error) {
                console.error('Ошибка загрузки файлов:', error);
            }
        }

        // Форматирование размера файла
        function formatFileSize(bytes) {
            if (bytes === 0) return '0 B';
            const k = 1024;
            const sizes = ['B', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
        }

        // Переход на уровень вверх
        function goUp() {
            const parentPath = currentPath.substring(0, currentPath.lastIndexOf('\\'));
            if (parentPath) {
                loadFiles(parentPath);
            }
        }

        // Создание папки
        async function createFolder() {
            const name = prompt('Введите название папки:');
            if (!name) return;
            
            try {
                const response = await fetch('/api/create-folder', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ path: currentPath, name })
                });
                
                const data = await response.json();
                showResult(document.getElementById('fileResult'), data.message || data.error, response.ok);
                
                if (response.ok) {
                    loadFiles(currentPath);
                }
            } catch (error) {
                showResult(document.getElementById('fileResult'), `Ошибка: ${error.message}`, false);
            }
        }

        // Загрузка файлов
        async function uploadFiles() {
            const fileInput = document.getElementById('fileUpload');
            const files = fileInput.files;
            
            if (files.length === 0) {
                showResult(document.getElementById('fileResult'), 'Выберите файлы для загрузки', false);
                return;
            }
            
            for (const file of files) {
                const formData = new FormData();
                formData.append('file', file);
                formData.append('path', currentPath);
                
                try {
                    const response = await fetch('/api/upload', {
                        method: 'POST',
                        body: formData
                    });
                    
                    const data = await response.json();
                    showResult(document.getElementById('fileResult'), 
                        `${file.name}: ${data.message || data.error}`, response.ok);
                } catch (error) {
                    showResult(document.getElementById('fileResult'), 
                        `${file.name}: Ошибка ${error.message}`, false);
                }
            }
            
            loadFiles(currentPath);
            fileInput.value = '';
        }

        // Открытие файла для редактирования
        async function openFile(filePath) {
            try {
                const response = await fetch(`/api/file-content?filePath=${encodeURIComponent(filePath)}`);
                const data = await response.json();
                
                if (response.ok) {
                    editingFile = filePath;
                    document.getElementById('fileContent').value = data.content;
                    document.getElementById('fileEditor').style.display = 'block';
                } else {
                    // Если файл нельзя прочитать как текст, предложить скачать
                    const link = document.createElement('a');
                    link.href = `/api/download?filePath=${encodeURIComponent(filePath)}`;
                    link.download = filePath.split('\\').pop();
                    link.click();
                }
            } catch (error) {
                showResult(document.getElementById('fileResult'), `Ошибка: ${error.message}`, false);
            }
        }

        // Сохранение файла
        async function saveFile() {
            if (!editingFile) return;
            
            const content = document.getElementById('fileContent').value;
            
            try {
                const response = await fetch('/api/save-file', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ filePath: editingFile, content })
                });
                
                const data = await response.json();
                showResult(document.getElementById('fileResult'), data.message || data.error, response.ok);
                
                if (response.ok) {
                    loadFiles(currentPath);
                }
            } catch (error) {
                showResult(document.getElementById('fileResult'), `Ошибка: ${error.message}`, false);
            }
        }

        // Закрытие редактора
        function closeEditor() {
            document.getElementById('fileEditor').style.display = 'none';
            editingFile = '';
        }

        // Удаление файла или папки
        async function deleteItem(path) {
            if (!confirm(`Удалить "${path}"?`)) return;
            
            try {
                const response = await fetch(`/api/delete?path=${encodeURIComponent(path)}`, {
                    method: 'DELETE'
                });
                
                const data = await response.json();
                showResult(document.getElementById('fileResult'), data.message || data.error, response.ok);
                
                if (response.ok) {
                    loadFiles(currentPath);
                }
            } catch (error) {
                showResult(document.getElementById('fileResult'), `Ошибка: ${error.message}`, false);
            }
        }

        // Загрузка списка процессов
        async function loadProcesses() {
            try {
                const response = await fetch('/api/processes');
                const processes = await response.json();
                
                const processList = document.getElementById('processList');
                processList.innerHTML = '';
                
                processes.forEach(process => {
                    const processItem = document.createElement('div');
                    processItem.className = 'process-item';
                    
                    processItem.innerHTML = `
                        <div class="process-info">
                            <div class="process-name">${process.name} (PID: ${process.id})</div>
                            <div class="process-details">
                                Память: ${formatFileSize(process.memoryUsage)} • 
                                Запущен: ${process.startTime}
                                ${process.windowTitle ? ` • ${process.windowTitle}` : ''}
                            </div>
                        </div>
                        <button onclick="killProcess(${process.id})" class="danger">🗙 Завершить</button>
                    `;
                    
                    processList.appendChild(processItem);
                });
            } catch (error) {
                console.error('Ошибка загрузки процессов:', error);
            }
        }

        // Завершение процесса
        async function killProcess(id) {
            if (!confirm(`Завершить процесс с PID ${id}?`)) return;
            
            try {
                const response = await fetch(`/api/process/${id}`, {
                    method: 'DELETE'
                });
                
                const data = await response.json();
                showResult(document.getElementById('processResult'), data.message || data.error, response.ok);
                
                if (response.ok) {
                    loadProcesses();
                }
            } catch (error) {
                showResult(document.getElementById('processResult'), `Ошибка: ${error.message}`, false);
            }
        }

        // Добавление задачи в пакет
        function addTask() {
            const batchTasks = document.getElementById('batchTasks');
            const taskDiv = document.createElement('div');
            taskDiv.className = 'batch-task';
            
            taskDiv.innerHTML = `
                <div class="batch-task-header">
                    <input type="text" placeholder="Название задачи">
                    <select>
                        <option value="js">JavaScript</option>
                        <option value="ps">PowerShell</option>
                    </select>
                    <button onclick="removeTask(this)" class="danger">❌</button>
                </div>
                <textarea placeholder="Код задачи"></textarea>
            `;
            
            batchTasks.appendChild(taskDiv);
        }

        // Удаление задачи
        function removeTask(button) {
            button.closest('.batch-task').remove();
        }

        // Выполнение пакета задач
        async function executeBatch() {
            const tasks = [];
            const taskElements = document.querySelectorAll('.batch-task');
            
            taskElements.forEach(taskElement => {
                const name = taskElement.querySelector('input').value || 'Безымянная задача';
                const type = taskElement.querySelector('select').value;
                const script = taskElement.querySelector('textarea').value;
                
                if (script.trim()) {
                    tasks.push({ name, type, script });
                }
            });
            
            if (tasks.length === 0) {
                showResult(document.getElementById('batchResult'), 'Нет задач для выполнения', false);
                return;
            }
            
            try {
                const response = await fetch('/api/batch-execute', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ tasks })
                });
                
                const data = await response.json();
                
                let resultText = 'Результаты выполнения пакета задач:\n\n';
                data.results.forEach(result => {
                    resultText += `=== ${result.taskName} ===\n`;
                    resultText += `Статус: ${result.success ? 'Успешно' : 'Ошибка'}\n`;
                    if (result.output) resultText += `Вывод:\n${result.output}\n`;
                    if (result.error) resultText += `Ошибка:\n${result.error}\n`;
                    resultText += `Код завершения: ${result.exitCode}\n\n`;
                });
                
                showResult(document.getElementById('batchResult'), resultText, response.ok);
            } catch (error) {
                showResult(document.getElementById('batchResult'), `Ошибка: ${error.message}`, false);
            }
        }

        // Инициализация при загрузке страницы
        document.addEventListener('DOMContentLoaded', () => {
            loadServerInfo();
            loadFiles();
        });
    </script>
</body>
</html>
""";
