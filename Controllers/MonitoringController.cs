using Microsoft.AspNetCore.Mvc;
using HumanQuery.Services;
using System;

namespace HumanQuery.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonitoringController : ControllerBase
    {
        private readonly QueryLogService _logService;

        public MonitoringController(QueryLogService logService)
        {
            _logService = logService;
        }

        [HttpGet("logs")]
        public IActionResult GetLogs()
        {
            var logs = _logService.GetAllLogs();
            return Ok(logs);
        }

        [HttpGet("logs/{id}")]
        public IActionResult GetLogById(Guid id)
        {
            var log = _logService.GetLogById(id);
            if (log == null)
            {
                return NotFound(new { message = "Log no encontrado" });
            }
            return Ok(log);
        }

        [HttpDelete("logs")]
        public IActionResult ClearLogs()
        {
            _logService.ClearLogs();
            return Ok(new { message = "Logs limpiados exitosamente" });
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var logs = _logService.GetAllLogs();
            var stats = new
            {
                totalQueries = logs.Count,
                successfulQueries = logs.Count(l => l.Success),
                failedQueries = logs.Count(l => !l.Success),
                averageResponseTime = logs.Any() ? logs.Average(l => l.ElapsedMilliseconds) : 0
            };
            return Ok(stats);
        }
    }
}
