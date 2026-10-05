using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using HumanQuery.Services;
using HumanQuery.Models;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Data.SqlClient; // Para SQL Server

namespace HumanQuery.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HumanQueryController : ControllerBase
    {
        private readonly OpenAiService _openAiService;
        private readonly IConfiguration _configuration;
        private readonly QueryLogService _logService;
        private readonly string _connectionString;

        public HumanQueryController(
            OpenAiService openAiService,
            IConfiguration configuration,
            QueryLogService logService)
        {
            _openAiService = openAiService;
            _configuration = configuration;
            _logService = logService;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        private string GetSchema()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var cmd = new SqlCommand(@"
                    SELECT TABLE_NAME
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                ", connection);

                var reader = cmd.ExecuteReader();
                var tableNames = new List<string>();

                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }

                reader.Close();

                var schemaDescription = new StringBuilder();

                foreach (var tableName in tableNames)
                {
                    schemaDescription.Append($"{tableName}(");

                    var columnsCmd = new SqlCommand(@"
                        SELECT COLUMN_NAME, DATA_TYPE
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = @TableName
                        ORDER BY ORDINAL_POSITION
                    ", connection);

                    columnsCmd.Parameters.AddWithValue("@TableName", tableName);

                    var columnsReader = columnsCmd.ExecuteReader();
                    var columns = new List<string>();

                    while (columnsReader.Read())
                    {
                        columns.Add($"{columnsReader.GetString(0)} {columnsReader.GetString(1)}");
                    }

                    schemaDescription.Append(string.Join(", ", columns));
                    schemaDescription.AppendLine(");");

                    columnsReader.Close();
                }

                var schema = schemaDescription.ToString();

                Console.WriteLine("=========== SCHEMA ENVIADO A IA ===========");
                Console.WriteLine(schema);
                Console.WriteLine("===========================================");

                return schema;
            }
        }

        private async Task<List<Dictionary<string, object>>> QueryDatabaseAsync(string sqlQuery)
        {
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var columnName = reader.GetName(i);
                                    var value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                                    row[columnName] = value;
                                }

                                results.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ejecutando SQL generado:");
                Console.WriteLine(ex.Message);
                // Devuelve la lista vacía para que el flujo continúe aunque haya error
            }

            return results;
        }

        private string CleanJsonFromMarkdown(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            // Remove markdown code blocks (```json ... ```)
            var trimmed = response.Trim();
            if (trimmed.StartsWith("```json"))
            {
                trimmed = trimmed.Substring(7); // Remove ```json
            }
            else if (trimmed.StartsWith("```"))
            {
                trimmed = trimmed.Substring(3); // Remove ```
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3); // Remove trailing ```
            }

            return trimmed.Trim();
        }

        private async Task<(string response, string prompt)> HumanQueryToSqlAsync(string humanQuery)
        {
            var databaseSchema = GetSchema();

            var systemMessage = $@"
                Given the following schema, write a SQL query that retrieves the requested information.
                Return the SQL query inside a JSON structure with the key ""sql_query"".

                IMPORTANT: This is SQL Server, so:
                - Always use square brackets [] around table names and column names that have spaces or special characters
                - For example: [Order Details] instead of Order Details
                - For example: [Unit Price] instead of Unit Price
                - Standard column names without spaces don't need brackets but it's safer to always use them

                <schema>
                {databaseSchema}
                </schema>
            ";

            var userMessage = humanQuery;

            var prompt = $"{systemMessage}\nUser query: {userMessage}";

            var response = await _openAiService.SendPromptAsync(prompt);

            Console.WriteLine("=========== RESPUESTA DE IA (JSON SQL) ===========");
            Console.WriteLine(response);
            Console.WriteLine("==================================================");

            return (response, prompt);
        }

        private async Task<(string response, string prompt)> BuildAnswerAsync(List<Dictionary<string, object>> result, string humanQuery)
        {
            var systemMessage = $@"
                Given a user's question and the SQL rows response from the database,
                write a human-readable response to the user's question.
                <user_question>
                {humanQuery}
                </user_question>
                <sql_response>
                {JsonConvert.SerializeObject(result)}
                </sql_response>
            ";

            var prompt = $"{systemMessage}";

            var response = await _openAiService.SendPromptAsync(prompt);

            return (response, prompt);
        }

        [HttpPost("human_query")]
        public async Task<IActionResult> HumanQuery([FromBody] PostHumanQueryPayload payload)
        {
            var stopwatch = Stopwatch.StartNew();
            var log = new QueryLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                HumanQuery = payload.HumanQuery ?? string.Empty,
                Success = false
            };

            try
            {
                if (string.IsNullOrWhiteSpace(payload.HumanQuery))
                {
                    log.ErrorMessage = "La consulta humana no puede ser vacía.";
                    _logService.AddLog(log);
                    return BadRequest(new { message = log.ErrorMessage });
                }

                // Obtener schema
                log.DatabaseSchema = GetSchema();

                // Generar SQL
                var (sqlQueryJson, promptSql) = await HumanQueryToSqlAsync(payload.HumanQuery);
                log.PromptToGenerateSql = promptSql;

                if (string.IsNullOrEmpty(sqlQueryJson))
                {
                    log.ErrorMessage = "No se pudo generar la consulta SQL.";
                    _logService.AddLog(log);
                    return BadRequest(new { error = log.ErrorMessage });
                }

                Dictionary<string, string>? sqlQueryResult;
                try
                {
                    // Clean markdown formatting from OpenAI response
                    var cleanedJson = CleanJsonFromMarkdown(sqlQueryJson);
                    sqlQueryResult = JsonConvert.DeserializeObject<Dictionary<string, string>>(cleanedJson);
                }
                catch (Exception ex)
                {
                    log.ErrorMessage = $"No se pudo interpretar la respuesta de OpenAI: {ex.Message}";
                    _logService.AddLog(log);
                    return BadRequest(new { error = log.ErrorMessage });
                }

                if (sqlQueryResult == null || !sqlQueryResult.TryGetValue("sql_query", out var sqlQuery))
                {
                    log.ErrorMessage = "La respuesta no contenía la consulta SQL.";
                    _logService.AddLog(log);
                    return BadRequest(new { error = log.ErrorMessage });
                }

                log.GeneratedSql = sqlQuery;

                // Ejecutar query
                var result = await QueryDatabaseAsync(sqlQuery);
                log.ResultCount = result.Count;

                // Guardar datos crudos en JSON
                log.RawDataJson = JsonConvert.SerializeObject(result);

                // Generar respuesta humanizada
                var (answer, promptHumanize) = await BuildAnswerAsync(result, payload.HumanQuery);
                log.PromptToHumanize = promptHumanize;

                if (string.IsNullOrEmpty(answer))
                {
                    log.ErrorMessage = "No se pudo generar la respuesta humana.";
                    _logService.AddLog(log);
                    return BadRequest(new { error = log.ErrorMessage });
                }

                log.HumanizedAnswer = answer;
                log.Success = true;

                return Ok(new {
                    success = true,
                    respuestaHumanizada = answer,
                    sqlGenerado = sqlQuery,
                    resultadosCrudos = result
                });
            }
            catch (Exception ex)
            {
                log.ErrorMessage = ex.Message;
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
            finally
            {
                stopwatch.Stop();
                log.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                _logService.AddLog(log);
            }
        }
    }

    public class PostHumanQueryPayload
    {
        public string HumanQuery { get; set; }
    }
}
