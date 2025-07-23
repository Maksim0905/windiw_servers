using Microsoft.AspNetCore.Mvc;
using ServerManager.Models;
using ServerManager.Services;

namespace ServerManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WindowsServersController : ControllerBase
    {
        private readonly WindowsServerService _serverService;
        private readonly ILogger<WindowsServersController> _logger;

        public WindowsServersController(WindowsServerService serverService, ILogger<WindowsServersController> logger)
        {
            _serverService = serverService;
            _logger = logger;
        }

        /// <summary>
        /// Получить список серверов с возможностью фильтрации
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerInfo>>> GetServers([FromQuery] ServerFilterRequest? filter = null)
        {
            try
            {
                var servers = await _serverService.GetServersAsync(filter);
                return Ok(servers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting servers");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Получить сервер по ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ServerInfo>> GetServer(int id)
        {
            try
            {
                var server = await _serverService.GetServerAsync(id);
                if (server == null)
                    return NotFound();

                return Ok(server);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting server {ServerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Создать новый сервер
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ServerInfo>> CreateServer([FromBody] CreateServerRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var server = await _serverService.CreateServerAsync(request);
                return CreatedAtAction(nameof(GetServer), new { id = server.Id }, server);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating server");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Обновить сервер
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ServerInfo>> UpdateServer(int id, [FromBody] UpdateServerRequest request)
        {
            try
            {
                var server = await _serverService.UpdateServerAsync(id, request);
                if (server == null)
                    return NotFound();

                return Ok(server);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating server {ServerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Удалить сервер
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteServer(int id)
        {
            try
            {
                var success = await _serverService.DeleteServerAsync(id);
                if (!success)
                    return NotFound();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting server {ServerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Проверить статус сервера
        /// </summary>
        [HttpPost("{id}/check-status")]
        public async Task<ActionResult> CheckServerStatus(int id)
        {
            try
            {
                var success = await _serverService.CheckServerStatusAsync(id);
                return Ok(new { success, message = success ? "Server status checked successfully" : "Server is offline or unreachable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking server status {ServerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Проверить статус всех серверов
        /// </summary>
        [HttpPost("check-all-status")]
        public async Task<ActionResult> CheckAllServersStatus()
        {
            try
            {
                await _serverService.CheckAllServersStatusAsync();
                return Ok(new { message = "All servers status check initiated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking all servers status");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Выполнить PowerShell команду на сервере
        /// </summary>
        [HttpPost("{id}/execute")]
        public async Task<ActionResult<string>> ExecuteCommand(int id, [FromBody] ExecuteCommandRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Command))
                    return BadRequest("Command is required");

                var result = await _serverService.ExecuteCommandAsync(id, request.Command);
                return Ok(new { result, executedAt = DateTime.UtcNow });
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command on server {ServerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Получить список Windows сервисов на сервере
        /// </summary>
        [HttpGet("{id}/services")]
        public async Task<ActionResult<List<WindowsServiceInfo>>> GetWindowsServices(int id)
        {
            try
            {
                var services = await _serverService.GetWindowsServicesAsync(id);
                return Ok(services);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Windows services for server {ServerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Управление Windows сервисом (start/stop/restart)
        /// </summary>
        [HttpPost("{id}/services/{serviceName}/{action}")]
        public async Task<ActionResult<string>> ManageWindowsService(int id, string serviceName, string action)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceName))
                    return BadRequest("Service name is required");

                if (!new[] { "start", "stop", "restart" }.Contains(action.ToLower()))
                    return BadRequest("Invalid action. Use: start, stop, restart");

                var result = await _serverService.ManageWindowsServiceAsync(id, serviceName, action);
                return Ok(new { result, action, serviceName, executedAt = DateTime.UtcNow });
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing service {ServiceName} on server {ServerId}", serviceName, id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Получить статистику серверов
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<Dictionary<string, int>>> GetStatistics()
        {
            try
            {
                var stats = await _serverService.GetServerStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting server statistics");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}